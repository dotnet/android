# Type Mapping API Specification for .NET Android

## 0. Open Questions

- [ ] What is the startup perf impact of loading large type maps?
    - TODO: measure how long it takes to load ~8000 TypeMapAttributes into a type map (untrimmed Debug build scenario) on Samsung A16 (low end phone)
- [ ] What is the potnetial size saving from excluding unnecessary .java and .o for trimmed classes?
    - TODO: compare the size of .dex and .so files with and without the trimmed java classes (~300 difference)
- [x] Do we need special handling for array and generic types (especially lists)?
    - **RESOLVED**: Yes, array types require special handling. See [Section 20.1: Array Type Handling](#201-array-type-handling) for the design.
    - Array types use `ArrayProxyAttribute<T>` for AOT-safe array creation
    - Generic list types are handled through the standard MCW binding pattern

## 1. Overview

### 1.1 Purpose

This specification defines the architecture for enabling Java-to-.NET interoperability in .NET Android applications using the .NET Type Mapping API. The design is fully compatible with Native AOT and trimming, replacing the legacy reflection-based `TypeManager` system.

### 1.2 Goals

- **AOT-Safe**: All type instantiation and method resolution works with Native AOT
- **Trimming-Safe**: Proper annotations ensure required types survive aggressive trimming
- **Single Activation Path**: All types (framework + user) use the same activation mechanism
- **Performance**: Minimize runtime overhead through caching at native and managed layers
- **Developer Experience**: No changes required to existing .NET Android application code
- **Export Support**: Handle `[Export]` methods with static codegen (no reflection)

### 1.3 Scope

- Release builds using CoreCLR or NativeAOT runtime
- All Java peer types: user classes, SDK bindings, interfaces, Implementors
- `[Register]` and `[Export]` attribute methods

**Out of scope:**
- Debug builds using Mono (continue to use existing reflection-based TypeManager)
- Non-shipping code (desktop JVM targets in java-interop repo)

### 1.4 Prerequisites

This specification requires .NET 11 SDK with [dotnet/runtime#121513](https://github.com/dotnet/runtime/pull/121513) merged. This PR adds the `--typemap-entry-assembly` ILLink flag that enables the `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic to work correctly with trimming. See [Section 19: Toolchain Requirements](#19-toolchain-requirements) for details.

---

## 2. Background

### 2.1 Managed-Callable Wrappers (MCW)

MCWs are .NET bindings for Java classes. They allow managed code to instantiate and call Java objects:

```csharp
// MCW - wraps existing Java class android.widget.TextView
[Register("android/widget/TextView", DoNotGenerateAcw = true)]
public class TextView : View { ... }
```

**`DoNotGenerateAcw = true`:** This flag indicates the type is a pure MCW (binding for an existing Java class). No JCW `.java` file is generated for it.

### 2.2 Java-Callable Wrappers (JCW/ACW)

JCWs are Java classes generated for .NET types that need to be callable from Java:

```csharp
// .NET class that will have a JCW generated
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState) { ... }
}
```

```java
// Generated JCW
public class MainActivity extends android.app.Activity
    implements mono.android.IGCUserPeer
{
    public MainActivity() {
        super();
        if (getClass() == MainActivity.class) {
            nc_activate_0();  // Activates .NET peer
        }
    }

    @Override
    public void onCreate(android.os.Bundle savedInstanceState) {
        n_onCreate(savedInstanceState);
    }

    private native void n_onCreate(android.os.Bundle savedInstanceState);
    private native void nc_activate_0();
}
```

### 2.3 Legacy System Limitations

The previous system used:
- `TypeManager.Activate()` with `Type.GetType()` calls - not AOT-safe
- Reflection-based constructor invocation via `Activator.CreateInstance()` - not trimming-safe
- Native type maps with integer indices - complex dual-table system

---

## 3. Architecture

### 3.1 High-Level Design

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              BUILD TIME                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │           MSBuild Task: GenerateTypeMaps                              │ │
│  │                                                                        │ │
│  │  1. Scan assemblies for Java peers ([Register], [Export], implements) │ │
│  │  2. Collect marshal methods + activation constructors                 │ │
│  │  3. Generate TypeMapAssembly.dll:                                     │ │
│  │     - TypeMap<T> attributes                                           │ │
│  │     - JavaPeerProxy subclasses with UCO methods                       │ │
│  │     - GetFunctionPointer switch statements                            │ │
│  │     - CreateInstance factory methods                                  │ │
│  │  4. Generate JCW .java files (user types + Implementors only)         │ │
│  │  5. Generate LLVM IR .ll files per type                               │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│           │                              │                    │             │
│           ▼                              ▼                    ▼             │
│  ┌────────────────┐             ┌────────────────┐    ┌────────────────────┐│
│  │ ILLink/Trimmer │             │ Java Compiler  │    │ LLVM → .o → .so    ││
│  └────────────────┘             └────────────────┘    └────────────────────┘│
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                               RUNTIME                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Java calls native method (e.g., n_onCreate)                                │
│           │                                                                 │
│           ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ LLVM-generated stub                                                    ││
│  │   1. Check cached function pointer (@fn_ptr_N)                         ││
│  │   2. If null: call typemap_get_function_pointer(className, len, idx)   ││
│  │   3. Call resolved UCO method                                          ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│           │                                                                 │
│           ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ TypeMapAttributeTypeMap.GetFunctionPointer(className, methodIndex)     ││
│  │   1. Lookup type via TypeMapping.Get<Java.Lang.Object>(className)      ││
│  │   2. Get cached JavaPeerProxy via GetCustomAttribute                   ││
│  │   3. Return proxy.GetFunctionPointer(methodIndex)                      ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│           │                                                                 │
│           ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ Generated UCO method (e.g., n_onCreate_mm_0)                           ││
│  │   - WaitForBridgeProcessing() (non-activation only)                    ││
│  │   - try { call original n_* callback } catch { UnhandledException }    ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Key Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `GenerateTypeMaps` | MSBuild task | Scan assemblies, generate TypeMapAssembly.dll, .java, .ll |
| `TypeMapAssembly.dll` | Generated | Contains proxies, UCOs, TypeMap attributes |
| `TypeMapAttributeTypeMap` | Mono.Android.dll | Runtime type lookup, function pointer resolution |
| `LlvmIrTypeMap` | Mono.Android.dll | Legacy Mono runtime path |
| `JavaPeerProxy` | Mono.Android.dll | Base class for generated proxies |
| LLVM IR stubs | Generated .ll | JNI entry points with caching |

### 3.3 ITypeMap Interface

The `ITypeMap` interface abstracts type mapping operations:

```csharp
interface ITypeMap
{
    // Java-to-.NET type resolution
    bool TryGetTypesForJniName(string jniSimpleReference, [NotNullWhen(true)] out IEnumerable<Type>? types);

    // Get invoker type for interface/abstract class
    bool TryGetInvokerType(Type type, [NotNullWhen(true)] out Type? invokerType);

    // .NET-to-Java type resolution
    bool TryGetJniNameForType(Type type, [NotNullWhen(true)] out string? jniName);
    IEnumerable<string> GetJniNamesForType(Type type);

    // Peer instance creation
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);

    // Marshal method function pointer resolution
    IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex);

    // Array creation (AOT-safe)
    // rank=1 for T[], rank=2 for T[][]
    Array CreateArray(Type elementType, int length, int rank);
}
```

### 3.4 Runtime Selection

At initialization, the runtime selects the appropriate `ITypeMap` implementation:

```csharp
private static ITypeMap CreateTypeMap()
{
    if (RuntimeFeature.IsCoreClrRuntime)
        return new TypeMapAttributeTypeMap();  // AOT-safe, uses generated attributes
    else if (RuntimeFeature.IsMonoRuntime)
        return new LlvmIrTypeMap();            // Legacy path
    else
        throw new NotSupportedException();
}
```

### 3.5 Feature Switches

| Switch | Default | Purpose |
|--------|---------|---------|
| `Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime` | `true` | Use LlvmIrTypeMap (legacy) |
| `Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime` | `false` | Use TypeMapAttributeTypeMap |

---

## 4. Type Classification

### 4.1 Types That Need Proxies

| Type Category | Example | JCW? | TypeMap Entry | GetFunctionPointer | CreateInstance |
|---------------|---------|------|---------------|-------------------|----------------|
| User class with JCW | `MainActivity` | Yes | ✅ | Returns UCO ptrs | `new T(h, t)` |
| SDK MCW (binding) | `Activity` | No | ✅ | Throws | `new T(h, t)` |
| Interface | `IOnClickListener` | No | ✅ | Throws | Returns Invoker |
| Implementor | `IOnClickListenerImplementor` | Yes | ✅ | Returns UCO ptrs | `new T(h, t)` |

### 4.2 Types That Do NOT Need Proxies

| Type Category | Example | Reason |
|---------------|---------|--------|
| Invoker | `IOnClickListenerInvoker` | Share JNI name with interface; instantiated by interface proxy |
| `DoNotGenerateAcw` types without activation | Internal helpers | No JCW, no peer creation from Java |
| Generic types | `List<T>` | Not directly mapped to Java |

**Key Design Decision:** Invokers are excluded from the TypeMap because:
1. They have `DoNotGenerateAcw=true` (no JCW)
2. They share the same JNI name as their interface
3. They are only instantiated by the interface proxy's `CreateInstance` method
4. No `GetFunctionPointer` calls ever target them

---

## 5. Type Map Attributes

### 5.1 TypeMap Attribute Structure

Each Java peer type is registered using assembly-level attributes:

```csharp
// TypeMap<TUniverse>(string jniClassName, Type proxyType, Type trimTarget)
// - jniClassName: Java class name used as lookup key
// - proxyType: The proxy type RETURNED by TypeMap lookups
// - trimTarget: Ensures trimmer preserves mapping when target is used

[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(MainActivity_Proxy), typeof(MainActivity))]
```

### 5.2 Proxy Self-Application Pattern

The proxy type applies itself as an attribute to itself:

```csharp
// TypeMap returns the proxy type
[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(MainActivity_Proxy), typeof(MainActivity))]

// Proxy applies ITSELF as an attribute to ITSELF
[MainActivity_Proxy]  // Self-application
public sealed class MainActivity_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex) => ...;
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new MainActivity(handle, transfer);
}

// At runtime:
Type proxyType = typeMap["com/example/MainActivity"];  // Returns typeof(MainActivity_Proxy)
JavaPeerProxy proxy = proxyType.GetCustomAttribute<JavaPeerProxy>();  // Returns MainActivity_Proxy instance
IJavaPeerable instance = proxy.CreateInstance(handle, transfer);  // Returns MainActivity instance
```

**Why this works:**
1. TypeMap returns the proxy type, not the target type
2. The .NET runtime's `GetCustomAttribute<T>()` instantiates attributes in an AOT-safe manner
3. The `trimTarget` parameter ensures the mapping is preserved when the target type survives trimming

### 5.3 Interface-to-Invoker Mapping

Interfaces cannot be instantiated directly. The interface proxy's `CreateInstance` directly returns an Invoker instance:

```csharp
[IOnClickListener_Proxy]
public sealed class IOnClickListener_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
    {
        // Interfaces don't have JCWs - no native methods call them directly
        throw new NotSupportedException("GetFunctionPointer not supported for interface types.");
    }

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Directly create Invoker instance - no separate lookup needed
        return new IOnClickListenerInvoker(handle, transfer);
    }
}
```

---

## 6. JavaPeerProxy Design

### 6.1 Base Class

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
abstract class JavaPeerProxy : Attribute
{
    /// <summary>
    /// Returns the function pointer for the UCO method at the given index.
    /// </summary>
    public abstract IntPtr GetFunctionPointer(int methodIndex);

    /// <summary>
    /// Creates an instance of the target type wrapping the given Java object.
    /// </summary>
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}
```

### 6.2 Proxy Behavior by Type Category

| Type Category | `GetFunctionPointer` Behavior | `CreateInstance` Behavior |
|---------------|-------------------------------|---------------------------|
| Concrete class with JCW | Returns UCO function pointers | Creates instance of the type |
| MCW (framework binding) | Throws `NotSupportedException` | Creates instance of the type |
| Interface | Throws `NotSupportedException` | Creates instance of **Invoker** |
| Implementor | Returns UCO function pointers | Creates instance of the implementor |

---

## 7. Generated Proxy Types

### 7.1 User Class Proxy

```csharp
[com_example_MainActivity_Proxy]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class com_example_MainActivity_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch
    {
        0 => (IntPtr)(delegate*<IntPtr, IntPtr, IntPtr, void>)&n_onCreate_mm_0,
        1 => (IntPtr)(delegate*<IntPtr, IntPtr, void>)&nc_activate_0,
        _ => IntPtr.Zero
    };

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new MainActivity(handle, transfer);

    [UnmanagedCallersOnly]
    public static void n_onCreate_mm_0(IntPtr jnienv, IntPtr obj, IntPtr p0)
    {
        AndroidRuntimeInternal.WaitForBridgeProcessing();
        try {
            MainActivity.n_OnCreate(jnienv, obj, p0);
        } catch (Exception ex) {
            AndroidEnvironmentInternal.UnhandledException(jnienv, ex);
        }
    }

    [UnmanagedCallersOnly]
    public static void nc_activate_0(IntPtr jnienv, IntPtr jobject)
    {
        if (JniEnvironment.WithinNewObjectScope)
            return;
        if (Java.Lang.Object.PeekObject(jobject) != null)
            return;

        var instance = (MainActivity)RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
        ((IJavaPeerable)instance).SetPeerReference(new JniObjectReference(jobject));
        CallActivationCtor(instance, jobject, JniHandleOwnership.DoNotTransfer);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
    static extern void CallActivationCtor(MainActivity instance, IntPtr handle, JniHandleOwnership transfer);
}
```

### 7.2 Implementor Proxy

Implementors implement Java interfaces and ARE callable from Java. Their UCO wrappers call the **Invoker's** static callback methods:

```csharp
[mono_android_view_View_OnClickListenerImplementor_Proxy]
public sealed class mono_android_view_View_OnClickListenerImplementor_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch
    {
        0 => (IntPtr)(delegate*<IntPtr, IntPtr, IntPtr, void>)&n_OnClick_mm_0,
        1 => (IntPtr)(delegate*<IntPtr, IntPtr, void>)&nc_activate_0,
        _ => IntPtr.Zero
    };

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new View.IOnClickListenerImplementor(handle, transfer);

    [UnmanagedCallersOnly]
    public static void n_OnClick_mm_0(IntPtr jnienv, IntPtr obj, IntPtr v)
    {
        AndroidRuntimeInternal.WaitForBridgeProcessing();
        try {
            // Call the INVOKER's static callback, not the Implementor's
            View.IOnClickListenerInvoker.n_OnClick_Landroid_view_View_(jnienv, obj, v);
        } catch (Exception ex) {
            AndroidEnvironmentInternal.UnhandledException(jnienv, ex);
        }
    }

    [UnmanagedCallersOnly]
    public static void nc_activate_0(IntPtr jnienv, IntPtr jobject) { /* ... */ }
}
```

**Key Design:** Implementors don't define `n_*` callbacks - the callbacks are in the Invoker class. The UCO wrapper extracts the callback method reference from the connector string in the `[Register]` attribute.

---

## 8. Method Index Ordering Contract

### 8.1 The Problem

LLVM IR stubs call `GetFunctionPointer(className, methodIndex)`. Both LLVM IR and the C# `GetFunctionPointer` switch must use identical indexing.

### 8.2 The Contract

**ORDERING RULE:** Methods are indexed in the following order:

1. **Regular marshal methods** (indices 0 to n-1)
   - Enumerate in declaration order from `[Register]` and `[Export]` attributes

2. **Activation constructors** (indices n to m-1)
   - One per activation constructor style (XI or JI)

**BOTH** the IL generator and LLVM IR generator MUST iterate methods in identical order.

### 8.3 Implementation Pattern

```csharp
int methodIndex = 0;

// First: regular methods (ordered consistently)
foreach (var method in type.GetMethodsWithRegisterOrExport().OrderBy(m => m.Name))
{
    EmitLlvmStub(method, methodIndex);
    EmitUcoWrapper(method, methodIndex);
    methodIndex++;
}

// Second: activation constructors
foreach (var ctor in type.GetActivationConstructors())
{
    EmitLlvmStub(ctor, methodIndex);
    EmitUcoWrapper(ctor, methodIndex);
    methodIndex++;
}
```

---

## 9. Activation Constructor Handling

### 9.1 Constructor Styles

| Style | Signature | Used By |
|-------|-----------|---------|
| **XI** | `(IntPtr handle, JniHandleOwnership transfer)` | Classic Xamarin.Android |
| **JI** | `(ref JniObjectReference reference, JniObjectReferenceOptions options)` | Java.Interop |

**Search order:** XI first, then JI.

### 9.2 Constructor Search Algorithm

```
1. Check if type T has XI ctor → use directly
2. Check if type T has JI ctor → use directly
3. Walk up hierarchy:
   a. Check BaseType for XI ctor → use with [UnsafeAccessor]
   b. Check BaseType for JI ctor → use with [UnsafeAccessor]
4. If no ctor found → emit build error
```

### 9.3 IgnoresAccessChecksTo for Protected Constructors

When the activation constructor is protected or in a base class, the TypeMaps assembly uses `IgnoresAccessChecksToAttribute` to bypass access checks:

```csharp
// Assembly-level attribute enables access to Mono.Android internals
[assembly: IgnoresAccessChecksTo("Mono.Android")]

// Direct newobj call works despite protected constructor
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
{
    return new Activity(handle, transfer);  // Direct call, not UnsafeAccessor
}
```

**IL generated**:
```il
ldarg.1          // handle
ldarg.2          // transfer
newobj instance void [Mono.Android]Android.App.Activity::.ctor(native int, valuetype JniHandleOwnership)
ret
```

**Note:** When using base class constructor, derived type field initializers do NOT run. This matches legacy behavior. See Section 20.6 for full details on IgnoresAccessChecksTo.

### 9.4 JI Constructor Handle Cleanup

For JI-style constructors, the `CreateInstance` method must:
1. Create `JniObjectReference` from handle: `new JniObjectReference(handle)`
2. Call the constructor with `JniObjectReferenceOptions.Copy`
3. After constructor returns, call `JNIEnv.DeleteRef(handle, transfer)` to clean up

```csharp
// JI-style CreateInstance pattern:
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
{
    var reference = new JniObjectReference(handle);
    var result = CreateInstanceUnsafe(ref reference, JniObjectReferenceOptions.Copy);
    JNIEnv.DeleteRef(handle, transfer);  // Clean up original handle
    return result;
}
```

**Build-time constant reading:** The `JniObjectReferenceOptions.Copy` value should be read from the Java.Interop assembly at build time using `MetadataReader`, not hardcoded. This ensures the generated IL stays in sync if the enum definition ever changes:

```csharp
// In JavaPeerScanner, when processing Java.Interop assembly:
void ReadJniObjectReferenceOptionsValues(MetadataReader reader)
{
    // Find JniObjectReferenceOptions enum
    // Read the "Copy" field's constant value
    // Store for use during IL generation
}
```

**Known issue (legacy behavior):** If the constructor throws an exception, the handle leaks. The legacy `LlvmIrTypeMap.CreateProxy` has the same issue. A future improvement could wrap the constructor call in try-finally to ensure cleanup:

```csharp
// Potential improvement for final implementation:
try {
    result = CreateInstanceUnsafe(ref reference, JniObjectReferenceOptions.Copy);
} finally {
    JNIEnv.DeleteRef(handle, transfer);
}
```

---

## 10. Export Attribute Support

### 10.1 Approach

Handle `[Export]` identically to `[Register]` at build time:

```csharp
[Export("myCustomMethod")]
public void MyMethod(int value) { ... }

// Treated same as:
// [Register("myCustomMethod", "(I)V", "n_myCustomMethod")]
```

### 10.2 JNI Signature Derivation

When `[Export]` doesn't specify a signature, derive from .NET types:

| .NET Type | JNI Type |
|-----------|----------|
| `void` | `V` |
| `bool` | `Z` |
| `byte` | `B` |
| `char` | `C` |
| `short` | `S` |
| `int` | `I` |
| `long` | `J` |
| `float` | `F` |
| `double` | `D` |
| `string` | `Ljava/lang/String;` |
| `T[]` | `[` + element type |
| Other | `L{jni/class/name};` |

---

## 11. LLVM IR Generation

### 11.1 Per-Method Stub Template

```llvm
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8
@fn_ptr_{N} = internal unnamed_addr global ptr null, align 8
@class_name_{TypeName} = internal constant [{len} x i8] c"{utf16_bytes}", align 2

define default void @Java_{pkg}_{Class}_{method}(ptr %env, ptr %obj, ...) #0 {
entry:
    %cached = load ptr, ptr @fn_ptr_{N}, align 8
    %is_null = icmp eq ptr %cached, null
    br i1 %is_null, label %resolve, label %call

resolve:
    %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
    call void %get_fn(ptr @class_name_{TypeName}, i32 {charCount}, i32 {methodIndex}, ptr @fn_ptr_{N})
    %resolved = load ptr, ptr @fn_ptr_{N}, align 8
    br label %call

call:
    %fn = phi ptr [ %cached, %entry ], [ %resolved, %resolve ]
    tail call void %fn(ptr %env, ptr %obj, ...)
    ret void
}

attributes #0 = { noinline nounwind "frame-pointer"="non-leaf" }
```

### 11.2 Callback Signature

```c
void (*typemap_get_function_pointer)(
    const char16_t* className,  // UTF-16 Java class name (NOT null-terminated)
    int32_t classNameLength,    // Length in char16_t units
    int32_t methodIndex,        // Index into proxy's GetFunctionPointer switch
    intptr_t* fnptr             // Out: resolved function pointer
);
```

**UTF-16 class names:** Stored as UTF-16 so they can be viewed directly as `ReadOnlySpan<char>` on the managed side without string allocation.

### 11.3 Symbol Visibility

JNI symbols MUST have `default` visibility:

```llvm
define default void @Java_...  ; NOT hidden!
```

### 11.4 File Organization

| File | Content |
|------|---------|
| `marshal_methods_{TypeName}.ll` | Per-type JNI stubs |
| `marshal_methods_init.ll` | Global `typemap_get_function_pointer` declaration |

---

## 12. UCO Wrapper Generation

### 12.1 Regular Method UCO

```csharp
[UnmanagedCallersOnly]
public static void n_{MethodName}_mm_{Index}(IntPtr jnienv, IntPtr obj, ...)
{
    AndroidRuntimeInternal.WaitForBridgeProcessing();
    try
    {
        TargetType.n_{MethodName}_{JniSignature}(jnienv, obj, ...);
    }
    catch (Exception ex)
    {
        AndroidEnvironmentInternal.UnhandledException(jnienv, ex);
    }
}
```

### 12.2 Activation UCO

```csharp
[UnmanagedCallersOnly]
public static void nc_activate_{Index}(IntPtr jnienv, IntPtr jobject)
{
    // Skip if being constructed from managed side
    if (JniEnvironment.WithinNewObjectScope)
        return;

    // Skip if peer already exists
    if (Java.Lang.Object.PeekObject(jobject) != null)
        return;

    // Create uninitialized instance
    var instance = (TargetType)RuntimeHelpers.GetUninitializedObject(typeof(TargetType));

    // Set peer reference
    ((IJavaPeerable)instance).SetPeerReference(new JniObjectReference(jobject));

    // Call activation constructor
    CallActivationCtor(instance, jobject, JniHandleOwnership.DoNotTransfer);
}
```

### 12.3 Blittable Parameter Handling

`[UnmanagedCallersOnly]` requires blittable parameters. Replace `bool` with `byte`:

```csharp
// Original: n_setEnabled(JNIEnv*, jobject, jboolean)
[UnmanagedCallersOnly]
public static void n_setEnabled_mm_0(IntPtr jnienv, IntPtr obj, byte enabled)
{
    TargetType.n_setEnabled(jnienv, obj, enabled);
}
```

---

## 13. Alias Handling

### 13.1 When Aliases Are Needed

Multiple .NET types registered with the same Java class name:

```csharp
[Register("com/example/Handler")]
class HandlerA : Java.Lang.Object { }

[Register("com/example/Handler")]  // Same!
class HandlerB : Java.Lang.Object { }
```

### 13.2 Alias Holder Pattern

```csharp
// Base name → alias holder
[assembly: TypeMap<Java.Lang.Object>("com/example/Handler", typeof(Handler_Aliases), typeof(Handler_Aliases))]

// Indexed names → proxies
[assembly: TypeMap<Java.Lang.Object>("com/example/Handler[0]", typeof(HandlerA_Proxy), typeof(HandlerA))]
[assembly: TypeMap<Java.Lang.Object>("com/example/Handler[1]", typeof(HandlerB_Proxy), typeof(HandlerB))]

// Associations for trimmer (preserves alias holder when target is used)
[assembly: TypeMapAssociation<AliasesUniverse>(typeof(HandlerA), typeof(Handler_Aliases))]
[assembly: TypeMapAssociation<AliasesUniverse>(typeof(HandlerB), typeof(Handler_Aliases))]

[JavaInteropAliases("com/example/Handler[0]", "com/example/Handler[1]")]
sealed class Handler_Aliases { }
```

### 13.3 LLVM IR Must Use Indexed Names

JCW for `HandlerB` must look up `"com/example/Handler[1]"`, NOT `"com/example/Handler"`:

```llvm
@class_name = internal constant [...] c"c\00o\00m\00/\00...\00[\001\00]\00"
```

The bracket characters `[` and `]` cannot appear in valid JNI names, guaranteeing no collisions.

---

## 14. Runtime Caching

### 14.1 Native Layer

Per-method function pointer globals in LLVM IR. First call resolves, subsequent calls use cached value.

### 14.2 Managed Layer

```csharp
public sealed class TypeMapAttributeTypeMap : ITypeMap
{
    private readonly Dictionary<Type, JavaPeerProxy?> _proxyCache = new();
    private readonly Lock _proxyCacheLock = new();

    private JavaPeerProxy? GetOrCreateProxy(Type proxyType)
    {
        lock (_proxyCacheLock)
        {
            if (_proxyCache.TryGetValue(proxyType, out var cached))
                return cached;

            var proxy = proxyType.GetCustomAttribute<JavaPeerProxy>();
            _proxyCache[proxyType] = proxy;
            return proxy;
        }
    }
}
```

---

## 15. Build Pipeline

### 15.1 Debug Build (No Trimming)

```
1. Compile user assemblies
         │
         ▼
2. GenerateTypeMaps (MSBuild Task)
   - Scans ALL assemblies for [Register] types
   - Generates TypeMapAssembly.dll with ALL types
   - Generates JCW .java files for ALL types
   - Generates marshal_methods_{TypeHash}.ll per type
         │
         ├──────────────────────────────────────────────┐
         ▼                                              ▼
3. LLVM Compilation                              4. Java Compilation
   - ALL .ll → .o                                   - ALL .java → .class
   - Per-arch                                             │
         │                                                ▼
         ▼                                         5. d8 (no shrinking)
6. Native Linking                                      - .class → .dex
   - Link ALL .o files                                    │
         │                                                │
         └────────────────────────────────────────────────┤
                                                          │
                                                          ▼
                                                   7. Package APK/AAB
                                                      (larger, all code included)
```

### 15.2 Release Build (With Trimming)

```
1. Compile user assemblies
         │
         ▼
2. GenerateTypeMaps (MSBuild Task) ─────────────────────────────────────┐
   - Scans ALL assemblies for [Register] types                         │
   - Generates TypeMapAssembly.dll with ALL types                       │
   - Generates JCW .java files for ALL types                            │
   - Generates marshal_methods_{TypeHash}.ll per type                   │
         │                                                              │
         ├──────────────────────────────────────────────┐               │
         ▼                                              ▼               │
3. ILLink/ILC (trims assemblies)                 4. Java Compilation    │
   - TypeMap attrs are roots                        - ALL .java → .class│
   - Some types are TRIMMED                               │             │
         │                                                │             │
         ▼                                                │             │
5. Post-Trimming Filter                                   │             │
   - Scan trimmed assemblies for surviving types          │             │
   - Compute list of .o files to link                     │             │
   - Generate ProGuard config for R8                      │             │
         │                                                │             │
         ├────────────────────┬───────────────────────────┤             │
         ▼                    ▼                           ▼             │
6. LLVM Compilation    7. Native Linking           8. R8 (Java shrink)  │
   - .ll → .o             - Link ONLY surviving        - Uses ProGuard  │
   - Per-arch               types' .o files             config          │
   - Can cache!                   │                           │         │
         │                        │                           │         │
         └────────────────────────┴───────────────────────────┘         │
                                  │                                     │
                                  ▼                                     │
                           9. Package APK/AAB ◄─────────────────────────┘
                              (optimized, dead code removed)
```

### 15.3 Debug vs Release Build Flows

The build flow differs significantly based on whether trimming is enabled:

#### Debug Builds (No Trimming)

```
GenerateTypeMaps
      │
      ├─► ALL .java files → javac → d8 → DEX
      │
      ├─► ALL .ll files → .o → link ALL into libmarshal_methods.so
      │
      └─► TypeMapAssembly.dll with ALL types
```

**Key points:**
- **No filtering needed** - all types survive
- ALL .java files compiled and packaged
- ALL .o files linked into native library
- Larger APK size (acceptable for Debug)
- Faster build (no trimming overhead)

#### Release Builds (With Trimming)

```
GenerateTypeMaps (ALL types)
      │
      ▼
ILLink/ILC (trims assemblies)
      │
      ▼
Post-Trimming Filter
      │
      ├─► Surviving .o files → link into libmarshal_methods.so
      │
      └─► ProGuard config → R8 removes dead Java classes
```

**Key points:**
- Generate EVERYTHING upfront (before trimming)
- After trimming, determine which types survived
- Filter both native (.o) and Java sides
- Smaller APK size (dead code removed)
- Longer build (trimming + filtering overhead)

#### Comparison Table

| Aspect | Debug | Release |
|--------|-------|---------|
| Trimming | OFF | ON |
| .ll/.o files | Link ALL | Link surviving only |
| Java classes | Compile ALL | R8 removes dead |
| ProGuard config | Not needed | Generated for surviving types |
| APK size | Larger | Optimized |
| Build speed | Faster | Slower (trimming overhead) |
| TypeMapAssembly.dll | ALL types | ALL types (roots for linker) |

### 15.3 Post-Trimming Filtering (Release Only)

**Problem:** We generate JCW .java files and LLVM IR .ll files for ALL types before trimming. After trimming, some .NET types are removed, but their Java classes and native stubs still exist.

**Solution:** After trimming, scan surviving assemblies and filter:

1. **Determine surviving types:** Scan trimmed assemblies for types with `[Register]` attribute still present
2. **Filter .o files:** Only link `marshal_methods_{TypeHash}.o` for surviving types
3. **Generate ProGuard config:** Only emit `-keep class` rules for surviving types

**Why this works:**
- Java loads JNI symbols via `dlsym` (dynamic lookup)
- Linker has NO visibility into which symbols Java calls
- We MUST explicitly control which .o files are linked
- `--gc-sections` does NOT help (symbols appear unreferenced to linker)

**Key insight:** The same "surviving types" list drives BOTH `.o` file filtering AND Proguard rule generation.

**When to skip filtering:** If `$(PublishTrimmed)` is false (Debug), skip the post-trimming filter entirely and link all .o files.

### 15.3 File Naming Convention

```
$(IntermediateOutputPath)/
├── typemap/
│   └── _Microsoft.Android.TypeMaps.dll        # TypeMap assembly (all types)
├── android/
│   ├── src/                                    # Generated JCW .java files
│   │   └── com/example/MainActivity.java
│   ├── marshal_methods_a1b2c3d4.ll            # TypeA
│   ├── marshal_methods_e5f6g7h8.ll            # TypeB
│   ├── marshal_methods_i9j0k1l2.ll            # TypeC (trimmed - not linked)
│   ├── arm64-v8a/
│   │   ├── marshal_methods_a1b2c3d4.o         # TypeA (linked)
│   │   ├── marshal_methods_e5f6g7h8.o         # TypeB (linked)
│   │   └── marshal_methods_i9j0k1l2.o         # TypeC (NOT linked)
│   └── x86_64/
│       └── ...
└── proguard/
    └── proguard_typemap.cfg                    # -keep rules for surviving types only
```

### 15.4 Type Hash Computation

Each type needs a stable identifier that:
- Uniquely identifies the type
- Can be computed at generation time AND post-trimming
- Survives trimming (based on type identity, not assembly position)

**Recommended:** Hash of `[Register]` attribute's Java class name:
```csharp
string typeHash = ComputeHash(javaClassName);  // e.g., "com/example/MainActivity" → "a1b2c3d4"
```

### 15.5 Incremental Build Strategy

| Type Source | Strategy |
|-------------|----------|
| SDK types (Mono.Android) | Pre-generate during SDK build, ship as artifacts |
| NuGet package types | Generate on first build, cache by package version |
| User types | Always regenerate (typically few types) |

### 15.6 TypeMap Attributes and Trimming

The trimmable type map system uses `TypeMapAttribute` to point to **proxy types**, which have direct references to the real types.

#### Key Design: TypeMapAttribute Points to PROXY Types

```csharp
// TypeMapAttribute points to the PROXY, not the original type
[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]

// The proxy has DIRECT REFERENCES to the real type's methods:
class MainActivity_Proxy {
    public static Java.Lang.Object CreateInstance(IntPtr handle, JniHandleOwnership ownership) {
        return new MainActivity(handle, ownership);  // Direct reference!
    }

    public static void n_OnCreate(IntPtr jnienv, IntPtr native__this, IntPtr bundle) {
        var __this = (MainActivity)Java.Lang.Object.GetObject<MainActivity>(native__this);
        __this.OnCreate(bundle);  // Direct reference!
    }
}
```

**Why this works:**
1. unconditional TypeMapAttribute **unconditionally preserves** MainActivity_Proxy
2. MainActivity_Proxy has **direct method references** to MainActivity
3. Trimmer sees references → **automatically preserves** MainActivity
4. **No `[DynamicDependency]` needed!**

#### TypeMapAttribute Constructor Variants

```csharp
// UNCONDITIONAL: TypeMapAttribute(string, Type) - proxy always preserved
// No [RequiresUnreferencedCode] - trimmer-safe!
[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]

// TRIMMABLE: TypeMapAttribute(string, Type, Type) - only if trimTarget is USED
// Has [RequiresUnreferencedCode] - may be trimmed!
[assembly: TypeMap<JavaObjects>("android/widget/TextView", typeof(TextView_Proxy), typeof(TextView))]
```

---

### 15.7 Legacy Trimmer Steps: Complete Analysis

This section provides a comprehensive analysis of ALL legacy ILLink custom steps, explaining what they do, why they exist, and how trimmable type map replaces them.

#### 15.7.1 Two Fundamentally Different Operations

| Operation | Method | Effect | When Called |
|-----------|--------|--------|-------------|
| **Mark** | `Annotations.Mark(type)` | Roots a type - it WILL be in final output | Unconditionally during assembly processing |
| **Preserve** | `Annotations.AddPreservedMethod(type, method)` | Keeps method IF type is already marked | Only after type is already marked |

**Critical Insight:** Most legacy steps only call `AddPreservedMethod()`, NOT `Mark()`. They don't root new types - they only preserve additional members on types that are ALREADY marked through normal code references.

#### 15.7.2 Step Categories

ILLink custom steps fall into two categories:

1. **Preservation Steps** - Call `Annotations.Mark()` or `AddPreservedMethod()` to affect trimming
2. **Non-Preservation Steps** - Modify assemblies or generate outputs, but don't affect what gets trimmed

Only preservation steps need trimmable type map replacements. Non-preservation steps either:
- Become post-trimming MSBuild tasks (work on already-trimmed assemblies)
- Are deemed unnecessary for modern scenarios
- Are unrelated to TypeMap and continue to work as-is

#### 15.7.3 Detailed Analysis: MarkJavaObjects (THE Key Step)

`MarkJavaObjects` is the ONLY step that unconditionally marks types. It has TWO entry points:

**Entry Point 1: `ProcessAssembly` (Unconditional Marking)**

Called once when an assembly is processed. Unconditionally marks types:

```csharp
public void ProcessAssembly(AssemblyDefinition assembly, ...) {
    foreach (var type in assembly.MainModule.Types) {
        // 1. Types with [Activity], [Service], etc. attributes
        if (ShouldPreserveBasedOnAttributes(type)) {
            Annotations.Mark(type);  // UNCONDITIONAL!
            PreserveJavaObjectImplementation(type);
            continue;
        }

        // 2. Custom views from layout XML files
        if (customViewMap.ContainsKey(type.FullName)) {
            Annotations.Mark(type);  // UNCONDITIONAL!
            PreserveJavaObjectImplementation(type);
            continue;
        }
    }
}
```

**Entry Point 2: `ProcessType` (Preservation Only)**

Called when a type is ALREADY marked (through code references or ProcessAssembly):

```csharp
public void ProcessType(TypeDefinition type) {
    // Only called if type is already marked!
    PreserveJavaObjectImplementation(type);  // AddPreservedMethod, not Mark
    if (IsImplementor(type))
        PreserveImplementor(type);
}
```

**Trimmable type map replacement:**
- Entry Point 1 → Use **unconditional TypeMapAttribute** (2-arg) for types with component attributes
- Entry Point 2 → **Proxy types have hard references** to activation ctors, so they're preserved automatically

#### 15.7.4 Detailed Analysis: PreserveJavaInterfaces

**What it does:** When an IJavaObject interface is marked, preserves ALL its methods.

```csharp
void ProcessType(TypeDefinition type) {
    if (!type.IsInterface) return;
    if (!type.ImplementsIJavaObject(cache)) return;

    foreach (MethodReference method in type.Methods)
        Annotations.AddPreservedMethod(type, method.Resolve());
}
```

**Why it exists:** Interface methods can be called from Java via the Invoker. If the interface is marked but methods are trimmed, Java calls would fail.

**Trimmable type map replacement:** Proxy types for interfaces have marshal methods that call the interface methods:
```csharp
class IContentHandler_Proxy {
    public static void n_Characters(IntPtr jnienv, IntPtr native__this, ...) {
        ((IContentHandler)obj).Characters(...);  // Hard reference to interface method!
    }
}
```
When the proxy is preserved, the interface method is preserved through the direct call.

#### 15.7.5 Detailed Analysis: PreserveRegistrations

**What it does:** When a method with `[Register]` is marked, preserves its handler/connector method.

```csharp
void ProcessMethod(MethodDefinition method) {
    if (!method.TryGetRegisterMember(out var member, out var nativeMethod, out var signature))
        return;

    // Preserve the handler method (e.g., GetOnCreateHandler)
    PreserveRegisteredMethod(method.DeclaringType, member, method);
}
```

**Why it exists:** Java calls the native method which invokes the handler. If the handler is trimmed, the call fails.

**Trimmable type map replacement:** Proxy types use `GetFunctionPointer()` or direct calls:
```csharp
class MainActivity_Proxy {
    static nint GetOnCreatePointer() =>
        (nint)(delegate* <IntPtr, IntPtr, IntPtr, void>)&n_OnCreate;

    public static void n_OnCreate(IntPtr jnienv, IntPtr native__this, IntPtr bundle) {
        var __this = GetObject<MainActivity>(native__this);
        __this.OnCreate(...);  // Hard reference to overridden method!
    }
}
```

#### 15.7.6 Detailed Analysis: PreserveApplications

**What it does:** When `[Application]` attribute is found, preserves BackupAgent and ManageSpaceActivity types.

```csharp
void PreserveApplicationAttribute(CustomAttribute attribute) {
    PreserveTypeProperty(attribute, "BackupAgent");      // Preserve default ctor
    PreserveTypeProperty(attribute, "ManageSpaceActivity");  // Preserve default ctor
}
```

**Why it exists:** Android creates these types at runtime. If they're trimmed, app crashes.

**Trimmable type map replacement - TypeMapAssociationAttribute:**

When the generator finds an `[Application]` attribute with `BackupAgent` or `ManageSpaceActivity` properties, it generates `TypeMapAssociationAttribute` to create a dependency between the Application type and the associated types:

```csharp
// During scanning phase:
if (IsApplicationAttribute(attr)) {
    associatedTypes = GetTypePropertyValues(attr, "BackupAgent", "ManageSpaceActivity");
}

// During generation phase:
foreach (var associatedType in peer.AssociatedTypes) {
    // Generates: [assembly: TypeMapAssociation<AliasesUniverse>(typeof(MyApp), typeof(MyBackupAgent))]
    AddApplicationAssociationAttribute(peer.ManagedTypeName, associatedType);
}
```

**How it works:**
1. When `MyApp` is activated (via JNI → proxy → activation ctor), it's an allocation from .NET's perspective
2. The trimmer sees `TypeMapAssociationAttribute<AliasesUniverse>(typeof(MyApp), typeof(MyBackupAgent))`
3. Because `MyApp` is allocated, `MyBackupAgent` is preserved

**Status:** ✅ Implemented in the generator.

#### 15.7.7 Detailed Analysis: PreserveJavaExceptions

**What it does:** When an exception type inheriting from `Java.Lang.Throwable` is marked, preserves its `string(message)` constructor.

```csharp
void ProcessType(TypeDefinition type) {
    if (type.IsJavaException(cache))
        PreserveStringConstructor(type);  // Preserve .ctor(string)
}
```

**Why it exists:** Java exceptions are wrapped with a message. If the string ctor is trimmed, wrapping fails.

**Trimmable type map replacement:** Exception proxy types call the string constructor:
```csharp
class MyException_Proxy {
    public static MyException CreateWithMessage(string message) {
        return new MyException(message);  // Hard reference to string ctor!
    }
}
```

#### 15.7.8 Detailed Analysis: PreserveExportedTypes

**What it does:** Marks methods/fields with `[Export]` or `[ExportField]` attributes.

```csharp
void ProcessExports(ICustomAttributeProvider provider) {
    foreach (var attribute in provider.CustomAttributes) {
        if (attribute is "Java.Interop.ExportAttribute") {
            Annotations.Mark(provider);  // MARKS the method/field!
            // Also marks exception types from Throws property
        }
    }
}
```

**Why it exists:** Exported methods are called from Java. They must be preserved even if not called from .NET.

**Trimmable type map replacement:** The generator detects `[Export]` and `[ExportField]` methods during scanning and includes them as marshal methods:

```csharp
// [Export] method detection - handled identically to [Register]
string? exportName = GetMethodExportAttribute(reader, method);
if (exportName != null) {
    methods.Add(new MarshalMethodInfo {
        JniName = exportName,
        JniSignature = BuildJniMethodSignature(paramTypes, returnType),
        NativeCallbackName = $"n_{exportName}",
        ...
    });
}

// [ExportField] method detection - returns a field value
string? exportFieldName = GetMethodExportFieldAttribute(reader, method);
if (exportFieldName != null) {
    methods.Add(new MarshalMethodInfo {
        JniName = exportFieldName,
        JniSignature = BuildJniMethodSignature([], returnType),  // No params
        NativeCallbackName = $"n_get_{exportFieldName}",
        ...
    });
}
```

**Status:** ✅ `[Export]` and `[ExportField]` are now handled in the generator.

#### 15.7.9 Complete ILLink Custom Steps Inventory

This table lists ALL custom ILLink steps used in .NET for Android and their trimmable type map replacement strategy:

| Step | Type | Phase | Replacement | Notes |
|------|------|-------|-------------|-------|
| **Preservation Steps:** | | | | |
| `MarkJavaObjects.ProcessAssembly` (custom views) | Marking | During MarkStep | TypeMap unconditional attr | Trimmable type map reads customview-map.txt |
| `MarkJavaObjects.ProcessAssembly` (HttpHandler) | Marking | During MarkStep | N/A | Deprecate `AndroidHttpClientHandlerType` (see [#10002](https://github.com/dotnet/android/pull/10002)) |
| `MarkJavaObjects.ProcessAssembly` (IJniNameProvider) | Marking | During MarkStep | TypeMap unconditional attr | Already handled - these ARE the component attrs |
| `MarkJavaObjects.ProcessType` | Marking | During MarkStep | Proxy class refs | Proxy refs activation ctor → automatic |
| `PreserveJavaInterfaces` | Marking | During MarkStep | Proxy class refs | Proxy marshal methods call interface methods |
| `PreserveRegistrations` | Marking | During MarkStep | Proxy class refs | Proxy uses GetFunctionPointer/calls handler |
| `PreserveApplications` | Marking | During MarkStep | TypeMapAssociationAttr | BackupAgent/ManageSpaceActivity cross-ref |
| `PreserveJavaExceptions` | Marking | During MarkStep | Proxy class refs | Proxy calls string ctor |
| `PreserveExportedTypes` | Marking | During MarkStep | TypeMap unconditional attr | Generator collects [Export]/[ExportField] |
| **Non-Preservation Steps:** | | | | |
| `FixAbstractMethodsStep` | IL Patching | During MarkStep | Likely unnecessary | Legacy compat (2017), start WITHOUT |
| `AddKeepAlivesStep` | IL Patching | After CleanStep | Likely unnecessary | Legacy compat, modern SDK has KeepAlive |
| `StripEmbeddedLibraries` | IL Patching | After CleanStep | Unrelated - post-trimming | Remove embedded jars/zips |
| `GenerateProguardConfiguration` | CodeGen | After CleanStep | Unrelated - post-trimming | Scan surviving types, generate -keep rules |
| `RemoveResourceDesignerStep` | IL Patching | After CleanStep | Unrelated - not trimming | Resource.designer.cs optimization |
| `GetAssembliesStep` | Utility | After CleanStep | Unrelated - not trimming | Support for RemoveResourceDesignerStep |
| `FixLegacyResourceDesignerStep` | IL Patching | Before MarkStep | Unrelated - not trimming | Legacy designer fixup |

**Step Types:**
- **Marking**: Calls `Annotations.Mark()` or `AddPreservedMethod()` to influence what survives trimming
- **IL Patching**: Modifies method bodies, adds/removes types or resources
- **CodeGen**: Generates new code or configuration files
- **Utility**: Helper step that collects data for other steps

**Trimmer Phases:**
- **Before MarkStep**: Assembly scanning/modification before the trimmer decides what to keep
- **During MarkStep**: Runs as MarkHandler/SubStep, can mark additional types/methods
- **After CleanStep**: Runs on already-trimmed assemblies (only surviving types/methods)

**Replacement Categories:**
- **TypeMap unconditional attr**: 2-arg `TypeMapAttribute` ensures type survives trimming unconditionally
- **Proxy class refs**: Proxy type has hard code references → normal trimmer dependency tracking
- **TypeMapAssociationAttr**: Preserves associated type when primary type is activated
- **Likely unnecessary**: Legacy compatibility, start without and add back only if customers report issues
- **Unrelated - post-trimming**: Works on already-trimmed assemblies, not related to trimmable type map preservation
- **Unrelated - not trimming**: These steps handle resource optimization, not type preservation

#### 15.7.10 Why Proxy References Replace Preservation Steps

The key insight is that **proxy types create hard code references** to everything that needs preservation:

```
User code uses MainActivity
    ↓
[assembly: TypeMap("example/MainActivity", typeof(MainActivity_Proxy))]
    is UNCONDITIONAL (2-arg constructor)
    ↓
Trimmer ALWAYS preserves MainActivity_Proxy
    ↓
MainActivity_Proxy contains:
    - new MainActivity(IntPtr, JniHandleOwnership)  → preserves activation ctor
    - __this.OnCreate(bundle)                        → preserves OnCreate override
    - ((IMyInterface)obj).DoSomething()             → preserves interface method
    ↓
All required types and methods are preserved through NORMAL trimmer dependency analysis!

BackupAgent/ManageSpaceActivity types are handled differently:
    - Cross-reference analysis during scanning finds types in [Application] attribute
    - Those types get unconditional TypeMapAttribute (2-arg)
    - No proxy reference needed - the TypeMapAttribute itself ensures preservation
```

This is fundamentally different from the legacy approach where custom steps had to explicitly call `AddPreservedMethod()` during the mark phase. The trimmable type map approach leverages the trimmer's existing dependency tracking.

#### 15.7.11 Custom Views from Layout XML

**CRITICAL:** The build process extracts custom view types from Android layout XML files:

1. `ConvertCustomView` task parses layout XML files
2. Creates `customview-map.txt` with format: `TypeName;path/to/layout.xml`
3. Legacy `MarkJavaObjects` reads this file and unconditionally marks types

**Trimmable type map requirement:** The `GenerateTypeMapAssembly` task must:
1. Accept `CustomViewMapFile` as an input
2. Parse the file to get custom view type names
3. Generate **unconditional TypeMapAttribute** for each custom view

```xml
<!-- MSBuild -->
<GenerateTypeMapAssembly
    CustomViewMapFile="$(IntermediateOutputPath)customview-map.txt"
    ... />
```

```csharp
// In generator
var customViewTypes = LoadCustomViewMapFile(CustomViewMapFile);
foreach (var peer in javaPeers) {
    bool isCustomView = customViewTypes.Contains(peer.ManagedTypeName);
    bool isUnconditional = isCustomView || /* other rules */;
}
```

---

### 15.8 Type Detection Rules: Unconditional vs Trimmable

This section defines the **exact rules** for determining which types are preserved **unconditionally** vs which are **trimmable**.

#### Terminology

| Term | Meaning | TypeMapAttribute Constructor |
|------|---------|------------------------------|
| **Unconditional** | Type is ALWAYS preserved, cannot be trimmed | `TypeMapAttribute(string, Type)` |
| **Trimmable** | Type is preserved only if used by .NET code | `TypeMapAttribute(string, Type, Type)` |

#### Rule Summary

| Detection Criteria | Preservation | Reason |
|-------------------|--------------|--------|
| User type with `[Activity]`, `[Service]`, etc. attribute | **Unconditional** | Android creates these |
| User type subclassing Android component (Activity, etc.) | **Unconditional** | Android creates these |
| Custom view referenced in layout XML | **Unconditional** | Android inflates these |
| Interface with `[Register]` | **Trimmable** | Only if .NET implements/uses |
| Implementor type (ends in "Implementor") | **Trimmable** | Only if C# event is used |
| `[Register]` with `DoNotGenerateAcw = true` | **Trimmable** | MCW - only if .NET uses |
| Invoker type | **Not in TypeMap** | Share JNI name with interface |

**Key Insight:** In the legacy system, most types are only preserved if they're referenced by user code. The legacy `MarkJavaObjects` only unconditionally marks:
1. Types with `[Activity]`, `[Service]`, `[BroadcastReceiver]`, `[ContentProvider]`, `[Application]`, `[Instrumentation]` attributes
2. Custom views from layout XML files
3. Custom HttpMessageHandler from settings

All other types (interfaces, MCWs, Implementors) are only preserved if user code references them.

#### Detailed Detection Rules

##### Rule 1: User-Defined Android Component Types → Unconditional

Types with these attributes are marked UNCONDITIONALLY. This is the **exhaustive list** of attributes that trigger unconditional preservation:

| Attribute (Full Name) | Short Form | Base Class | Why Unconditional |
|-----------------------|------------|------------|-------------------|
| `Android.App.ActivityAttribute` | `[Activity]` | `Android.App.Activity` | Android creates via Intent navigation |
| `Android.App.ApplicationAttribute` | `[Application]` | `Android.App.Application` | Android creates at app startup |
| `Android.App.ServiceAttribute` | `[Service]` | `Android.App.Service` | Android creates on startService/bindService |
| `Android.Content.BroadcastReceiverAttribute` | `[BroadcastReceiver]` | `Android.Content.BroadcastReceiver` | Android creates on broadcast |
| `Android.Content.ContentProviderAttribute` | `[ContentProvider]` | `Android.Content.ContentProvider` | Android creates on first query |
| `Android.App.InstrumentationAttribute` | `[Instrumentation]` | `Android.App.Instrumentation` | Test runner creates |

**Note:** `Android.Runtime.RegisterAttribute` is NOT in this list. While it implements `IJniNameProviderAttribute`, it's present on ALL Java peer types and doesn't by itself make a type unconditional.

```csharp
// UNCONDITIONAL - has [Activity] attribute
[Activity(Label = "My App", MainLauncher = true)]
public class MainActivity : Activity { }
```

##### Rule 2: Custom Views from Layout XML → Unconditional

Types referenced in Android layout XML files are marked UNCONDITIONALLY:

```xml
<!-- layout.xml -->
<com.example.MyCustomButton
    android:layout_width="wrap_content"
    android:layout_height="wrap_content" />
```

```csharp
// UNCONDITIONAL - referenced in layout XML
[Register("com/example/MyCustomButton")]
public class MyCustomButton : Button { }
```

**Trimmable type map requirement:** The TypeMap generator must process the custom view map file and generate unconditional entries.

##### Rule 3: Interfaces → TRIMMABLE

Java interfaces are **trimmable** - only preserved if .NET code uses them:

```csharp
// TRIMMABLE - interfaces are MCW bindings for existing Java interfaces
[Register("org/xml/sax/ContentHandler", "", "Org.Xml.Sax.IContentHandlerInvoker")]
public interface IContentHandler : IJavaObject { }

// Only preserved if user code:
// - Implements IContentHandler
// - Uses a type that implements IContentHandler
// - Calls a method that takes/returns IContentHandler
```

**Reasoning:** Interfaces don't have `DoNotGenerateAcw=true` in their `[Register]` attribute, but they are still MCW bindings. Android never creates interface instances directly - they're always created from .NET code.

##### Rule 4: Implementor Types → TRIMMABLE

Types ending in "Implementor" are helper classes for C# events. They are **trimmable**:

```csharp
// TRIMMABLE - only needed if C# event is used
[Register("mono/android/view/View_OnClickListenerImplementor")]
internal class OnClickListenerImplementor : Java.Lang.Object, View.IOnClickListener { }
```

These are created from .NET code when subscribing to events:
```csharp
button.Click += (s, e) => { };  // Creates OnClickListenerImplementor
```

If no code uses the event, the Implementor is trimmed away.

##### Rule 5: Managed Callable Wrapper (MCW) Types → TRIMMABLE

Types with `DoNotGenerateAcw = true` are **trimmable**:

#### Detection Algorithm

```csharp
PreservationMode DeterminePreservation(TypeDefinition type)
{
    var registerAttr = type.GetCustomAttribute("Android.Runtime.RegisterAttribute");
    if (registerAttr == null)
        return PreservationMode.None;  // No TypeMap needed

    // Check DoNotGenerateAcw property
    bool doNotGenerateAcw = registerAttr.GetProperty<bool>("DoNotGenerateAcw");

    if (doNotGenerateAcw)
    {
        // MCW type (binding for existing Java class)
        // Only preserve if .NET code actually uses it
        return PreservationMode.Trimmable;
    }

    // JCW type (user's .NET type with Java wrapper)
    // Always preserve because Android/Java may create at any time
    return PreservationMode.Unconditional;
}
```

#### Summary Table

| Type | `DoNotGenerateAcw` | Inherits From | Preservation | Example |
|------|-------------------|---------------|--------------|---------|
| User Activity | `false`/unset | `Activity` | **Unconditional** | `MainActivity` |
| User Service | `false`/unset | `Service` | **Unconditional** | `MyBackgroundService` |
| User Receiver | `false`/unset | `BroadcastReceiver` | **Unconditional** | `MyReceiver` |
| User Exception | `false`/unset | `Java.Lang.Throwable` | **Unconditional** | `MyCustomException` |
| User Interface | `false`/unset | `IJavaObject` | **Unconditional** | `IMyCallback` |
| User Java.Lang.Object | `false`/unset | `Java.Lang.Object` | **Unconditional** | `MyJavaObject` |
| SDK Activity | `true` | `Activity` | **Trimmable** | `Activity` binding |
| SDK View | `true` | `View` | **Trimmable** | `TextView`, `Button` |
| SDK Exception | `true` | `Java.Lang.Throwable` | **Trimmable** | `Java.Lang.Exception` |

#### Edge Cases

##### Nested Types
Nested types follow the same rules as their enclosing type:
```csharp
public class Outer : Java.Lang.Object {
    [Register("com/example/Outer$Inner")]
    public class Inner : Java.Lang.Object { }  // Unconditional
}
```

##### Generic Types
Generic types with Java bindings are preserved unconditionally if the open generic has `[Register]`:
```csharp
[Register("com/example/GenericClass")]
public class GenericClass<T> : Java.Lang.Object { }  // Unconditional for each instantiation
```

##### Types Referenced by Application Attributes
Types referenced in `[Application]` attribute properties are automatically unconditional:
```csharp
[Application(BackupAgent = typeof(MyBackupAgent))]
public class MyApp : Application { }

// MyBackupAgent is preserved unconditionally because it's referenced in [Application]
```

---

### 15.8 Proxy Type Generation

The proxy types must contain direct references to all methods/constructors that need preservation.

#### Proxy References Eliminate Need for DynamicDependency

The proxy types naturally reference everything that needs preservation:

```csharp
// Exception proxy references string ctor
class MyException_Proxy {
    public static MyException CreateWithMessage(string message) {
        return new MyException(message);  // Direct reference!
    }
}

// Interface proxy references all interface methods
class IMyInterface_Proxy {
    public static void n_DoSomething(IntPtr jnienv, IntPtr native__this) {
        ((IMyInterface)obj).DoSomething();  // Direct reference!
    }
}
```

#### BackupAgent/ManageSpaceActivity Preservation

BackupAgent and ManageSpaceActivity types referenced in `[Application]` attributes are preserved via **cross-reference analysis**, not via proxy references:

1. During scanning, we find `[Application(BackupAgent = typeof(MyBackupAgent))]`
2. We add `MyBackupAgent` to a "forced unconditional" set
3. When generating TypeMapAttribute for `MyBackupAgent`, we use the 2-arg (unconditional) variant

This is cleaner than embedding `typeof(MyBackupAgent)` in the Application proxy because:
- Each type gets exactly ONE TypeMapAttribute
- The analysis is done during the scanning phase (no code generation dependency)
- It's clear and explicit in the generated metadata

#### ILLink Step Replacements

| ILLink Step | Replacement |
|-------------|-------------|
| `PreserveApplications` | Cross-reference analysis: BackupAgent/ManageSpaceActivity types from [Application] get unconditional TypeMapAttribute |
| `PreserveJavaExceptions` | Proxy refs string ctor directly - exception types get unconditional TypeMap |
| `PreserveJavaInterfaces` | Proxy refs all interface methods directly - no separate preservation needed |
| `MarkJavaObjects` | ✅ Already done (proxy refs activation ctor) |
| `PreserveRegistrations` | ✅ Already done (unconditional TypeMap to proxy) |

#### Decision: unconditional vs trimmable

The actual logic implemented in the generator:

```csharp
bool isImplementor = peer.ManagedTypeName.EndsWith("Implementor", StringComparison.Ordinal);
bool isCustomView = _customViewTypes.Contains(peer.ManagedTypeName);
bool isUnconditional = isCustomView || (!peer.DoNotGenerateAcw && !peer.IsInterface && !isImplementor);
```

Decision tree:
```
Is this type a custom view from layout XML?
└─ YES → unconditional - Android inflates views from XML

Is this type an interface?
└─ YES → trimmable - Android never instantiates interfaces directly

Is this type an Implementor (name ends with "Implementor")?
└─ YES → trimmable - Only needed if interface is used from .NET

Does the type have DoNotGenerateAcw = true?
└─ YES → trimmable (MCW) - Only if .NET code uses the Java class
└─ NO → unconditional (JCW) - Java code might instantiate it

Is this type referenced in [Application] BackupAgent/ManageSpaceActivity?
└─ YES → TypeMapAssociationAttribute preserves it when Application is activated ✅
```

#### Benefits

1. **Cross-runtime** - Works with both ILLink and ILC
2. **Declarative** - Preservation is expressed in metadata, not imperative code
3. **No DynamicDependency needed** - Proxy direct references do the work
4. **No custom ILLink steps** - Can be deleted entirely
5. **NativeAOT compatible** - Enables skipping ILLink for NativeAOT builds

### 15.9 Non-Preservation ILLink Steps

These steps are NOT related to type/method preservation and are NOT replaced by trimmable type map:

#### 15.9.1 AddKeepAlivesStep

**Purpose:** Adds `GC.KeepAlive(parameter)` calls at the end of methods with `[Register]` attribute.

**Why it exists:** When a .NET method passes a Java object's `Handle` (IntPtr) to JNI, the GC might collect the wrapper object before the JNI call completes. `KeepAlive` prevents this.

**Where KeepAlive is needed:**
```csharp
// MCW method (outgoing call from .NET to Java)
public void DoSomething(View host) {
    __args[0] = new JniArgumentValue(host.Handle);  // Only IntPtr kept
    _members.InstanceMethods.InvokeVirtualVoidMethod(__id, this, __args);
} finally {
    GC.KeepAlive(host);  // Prevents GC during JNI call
}
```

**Where KeepAlive is NOT needed:**
```csharp
// Native callback (incoming call from Java to .NET)
static void n_OnClick(IntPtr jnienv, IntPtr native__this, IntPtr native_v) {
    var __this = GetObject<IOnClickListener>(native__this);
    var v = GetObject<View>(native_v);
    __this.OnClick(v);
    // No KeepAlive needed - Java holds the reference
}
```

**Modern .NET for Android:** The MCW generator (`Java.Interop.Tools.Generator`) already emits `GC.KeepAlive` calls:
- Location: `tools/generator/SourceWriters/Extensions/SourceWriterExtensions.cs`
- Method: `Parameter.ShouldGenerateKeepAlive()` determines which parameters need KeepAlive
- Skips: primitives, enums, strings

**When AddKeepAlivesStep runs:**
```csharp
// Only for legacy assemblies NOT built against .NET for Android
if (MonoAndroidHelper.IsDotNetAndroidAssembly(assembly))
    return false;  // Already has KeepAlive compiled in
```

**Customer fix:** Rebuild the library with the current .NET for Android SDK. The MCW generator will automatically include `GC.KeepAlive` calls.

**Trimmable type map strategy:** Start WITHOUT this step. If customers report issues with legacy libraries, implement as a separate post-restore assembly rewriting step (outside of ILLink).

#### 15.9.2 FixAbstractMethodsStep

**Purpose:** Fixes C#/Java impedance mismatch for interface versioning.

**The problem:**
- Java allows adding methods to interfaces without breaking existing implementations (throws `AbstractMethodError` at runtime)
- C# requires all interface methods to be implemented (throws `TypeLoadException` at type load)

**Scenario:**
1. Library built against API-10 implements `ICursor` with API-10 methods
2. App targets API-11+ where `ICursor` has new methods (e.g., `GetType()`)
3. Without this step: `TypeLoadException` when type loads
4. With this step: Missing methods are injected with `throw new Java.Lang.AbstractMethodError()`

**History:** Introduced in Xamarin.Android 6.1 (2017) for binary compatibility with older libraries.

**Is it still relevant?**
- Only affects libraries built against older `$(TargetFrameworkVersion)` used in newer apps
- C# now has Default Interface Methods (DIM) which could reduce the need
- Modern bindings may use DIM for new interface members
- The step already implements `IAssemblyModifierPipelineStep` for use outside ILLink

**Trimmable type map strategy:** Start WITHOUT this step. This is a legacy compatibility feature from 9+ years ago. If customers report `TypeLoadException` with older libraries:
1. First recommendation: Rebuild the library with current SDK
2. If not possible: Implement as a separate post-restore assembly rewriting step (outside of ILLink)

The step can be added back as needed - it's IL modification, not preservation, so it's independent of trimmable type map.

#### 15.9.3 StripEmbeddedLibraries

**Purpose:** Removes embedded native libraries (.so files) from assemblies after they've been extracted.

**Trimmable type map impact:** Still needed. Can be converted to MSBuild task.

#### 15.9.4 GenerateProguardConfiguration

**Purpose:** Generates ProGuard/R8 configuration to keep Java classes that have .NET bindings.

**Trimmable type map plan:** Convert to post-trimming MSBuild task that:
1. Scans trimmed assemblies for surviving types with `[Register]`
2. Generates `-keep class` rules only for surviving types
3. Also generates list of `.o` files to link (same surviving types list)

See section 15.3 "Post-Trimming Filtering" for details.

#### 15.9.5 RemoveResourceDesignerStep / FixLegacyResourceDesignerStep

**Purpose:** Handles Resource.designer.cs optimization and legacy compatibility.

**Trimmable type map impact:** Still needed. Not related to TypeMap.

---

## 16. Error Handling

### 16.1 Build-Time Errors

| Error Code | Description |
|------------|-------------|
| XA4212 | Type implements IJavaObject but doesn't extend Java.Lang.Object |
| XA4xxx | No activation constructor found in type hierarchy |

### 16.2 Runtime Errors

| Scenario | Behavior |
|----------|----------|
| Unknown Java class name | `GetFunctionPointer` returns `IntPtr.Zero` |
| Invalid method index | `GetFunctionPointer` returns `IntPtr.Zero` |
| Interface `GetFunctionPointer` | Throws `NotSupportedException` |
| No activation ctor | `CreateInstance` throws `MissingMethodException` |

---

## 17. Implementation Checklist

### 17.1 Core Infrastructure

- [ ] MSBuild task: `GenerateTypeMaps`
- [ ] Assembly scanning for Java peers
- [ ] TypeMap attribute generation
- [ ] Proxy type generation with UCO methods
- [ ] GetFunctionPointer switch statements
- [ ] CreateInstance factory methods

### 17.2 Constructor Handling

- [x] IgnoresAccessChecksTo for protected/base constructors (replaced UnsafeAccessor)
- [ ] XI + JI constructor support
- [ ] Constructor search up hierarchy

### 17.3 Native Integration

- [ ] LLVM IR stub generation
- [ ] JCW Java file generation
- [ ] Implementor JCW + UCO generation
- [ ] Function pointer callback wiring

### 17.4 Performance

- [ ] SDK type pre-generation and caching
- [ ] NuGet package type caching
- [ ] Parallel assembly scanning

### 17.5 Validation

- [ ] Full trimming validation (`TrimMode=full`)
- [ ] Performance benchmarks
- [ ] Test suite

---

## 18. File Locations

### 18.1 Build-Time Components

| Component | Location |
|-----------|----------|
| TypeMaps generator task | `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMaps.cs` |
| Build targets | `src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/` |

### 18.2 Runtime Components

| Component | Location |
|-----------|----------|
| ITypeMap | `src/Mono.Android/Java.Interop/ITypeMap.cs` |
| TypeMapAttributeTypeMap | `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` |
| LlvmIrTypeMap | `src/Mono.Android/Java.Interop/LlvmIrTypeMap.cs` |
| JavaPeerProxy | `src/Mono.Android/Java.Interop/JavaPeerProxy.cs` |
| RuntimeFeature | `src/Mono.Android/Microsoft.Android.Runtime/RuntimeFeature.cs` |

---

## Appendix A: Learnings from Original Designs

This appendix documents design decisions that were revised during prototyping and explains why the changes were necessary.

### A.1 Invokers in TypeMap

**Original Design:** Generate proxy types for Invoker classes (e.g., `IOnClickListenerInvoker`) with `NotSupportedException` in `GetFunctionPointer`.

**What Happened:** Invokers share the same JNI name as their corresponding interfaces (`DoNotGenerateAcw=true`). Including them in the TypeMap created thousands of unnecessary alias entries (3,506 instead of 60 for a simple app), generating ~20% more proxy types than needed.

**Resolution:** Exclude Invokers from the TypeMap entirely. They are only instantiated by the interface proxy's `CreateInstance` method, which directly calls `new InvokerType(handle, transfer)`. No `GetFunctionPointer` calls ever target them.

**Impact:** 20% reduction in generated proxy count (8,811 → 7,067 for HelloWorld sample).

### A.2 Method Index Synchronization

**Original Design:** The spec described method indices but did not specify an explicit ordering contract between IL generation and LLVM IR generation.

**What Happened:** The LLVM IR stubs were generated with regular methods first (indices 0..n-1) and activation constructors second (n..m-1). The C# `GetFunctionPointer` switch was generated in the opposite order (activation first). This caused `onCreate` (index 0) to return the activation constructor's function pointer, resulting in SIGSEGV crashes.

**Resolution:** Establish an explicit ordering contract that both generators must follow:
1. Regular marshal methods: indices 0 to n-1
2. Activation constructors: indices n to m-1

Both generators must enumerate methods in identical order.

**Impact:** Critical fix - the system was non-functional without this.

### A.3 Implementor Callback Routing

**Original Design:** Mentioned that Implementors would call Invoker's `n_*` methods, but lacked implementation details.

**What Happened:** Implementors don't define `n_*` callbacks - those are defined in the Invoker class. The first implementation tried to call non-existent methods on the Implementor type.

**Resolution:** The UCO wrapper for an Implementor must extract the callback method reference from the `[Register]` connector string and call the Invoker's static method:

```csharp
// Connector string: "GetOnClickHandler:Android.Views.View/IOnClickListenerInvoker, Mono.Android"
// UCO calls: IOnClickListenerInvoker.n_OnClick_Landroid_view_View_(...)
```

**Impact:** Required for interface event handlers (e.g., `button.Click +=`) to work.

### A.4 Assembly Writing Library

**Original Design:** Did not specify which library to use for generating the TypeMapAssembly.dll.

**What Happened:** Initial exploration considered Mono.Cecil, but `System.Reflection.Metadata.Ecma335` (S.R.M.E) proved to be the right choice:
- No external dependency (part of BCL)
- Lower-level but sufficient for code generation needs
- Avoids version conflicts with other Cecil uses in the build

**Resolution:** Use S.R.M.E for TypeMapAssembly.dll generation. Required learning curve for manual encoding of:
- Exception handlers (`ExceptionRegionEncoder`)
- `[UnmanagedCallersOnly]` attribute encoding
- Control flow with `ControlFlowBuilder`

### A.5 ILLink Step vs MSBuild Task

**Original Design:** Prototype used an ILLink custom step running before MarkStep.

**What Happened:** The ILLink step approach works but has limitations:
- Runs late in build pipeline, limiting incremental build optimizations
- Cannot easily cache SDK type maps separately
- Harder to debug than a standalone MSBuild task
- Tight coupling with linker internals

**Resolution:** Production implementation should be a standalone MSBuild task running before ILLink. Benefits:
- Better incremental build support
- SDK type pre-generation and caching
- Cleaner separation of concerns
- Easier testing and debugging

### A.6 Export Attribute Support

**Original Design:** Listed `[Export]` as out of scope / future work, with options including deprecation or reflection fallback.

**What Happened:** During implementation, it became clear that `[Export]` methods can be handled identically to `[Register]` methods:
- Detect `[Export]` attribute on methods
- Derive JNI name from `Export.Name` or method name
- Derive JNI signature from .NET parameter/return types
- Generate UCO wrapper and LLVM stub same as `[Register]`

**Resolution:** Support `[Export]` with static codegen. This eliminates the need for `Mono.Android.Export.dll` and runtime reflection-based registration.

### A.7 Dynamic Registration Isolation

**Original Design:** Unclear separation between static and dynamic registration paths.

**What Happened:** `[Export]` handling and other dynamic registration code was interleaved with static type map code, causing trimmer warnings and AOT compatibility issues.

**Resolution:** Isolate all reflection-based code in `DynamicNativeMembersRegistration` class with:
- `[RequiresUnreferencedCode]` attribute
- Feature switch guard (`IsCoreClrRuntime` returns immediately)
- Clear boundary between static (CoreCLR/NativeAOT) and dynamic (Mono) paths

### A.8 Performance Optimizations Discovered

**Original Design:** Sequential assembly scanning; basic caching.

**What Happened:** Initial implementation was slow for large apps with many SDK types.

**Resolution:**
- Parallel assembly scanning: 6x faster TypeMap generation
- Two-layer caching: native (LLVM IR globals) + managed (Dictionary with Lock)
- Exclude Invokers: 20% fewer proxies to generate

### A.9 ILLink TypeMap Intrinsic Requirements

**Original Design:** Use `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic for AOT-safe type map lookup.

**What Happened:** This intrinsic requires ILLink to:
1. Recognize the `--typemap-entry-assembly` command-line flag
2. Replace the intrinsic call with inline code that builds a dictionary from `TypeMapAttribute` entries
3. Without this flag, ILLink leaves the call unchanged
4. At runtime, the fallback `TypeMapLazyDictionary` tries to scan assemblies
5. With trimming enabled, required types may be removed → crash

**Resolution:** This feature requires .NET runtime with PR dotnet/runtime#121513 merged. The PR adds:
- `--typemap-entry-assembly NAME` flag to ILLink
- MSBuild target that passes `$(TypeMapEntryAssembly)` to ILLink
- Runtime support for the `TypeMappingEntryAssembly` runtimeconfig property

**Impact:** Critical dependency - TypeMap API v3 cannot function without this ILLink feature.

### A.10 Interface Split Optimization (DoNotGenerateAcw)

**Original Design:** Generate UCO methods and `GetFunctionPointer` switch for ALL proxy types.

**What Happened:** Analysis revealed:
- ~95% of types are **MCW types** (Managed Callable Wrappers) with `DoNotGenerateAcw = true`
- These types wrap existing Java classes - Java never calls back into them
- Only ~5% are **ACW types** that override Java methods and need callbacks
- Generating UCO stubs and LLVM IR for all types is wasteful

**Resolution:** Split `IJavaPeerProxy` interface:

```csharp
// For ALL Java peer types - creates managed wrapper from Java reference
public interface IJavaPeerProxy
{
    IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}

// ONLY for types that generate ACW (override Java methods)
public interface IAndroidCallableWrapper
{
    IntPtr GetFunctionPointer(int methodIndex);
}
```

Decision logic:
- `DoNotGenerateAcw = true` → Implement only `IJavaPeerProxy` (no UCO, no LLVM IR)
- `DoNotGenerateAcw = false` → Implement both interfaces (full UCO + LLVM IR)

**Impact:**
- ~95% fewer UCO methods generated
- ~95% less LLVM IR code
- Faster codegen and smaller binaries
- Runtime only checks for callbacks on types that actually have them

### A.11 Callback Type Resolution for Base Class Methods

**Original Design:** The spec mentioned walking up the base class chain to find `[Register]` attributes but didn't specify how to determine the callback type.

**What Happened:** The `[Register]` connector string for methods in the same class doesn't include type information:
```
GetOnCreate_Landroid_os_Bundle_Handler    // No ":Type, Assembly" suffix
```

When `ParseConnectorType()` returned `(null, null)`, the code fell back to the user type (`MainActivity`) instead of the base type (`Activity`) where the callback is defined.

**Resolution:** When finding a Register attribute during base class chain walk, use that base class as the callback type:
```csharp
var (callbackTypeName, callbackAssemblyName) = ParseConnectorType(connector);
if (callbackTypeName == null) {
    callbackTypeName = baseTypeName;
    callbackAssemblyName = baseAssemblyName;
}
```

**Impact:** Critical fix - without this, UnsafeAccessor targets the wrong type, causing `MissingMethodException` at runtime.

---

## 19. Toolchain Requirements

### 19.1 .NET SDK Version

The Type Mapping API v3 requires a .NET SDK version that includes:

| Requirement | Minimum Version | PR/Issue |
|-------------|-----------------|----------|
| ILLink `--typemap-entry-assembly` flag | .NET 11 (post PR #121513) | [dotnet/runtime#121513](https://github.com/dotnet/runtime/pull/121513) |
| `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic | .NET 11 | Same PR |
| `TypeMappingEntryAssembly` runtimeconfig property | .NET 11 | Same PR |

### 19.2 Build Configuration

The following MSBuild properties must be set:

```xml
<PropertyGroup>
  <!-- Set the TypeMap entry assembly for ILLink -->
  <TypeMapEntryAssembly>_Microsoft.Android.TypeMaps</TypeMapEntryAssembly>
</PropertyGroup>

<!-- RuntimeHostConfigurationOption for runtime to find the assembly -->
<ItemGroup>
  <RuntimeHostConfigurationOption
    Include="System.Runtime.InteropServices.TypeMappingEntryAssembly"
    Value="$(TypeMapEntryAssembly)" />
  <!-- NOTE: Do NOT set Trim="true" - this is an assembly name, not a feature switch -->
</ItemGroup>
```

### 19.3 ILLink Targets

The .NET SDK's `Microsoft.NET.ILLink.targets` automatically adds the flag:
```xml
<_ExtraTrimmerArgs>
  $(_ExtraTrimmerArgs) --typemap-entry-assembly "$(TypeMapEntryAssembly)"
</_ExtraTrimmerArgs>
```

### 19.4 Version Compatibility Matrix

| .NET for Android | .NET SDK Required | Notes |
|------------------|-------------------|-------|
| 36.x (TypeMap v3) | .NET 11 (post-#121513) | Full TypeMap API support |
| 35.x and earlier | .NET 10+ | Legacy LLVM IR TypeMap |

---

## 20. Open Issues and TODOs

### 20.1 Array Type Handling

**Status**: Designed and Implemented

#### Problem

The legacy `Array.CreateInstance(elementType, length)` method is not AOT-safe. When marshalling Java arrays to .NET arrays (e.g., `TrustManagerFactory.GetTrustManagers()` returns `ITrustManager[]`), the runtime needs to create .NET arrays without using reflection.

#### Existing Marshalling Flow

The existing binding code for array-returning methods looks like:

```csharp
// Generated MCW binding
public unsafe ITrustManager[]? GetTrustManagers()
{
    return (ITrustManager[])JNIEnv.GetArray(
        _members.InstanceMethods.InvokeNonvirtualObjectMethod(...).Handle,
        JniHandleOwnership.TransferLocalRef,
        typeof(ITrustManager));  // Element type passed here
}
```

The `JNIEnv.GetArray` flow:
1. Get array length via `JniEnvironment.Arrays.GetArrayLength()`
2. Pick converter based on element type (primitive vs reference)
3. For reference types: call `ArrayCreateInstance(elementType, length)` to allocate
4. Call `CopyArray()` to populate individual elements via `GetObjectArrayElement`

**Our change point:** `ArrayCreateInstance` - replace `Array.CreateInstance()` with AOT-safe lookup.

#### ILLink Limitation: Array Types as Trim Targets

We attempted to use `T[]` as the trim target for array TypeMap entries, but ILLink cannot resolve array types:

```
System.NotSupportedException: TypeDefinition cannot be resolved from 'Mono.Cecil.ArrayType'
```

This means we cannot have separate TypeMap entries for array types with `T[]` as the trim target.

#### Solution: Unified Proxy with CreateArray Method

Instead of separate `ArrayProxyAttribute<T>` entries, we add array creation capability directly to the existing `JavaPeerProxy` base class. Each generated proxy can create arrays of its target type by calling a static generic helper.

**JavaPeerProxy base class (in Mono.Android):**

```csharp
public abstract class JavaPeerProxy : Attribute
{
    // Existing: Create peer instances
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);

    // Create arrays of the target type
    // rank=1 for T[], rank=2 for T[][]
    public abstract Array CreateArray(int length, int rank);

    // Static generic helper - contains the actual array creation logic
    // Generated proxies call this to avoid duplicating the logic
    protected static Array CreateArrayOf<T>(int length, int rank)
    {
        return rank switch {
            1 => new T[length],
            2 => new T[length][],
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or 2"),
        };
    }
}
```

**Generated proxy classes:**

```csharp
// For concrete classes - just forwards to the static helper
internal sealed class Javax_Net_Ssl_TrustManagerFactory_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(...) { /* activation logic */ }
    public override Array CreateArray(int length, int rank)
        => CreateArrayOf<TrustManagerFactory>(length, rank);
}

// For interfaces (can't instantiate, but CAN create arrays)
internal sealed class Javax_Net_Ssl_ITrustManager_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(...) => throw new NotSupportedException();
    public override Array CreateArray(int length, int rank)
        => CreateArrayOf<ITrustManager>(length, rank);
}

// For string (special case, commonly used in T[][] APIs)
internal sealed class Java_Lang_String_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(...) { /* ... */ }
    public override Array CreateArray(int length, int rank)
        => CreateArrayOf<string>(length, rank);
}
```

#### Array Rank Support

| JNI Signature | Element Type | Call | Result |
|---------------|--------------|------|--------|
| `[Ljava/lang/String;` | `string` | `CreateArray(n, 1)` | `new string[n]` |
| `[[Ljava/lang/String;` | `string[]` | `CreateArray(n, 2)` | `new string[n][]` |

**Note:** Higher-rank arrays (T[][][], etc.) are not supported in AOT mode and will throw `ArgumentOutOfRangeException`. This is a practical limitation that covers all known Android API use cases.

#### ITypeMap Interface

```csharp
interface ITypeMap
{
    // ... existing methods ...

    // Array creation (AOT-safe)
    // rank=1 for T[], rank=2 for T[][]
    // If elementType is already an array (T[]), it's unwrapped and rank is incremented
    Array CreateArray(Type elementType, int length, int rank);
}
```

#### Runtime Array Creation

```csharp
// In TypeMapAttributeTypeMap (CoreCLR/NativeAOT path - NO reflection)
public Array CreateArray(Type elementType, int length, int rank)
{
    if (rank < 1 || rank > 2) {
        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or 2");
    }

    // Handle nested arrays: if elementType is T[], unwrap and bump rank
    if (elementType.IsArray) {
        return CreateArray(elementType.GetElementType()!, length, rank + 1);
    }

    // 1. Get JNI name for element type
    if (!TryGetJniNameForType(elementType, out var jniName)) {
        throw new InvalidOperationException($"No JNI name for {elementType}");
    }

    // 2. Look up proxy for that type (reuse existing mechanism)
    if (!_externalTypeMap.TryGetValue(jniName, out var proxyType)) {
        throw new InvalidOperationException($"No proxy registered for {jniName}");
    }

    // 3. Get cached proxy instance and call CreateArray (virtual call, no reflection)
    var proxy = GetProxyForType(proxyType);
    if (proxy == null) {
        throw new InvalidOperationException($"No proxy instance for {proxyType}");
    }

    return proxy.CreateArray(length, rank);
}

// In LlvmIrTypeMap (MonoVM legacy path - reflection OK)
public Array CreateArray(Type elementType, int length, int rank)
{
    if (rank < 1 || rank > 2) {
        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or 2");
    }

    // Unwrap nested array types
    while (elementType.IsArray) {
        elementType = elementType.GetElementType()!;
        rank++;
    }

    if (rank > 2) {
        throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or 2");
    }

    // Use reflection (OK for MonoVM)
    var arrayType = rank == 1 ? elementType : elementType.MakeArrayType();
    return Array.CreateInstance(arrayType, length);
}
```

#### JNIEnv Integration

`JNIEnv.ArrayCreateInstance` delegates to `ITypeMap.CreateArray`:

```csharp
static Array ArrayCreateInstance(Type elementType, int length)
{
    // elementType may be T or T[] (for nested arrays)
    // CreateArray unwraps array types and computes rank internally
    // External callers always use rank=1 - nested arrays are handled by unwrapping
    return JNIEnvInit.TypeMap!.CreateArray(elementType, length, rank: 1);
}
```

#### Benefits of This Design

1. **Single method** - `CreateArray(length, rank)` instead of separate `CreateArray` and `CreateArray2`
2. **Logic in base class** - The switch expression is in handwritten C#, not generated IL
3. **Simple generated IL** - Each proxy just calls `CreateArrayOf<T>(length, rank)`
4. **AOT-safe** - `new T[length]` with known T at compile time
5. **Interfaces supported** - Interface proxies can create arrays even though they can't create instances
6. **Trimmer-friendly** - When element type is preserved, its proxy is preserved

#### Trimmer Behavior

- **Used types**: If `ITrustManager` is used, its proxy and `CreateArray` method are preserved
- **Unused types**: If a type is trimmed, its proxy is trimmed, so no array creation overhead
- **No separate array entries**: No risk of array entries surviving when element type is trimmed

#### Primitive Arrays

Primitive arrays (`byte[]`, `int[]`, etc.) are handled separately through explicit converters in `NativeArrayToManaged`:
- Direct array allocation: `new byte[len]`, `new int[len]`, etc.
- Copy via JNI: `GetByteArrayElements`, `GetIntArrayElements`, etc.
- These don't require TypeMap entries as they use hardcoded allocations

#### Generator Changes

The `GenerateTypeMapAssembly` task generates `CreateArray` for each proxy:

```csharp
// For each proxy type:
void GenerateProxyType(JavaPeer peer)
{
    // ... existing CreateInstance generation ...

    // Generate CreateArray method that calls the static helper
    // public override Array CreateArray(int length, int rank)
    //     => CreateArrayOf<T>(length, rank);
    GenerateCreateArrayMethod(peer.ManagedType);
}
```

**Note:** No separate array TypeMap entries are generated. Array creation uses the element type's proxy.

### 20.2 ILLink UnsafeAccessor Constructor Marking

**Status**: Known limitation, workaround available

ILLink marks **all constructors** when it sees `[UnsafeAccessor(UnsafeAccessorKind.Constructor)]`, regardless of signature matching. This is intentional per ILLink design (Cecil resolution limitations).

**Workaround**: Use `ldftn + calli` instead of UnsafeAccessor for constructor calls:
```il
ldtoken TargetType
call Type.GetTypeFromHandle
call RuntimeHelpers.GetUninitializedObject
castclass TargetType
dup
ldarg.1  ; handle
ldarg.2  ; transfer
ldftn instance void TargetType::.ctor(IntPtr, JniHandleOwnership)
calli instance void(IntPtr, JniHandleOwnership)
ret
```

**Limitation**: `ldftn + calli` cannot bypass access checks at runtime. Protected constructors fail with `MethodAccessException`.

**Solution**: Use `IgnoresAccessChecksToAttribute` (see Section 20.6).

**Result with UnsafeAccessor**: 54 types preserved
**Result with ldftn+calli + IgnoresAccessChecksTo**: 26 types preserved (better than legacy's 37)

**Future**: Consider filing an issue with dotnet/runtime to add signature matching to `ProcessConstructorAccessor` in ILLink.

### 20.3 Callback Type Resolution for Inherited Methods

**Status**: Fixed

When a user type overrides a method from a base class (e.g., `MainActivity` overrides `Activity.OnCreate`), the `n_*` callback method is defined in the **base class**, not the user class.

**Bug Found**: The `[Register]` connector string for methods in the same class does not include type information:
```
GetOnCreate_Landroid_os_Bundle_Handler    // No ":Type, Assembly" suffix
```

When walking up the base class chain and finding this connector, `ParseConnectorType()` returned `(null, null)`, causing the code to fall back to the user type (`MainActivity`) instead of the base type (`Activity`).

**Fix**: When a Register attribute is found in a base class during hierarchy walk, use that base class as the callback type:
```csharp
var (callbackTypeName, callbackAssemblyName) = ParseConnectorType(connector);
// If connector doesn't specify type, the callback is in the base type where we found it
if (callbackTypeName == null) {
    callbackTypeName = baseTypeName;
    callbackAssemblyName = baseAssemblyName;
}
```

**Impact**: Critical fix - callbacks would fail to resolve without this.

### 20.4 Shared Callback Wrappers (Future Optimization)

**Status**: Proposed, not implemented

**Observation**: Multiple user types overriding the same base class method (e.g., `Activity.OnCreate`) all need the same callback wrapper. Currently we generate a separate wrapper for each user type.

**Current approach**:
```
MainActivity_Proxy.n_onCreate_mm_0 → calls Activity.n_OnCreate_Landroid_os_Bundle_
MyOtherActivity_Proxy.n_onCreate_mm_0 → calls Activity.n_OnCreate_Landroid_os_Bundle_
```

**Proposed optimization**:
```
Activity_Proxy.n_onCreate_mm_0 → calls Activity.n_OnCreate_Landroid_os_Bundle_
MainActivity, MyOtherActivity both reference Activity_Proxy.n_onCreate_mm_0
```

**Why this works**:
- The `n_*` callbacks are static methods that take `(jnienv, native_this, ...)`
- They use `GetObject<T>(native_this)` to get the managed instance
- They call the virtual instance method which dispatches to the correct override
- Virtual dispatch ensures the right user code runs regardless of which proxy hosts the wrapper

**Benefits**:
- Reduces code duplication - one callback wrapper per base class method
- Smaller TypeMaps assembly
- Better for trimming - fewer methods to analyze

**Implementation approaches**:
1. Generate UCO wrappers only for the declaring type (where the virtual method is first defined)
2. Derived proxy's `GetFunctionPointer` delegates to base proxy for inherited methods
3. Or: Native code references base class proxy directly

**Technical challenges**:
- Currently `GetFunctionPointer(int methodIndex)` uses indices 0, 1, 2... per proxy class
- Native LLVM IR needs to know which proxy has the wrapper and what method index
- Consistent method indices across hierarchy or delegation pattern needed

### 20.5 UnsafeAccessor for Static Method Callbacks

**Status**: Resolved - replaced with IgnoresAccessChecksTo

**Original Problem**: UCO wrappers need to call the `n_*` static callback methods defined in MCW base classes (e.g., `Activity.n_OnCreate_Landroid_os_Bundle_`). These methods may be `private` or `internal`, requiring access bypass from the TypeMaps assembly.

**Original approach**: `[UnsafeAccessor(UnsafeAccessorKind.StaticMethod)]` - but this had issues with ILLink marking and runtime resolution.

**Solution**: Use `IgnoresAccessChecksToAttribute` instead (see Section 20.6).

### 20.6 IgnoresAccessChecksTo for Cross-Assembly Access

**Status**: Implemented and working

**Problem**: The TypeMaps assembly needs to:
1. Call protected constructors (XI-style: `(IntPtr, JniHandleOwnership)`)
2. Call private/internal `n_*` callback methods in Mono.Android

Both `ldftn + calli` and direct `call`/`newobj` fail with `MethodAccessException` because the TypeMaps assembly cannot access protected/internal members of other assemblies.

**Solution**: Use `IgnoresAccessChecksToAttribute` - a CLR-recognized attribute that allows an assembly to bypass access checks when calling into another assembly.

**Implementation**:
```csharp
// Defined in TypeMaps assembly (not in BCL, but recognized by CLR)
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }
        public string AssemblyName { get; }
    }
}

// Applied at assembly level
[assembly: IgnoresAccessChecksTo("Mono.Android")]
[assembly: IgnoresAccessChecksTo("Java.Interop")]
```

**Generated IL for callbacks** (direct call, no UnsafeAccessor):
```il
// Callback wrapper for n_OnCreate
.method public hidebysig static void
    n_OnCreate_mm_0(native int jnienv, native int native__this, native int bundle)
    cil managed
{
    ldarg.0          // jnienv
    ldarg.1          // native__this
    ldarg.2          // bundle
    call void [Mono.Android]Android.App.Activity::n_OnCreate_Landroid_os_Bundle_(
        native int, native int, native int)
    ret
}
```

**Generated IL for constructors** (direct newobj):
```il
.method public hidebysig static object
    CreateInstance(native int handle, valuetype JniHandleOwnership transfer)
    cil managed
{
    ldarg.0          // handle
    ldarg.1          // transfer
    newobj instance void [Mono.Android]Android.App.Activity::.ctor(
        native int, valuetype JniHandleOwnership)
    ret
}
```

**Critical ordering requirement**: `DefineIgnoresAccessChecksToAttribute()` must be called AFTER `AddTypeReferences()` to ensure `_attributeUsageTypeRef` and `_attributeTargetsTypeRef` are initialized. Calling it before causes TypeReference row 0 (invalid) to be used in the `[AttributeUsage]` custom attribute, which crashes ILLink with IL1012.

**Advantages over UnsafeAccessor**:
1. **Better trimming**: ILLink doesn't mark extra types/constructors
2. **Simpler IL**: Direct `call`/`newobj` instead of accessor methods
3. **Runtime compatible**: Works with CoreCLR on Android
4. **No signature matching issues**: Direct calls use exact signatures

**Result**: 26 types preserved (vs 54 with UnsafeAccessor, vs 37 legacy)

---

*Document version: 1.4*
*Last updated: 2026-01-28*
