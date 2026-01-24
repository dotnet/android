# Invokers May Not Need TypeMap Entries

> Analysis of whether invoker types need their own entries in the type mapping system.

## Background

The current `type-mapping-api-v2-spec.md` includes invokers as first-class citizens in the typemap:
- Section 4.2: Interface-to-Invoker Mapping
- Section 6.4: Invoker Proxy generation
- `TryGetInvokerType(Type, out Type)` API

This document argues that **invokers don't need separate typemap entries or proxy classes**.

---

## How Invokers Work Today

### Runtime Flow (Java.Interop)

When `CreatePeer` encounters an interface/abstract type:

```csharp
// JniRuntime.JniValueManager.cs:403
type = Runtime.TypeManager.GetInvokerType(type) ?? type;
var self = GetUninitializedObject(type);
```

The invoker type replaces the interface type and is instantiated via activation constructor.

### Invoker Discovery

Currently uses `[JniTypeSignature(InvokerType=typeof(...))]` attribute:

```csharp
[JniTypeSignature("net/dot/jni/test/JavaInterface", InvokerType=typeof(IJavaInterfaceInvoker))]
interface IJavaInterface : IJavaPeerable { ... }
```

The `GetInvokerTypeCore` method reads this attribute to find the invoker.

---

## Key Insight: Interface Proxy Can Handle Everything

The spec already recommends (Section 6.3) that interface proxies directly create invoker instances:

```csharp
public sealed class IOnClickListener_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new IOnClickListenerInvoker(handle, transfer);  // Directly creates invoker!
}
```

If the interface's `CreateInstance` directly instantiates the invoker, then:

1. **No typemap lookup needed** for the invoker type
2. **No proxy class needed** for the invoker
3. **`TryGetInvokerType` becomes mostly obsolete**

---

## Why Invokers Don't Need TypeMap Entries

| Invoker Characteristic | Implication |
|------------------------|-------------|
| No JCW (`DoNotGenerateAcw=true`) | No LLVM IR stubs → No `GetFunctionPointer` needed |
| No Java-initiated instantiation | Never activated via `nc_activate_*` from Java |
| Only created when wrapping Java objects | Interface proxy's `CreateInstance` handles this |
| No user subclasses expected | Not a "CreateInstance" scenario users would trigger |
| Shares Java type name with interface | Would be duplicate entry in `_externalTypeMap` anyway |

### What Would an Invoker Proxy Even Do?

```csharp
// Hypothetical invoker proxy - but why generate this?
public sealed class IOnClickListenerInvoker_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
        => throw new NotSupportedException("Invokers have no JCW");
    
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new IOnClickListenerInvoker(handle, transfer);
}
```

This is **identical** to what the interface proxy already does! The invoker proxy is redundant.

---

## Revised Type Categorization

| Type | Needs TypeMap Entry? | Needs Proxy Class? | Why |
|------|---------------------|-------------------|-----|
| **Interface** | ✅ Yes | ✅ Yes | Java name lookup; `CreateInstance` → invoker |
| **Invoker** | ❌ No | ❌ No | Created by interface proxy directly |
| **Implementor** | ✅ Yes | ✅ Yes | Has JCW; needs activation + callbacks |
| **Abstract class** | ✅ Yes | ✅ Yes | Has invoker pattern like interfaces |
| **Concrete MCW** | ✅ Yes | ✅ Yes | Instance creation |
| **User JCW class** | ✅ Yes | ✅ Yes | Activation + virtual callbacks |

---

## Trimming Consideration

The invoker type still needs to survive trimming when the interface survives. This is handled by:

```csharp
[assembly: TypeMapAssociation<Java.Lang.Object>(typeof(IOnClickListener), typeof(IOnClickListenerInvoker))]
```

This association ensures:
- When `IOnClickListener` is preserved → `IOnClickListenerInvoker` is preserved
- The interface proxy can still call `new IOnClickListenerInvoker(...)` at runtime

**No proxy class is needed** - just the association attribute for trimming.

---

## `TryGetInvokerType` API

### Current Usages

1. **`CreatePeer` fallback** - but interface proxy handles this directly
2. **Reflection scenarios** - rare, could use attribute directly
3. **Legacy code** - transitional support

### Recommendation

Keep the API but document it as **legacy/fallback**:

```csharp
/// <summary>
/// Gets the invoker type for an interface or abstract class.
/// </summary>
/// <remarks>
/// In the TypeMap v2 system, this is primarily used as a fallback.
/// The preferred path is for interface proxies to directly instantiate
/// invokers in their CreateInstance method.
/// </remarks>
bool TryGetInvokerType(Type type, [NotNullWhen(true)] out Type? invokerType);
```

---

## Spec Changes Required

### Remove/Simplify

1. **Section 6.4** (Invoker Proxy) - remove entirely
2. **Section 16.2** - remove "Generate proxy types for Invokers" item
3. **Table in Section 4.2.1** - update to show invokers don't need proxies

### Keep

1. **Section 4.2** - Interface-to-Invoker Mapping (for trimming via `TypeMapAssociation`)
2. **`TryGetInvokerType` in `ITypeMap`** - as legacy/fallback API

### Update

1. **Section 5** - Clarify that invokers are not in the proxy type table
2. **Section 9.2** - Mark `TryGetInvokerType` as fallback

---

## Test Cases to Verify

From `external/Java.Interop/tests/Java.Interop-Tests/`:

1. **`JniRuntime.JniTypeManagerTests.cs:15-27`** - `GetInvokerType()` test
2. **`JniRuntimeJniValueManagerContract.cs:148-161`** - `CreatePeer_UsesFallbackType` test

These tests should continue to pass because:
- `GetInvokerType` still works (reads `[JniTypeSignature]` attribute)
- The test creates a peer for `IJavaInterface` and expects `IJavaInterfaceInvoker`

With the new approach, the interface proxy's `CreateInstance` would handle this directly, but the test doesn't care *how* the invoker is created, just that the result is an invoker instance.

---

## Summary

**Invokers don't need:**
- ❌ Their own entries in `_externalTypeMap`
- ❌ Their own proxy classes
- ❌ `GetFunctionPointer` implementations (no JCW)

**Invokers still need:**
- ✅ `TypeMapAssociation` for trimming preservation
- ✅ Activation constructor (called by interface proxy)
- ✅ To be instantiable (interface proxy calls `new InvokerType(...)`)

**This simplifies codegen** by eliminating an entire category of proxy types.
