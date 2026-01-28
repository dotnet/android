# ILLink UnsafeAccessor Constructor Marking Limitation

## Summary

ILLink marks **all constructors** of a type when it encounters `[UnsafeAccessor(UnsafeAccessorKind.Constructor)]`, regardless of the accessor method's parameter signature. This is a known design decision in ILLink, not a bug.

## Background

.NET for Android uses `UnsafeAccessor` to call protected activation constructors (e.g., `.ctor(IntPtr, JniHandleOwnership)`) from generated proxy types. This is necessary because:

1. Activation constructors are `protected` to prevent direct instantiation
2. The TypeMaps assembly needs to create instances of these types at runtime
3. `UnsafeAccessor` provides a clean, AOT-compatible way to access protected members (previously we used reflection to call the same constructor)

## The Problem

When ILLink sees an `UnsafeAccessor` targeting a constructor:

```csharp
[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
static extern ContextThemeWrapper CreateInstanceUnsafe(IntPtr handle, JniHandleOwnership transfer);
```

It marks **all** constructors of `ContextThemeWrapper`, not just the one matching the signature:

- `.ctor(IntPtr, JniHandleOwnership)` ✓ intended
- `.ctor()` ✗ unnecessary  
- `.ctor(Context, int)` ✗ unnecessary
- `.ctor(Context, Resources.Theme)` ✗ unnecessary

This causes a cascade of type preservation. For example, `ContextThemeWrapper(Context, Resources.Theme)` pulls in:
- `Resources.Theme` (nested type)
- `Resources` (enclosing type)
- `Resources(AssetManager, DisplayMetrics, Configuration)` constructor parameters
- And so on...

## Root Cause in ILLink

The behavior is in `dotnet/runtime` at `src/tools/illink/src/linker/Linker.Steps/UnsafeAccessorMarker.cs`:

```csharp
void ProcessConstructorAccessor(MethodDefinition method, string? name)
{
    // ...
    foreach (MethodDefinition targetMethod in targetType.Methods)
    {
        if (!targetMethod.IsConstructor || targetMethod.IsStatic)
            continue;

        // Marks ALL instance constructors, no signature matching
        _markStep.MarkMethodVisibleToReflection(targetMethod, ...);
    }
}
```

Compare this to `ProcessMethodAccessor` which at least filters by name:

```csharp
void ProcessMethodAccessor(MethodDefinition method, string? name, bool isStatic)
{
    // ...
    foreach (MethodDefinition targetMethod in targetType.Methods)
    {
        if (targetMethod.Name != name || targetMethod.IsStatic != isStatic)
            continue;  // Filters by name

        _markStep.MarkMethodVisibleToReflection(targetMethod, ...);
    }
}
```

## Why ILLink Does This

From the original commit message ([ac3979aca41](https://github.com/dotnet/runtime/commit/ac3979aca41)):

> "The implementation ran into a problem when trying to precisely match the signature overload resolution. Due to Cecil issues and the fact that Cecil's resolution algorithm is not extensible, it was not possible to match the runtime's behavior without adding lot more complexity... So, to simplify the implementation, **trimmer will mark all methods of a given name**. This means it will mark more than necessary."

For constructors, since all are named `.ctor`, this results in marking **everything**.

## Impact on .NET for Android

In a HelloWorld app:
- **With legacy native typemap**: ~33 types in typemap
- **With TypeMapAttribute + UnsafeAccessor (original approach)**: ~54 types in typemap
- **With TypeMapAttribute + ldftn/calli (current approach)**: ~26 types in typemap

The extra types in the UnsafeAccessor approach are due to ILLink marking all constructors, which pulls in constructor parameter types like `Resources.Theme`.

## Current Mitigation: ldftn + calli

We've implemented an alternative approach for XI-style types (types with protected activation constructors). Instead of using `UnsafeAccessor`, we generate IL that uses `ldftn` + `calli` to call the constructor directly:

```il
; Get uninitialized object
ldtoken TargetType
call Type.GetTypeFromHandle
call RuntimeHelpers.GetUninitializedObject
castclass TargetType
dup
; Load args
ldarg.1  ; handle
ldarg.2  ; transfer
; Load function pointer and call
ldftn instance void TargetType::.ctor(IntPtr, JniHandleOwnership)
calli instance void(IntPtr, JniHandleOwnership)
ret
```

**Results**: With this approach, the trimmed TypeMap assembly contains **26 proxy types** (down from 54 with UnsafeAccessor), which is close to the legacy native typemap count (~33).

**Note**: ILLink still marks all constructors of the target type (visible in linker-dependencies.xml), but this doesn't prevent the TypeMap proxy from being trimmed. The proxy is trimmed based on whether the TypeMapAttribute is referenced by the app, not based on constructor dependencies.

We've also disabled the legacy `MarkJavaObjects` and `Preserve*` ILLink steps since the TypeMapAttribute system now handles type preservation directly.

## Remaining Issue

For JI-style types (types with internal activation methods like `GetObject`), we still use `UnsafeAccessor(UnsafeAccessorKind.Method)` to call the internal method. This works better than constructor accessors because method accessors at least filter by name, though they still don't filter by signature.

## Potential Future Improvements

### Option 1: Improve ILLink (Long-term)

Add signature matching to `ProcessConstructorAccessor` similar to how NativeAOT handles it. The runtime already does precise signature matching at JIT time, so the information is available.

**Pros**: Fixes the root cause, benefits all UnsafeAccessor users
**Cons**: Requires changes to dotnet/runtime

### Option 2: Make Activation Constructors Internal

Add `InternalsVisibleTo` for the TypeMaps assembly and make activation constructors `internal protected` instead of just `protected`.

**Pros**: Avoids UnsafeAccessor entirely for XI-style, could use regular `call` instead of `ldftn+calli`
**Cons**: Breaking API change, requires signing TypeMaps assembly

## Current Status

The `ldftn + calli` approach provides acceptable trimming results for XI-style types. The remaining overhead from extra constructor marking in Mono.Android doesn't significantly impact app size since those types/constructors are already needed by the app.

## Related Links

- [ILLink UnsafeAccessorMarker.cs](https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/linker/Linker.Steps/UnsafeAccessorMarker.cs)
- [Original UnsafeAccessor implementation PR #88268](https://github.com/dotnet/runtime/pull/88268)
- [UnsafeAccessor documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute)
