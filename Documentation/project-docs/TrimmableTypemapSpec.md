# Type Mapping API Specification for .NET Android

## 1. Overview

### 1.1 Purpose

This specification defines the architecture for enabling Java-to-.NET interoperability in .NET Android applications using the .NET Type Mapping API. The design is fully compatible with Native AOT and trimming, replacing the legacy reflection-based `TypeManager` system.

### 1.2 Goals

- **AOT-Safe**: All type instantiation and method resolution works with Native AOT
- **Trimming-Safe**: Proper annotations ensure required types survive aggressive trimming
- **Developer Experience**: No changes required to existing .NET Android application code

### 1.3 Scope

- Debug and Release builds using CoreCLR or NativeAOT runtime
- All Java peer types: user classes, SDK bindings, interfaces with invokers
- `[Register]` and `[Export]` attribute methods

**Out of scope:**
- Debug builds using Mono (continue to use existing reflection-based TypeManager and Marshal Methods)
- Non-shipping code (desktop JVM targets in java-interop repo)

### 1.4 Prerequisites

This specification requires .NET 11 SDK with [dotnet/runtime#121513](https://github.com/dotnet/runtime/pull/121513) merged. This PR adds the `--typemap-entry-assembly` ILLink flag that enables the `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic to work correctly with trimming. See [Section 20: Toolchain Requirements](#20-toolchain-requirements) for details.

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
            nctor_0();  // Constructs .NET peer
        }
    }

    @Override
    public void onCreate(android.os.Bundle savedInstanceState) {
        n_onCreate(savedInstanceState);
    }

    private native void n_onCreate(android.os.Bundle savedInstanceState);
    private native void nctor_0();
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
│  │  2. Collect marshal methods + Java-callable constructors                 │ │
│  │  3. Generate TypeMapAssembly.dll:                                     │ │
│  │     - TypeMap<T> attributes                                           │ │
│  │     - JavaPeerProxyAttribute subclasses with UCO methods                       │ │
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
│  │   2. If null: call typemap_get_function_pointer(jniName, len, idx)    ││
│  │   3. Call resolved UCO method                                          ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│           │                                                                 │
│           ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │ TrimmableTypeMap.GetFunctionPointer(jniName, methodIndex)       ││
│  │   1. Lookup type via TypeMapping.Get<Java.Lang.Object>(jniName)        ││
│  │   2. Get cached JavaPeerProxyAttribute via GetCustomAttribute                   ││
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
| `TrimmableTypeMap` | Mono.Android.dll | Runtime type lookup, function pointer resolution |
| `JavaPeerProxyAttribute` | Mono.Android.dll | Base class for generated proxies |
| LLVM IR stubs | Generated .ll | JNI entry points with caching |

### 3.3 ITypeMap Interface

The `ITypeMap` interface abstracts type mapping operations:

```csharp
interface ITypeMap
{
    // Java-to-.NET type resolution
    bool TryGetTypesForJniName(string jniSimpleReference, [NotNullWhen(true)] out IEnumerable<Type>? types);

    // .NET-to-Java type resolution
    bool TryGetJniNameForType(Type type, [NotNullWhen(true)] out string? jniName);

    // Peer instance creation
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);

    // Marshal method function pointer resolution
    IntPtr GetFunctionPointer(ReadOnlySpan<char> jniName, int methodIndex);

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
    if (RuntimeFeature.IsCoreClrRuntime || RuntimeFeature.IsNativeAotRuntime)
        return new TrimmableTypeMap();  // AOT-safe, uses generated attributes
    else if (RuntimeFeature.IsMonoRuntime)
        return new MonoTypeMap();
    else
        throw new NotSupportedException();
}
```

---

## 4. Type Classification

### 4.1 Types That Need Proxies

The following table summarizes the proxy types generated for each type category. `GetFunctionPointer` and `CreateInstance` are methods on the proxy — see §5 for the base class design and §6 for full examples. "UCO" refers to `[UnmanagedCallersOnly]` wrapper methods described in §13.

| Type Category | Example | JCW? | TypeMap Entry | GetFunctionPointer | CreateInstance |
|---------------|---------|------|---------------|-------------------|----------------|
| User class with JCW | `MainActivity` | Yes | ✅ | Returns UCO ptrs | `GetUninitializedObject` + base `.ctor(h, t)` |
| SDK MCW (binding) | `Activity` | No | ✅ | Throws | `new T(h, t)` (bindings have this ctor) |
| Interface | `IOnClickListener` | No | ✅ | Throws | `new TInvoker(h, t)` |
| Generic user class | `GenericHolder<T>` | Yes | ✅ | Returns UCO ptrs | Throws (unreachable — see §4.2) |

### 4.2 Generic Types

User-defined generic types that extend Java peers (e.g., `class GenericHolder<T> : Java.Lang.Object`) **do** get JCWs and **do** need TypeMap entries. The JCW is generated for the open generic definition — Java sees one class (e.g., `crc64.../GenericHolder_1`) regardless of the type parameter.

**What works:**
- `TryGetType("crc64.../GenericHolder_1")` → returns `typeof(GenericHolder<>)` (the open generic definition)
- `TryGetJniNameForType(typeof(GenericHolder<int>))` → returns `"crc64.../GenericHolder_1"`
- Closed generic instances created on the .NET side (e.g., `new GenericHolder<int>()`) work normally — they are registered via `ConstructPeer` and Java can call their methods

**What does NOT work:**
- `CreateInstance(handle, transfer)` — this would only be called when there is a Java instance of `crc64.../GenericHolder_1` with no corresponding .NET peer in the object map. Since the open-generic JCW cannot have a constructor callable from Java (there is no way to determine `T`), this scenario is effectively **unreachable**. The proxy's `CreateInstance` throws `NotSupportedException` as a safety measure, matching the existing behavior where `TypeManager.Activate` rejects open generic types.

**Proxy design:** The proxy for a generic type maps to the open generic definition. `CreateInstance` throws with a clear error message. `GetFunctionPointer` works normally for marshal methods (method overrides are on the open definition).

### 4.3 Types That Do NOT Need Proxies

| Type Category | Example | Reason |
|---------------|---------|--------|
| Invoker | `IOnClickListenerInvoker` | Share JNI name with interface; instantiated by interface proxy |
| `DoNotGenerateAcw` types without activation ctor | Internal helpers | No JCW, no peer creation from Java |

**Key Design Decision:** Invokers are excluded from the TypeMap because:
1. They have `DoNotGenerateAcw=true` (no JCW)
2. They share the same JNI name as their interface
3. They are only instantiated by the interface proxy's `CreateInstance` method
4. No `GetFunctionPointer` calls ever target them

---

## 5. JavaPeerProxyAttribute Design

The TypeMap assembly references types and members from other assemblies (e.g., `Mono.Android`, user assemblies) that may be `protected` or `internal`. To make this possible, the TypeMap assembly uses [`IgnoresAccessChecksToAttribute`](https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/):

```csharp
[assembly: IgnoresAccessChecksTo("Mono.Android")]
[assembly: IgnoresAccessChecksTo("UserAssembly")]
// ... one per referenced assembly with non-public types/members
```

This is an assembly-level attribute that instructs the runtime to bypass access checks when the TypeMap assembly accesses non-public members of the target assembly. It is used extensively throughout the generated code — for calling protected activation constructors (§9.3), referencing internal types, and more.

### 5.1 Base Class

The base class provides instance creation and container factory methods. Note that `GetFunctionPointer` is NOT in the base class — it's in a separate interface (see §5.2).

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
abstract class JavaPeerProxyAttribute : Attribute
{
    /// <summary>
    /// Creates an instance of the target type wrapping the given Java object.
    /// </summary>
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);

    /// <summary>
    /// Gets a factory for creating containers (arrays, lists, sets, dictionaries) of the target type.
    /// See §16 for details on JavaPeerContainerFactory.
    /// </summary>
    public abstract JavaPeerContainerFactory GetContainerFactory();
}
```

### 5.2 IAndroidCallableWrapper Interface

ACW (Android Callable Wrapper) types implement this interface:

```csharp
/// <summary>
/// Interface for proxy types that represent Android Callable Wrappers (ACW).
/// ACW types are .NET types that have a corresponding generated Java class which calls back into .NET via JNI.
/// Only types with DoNotGenerateAcw=false implement this interface.
/// </summary>
public interface IAndroidCallableWrapper
{
    /// <summary>
    /// Gets a function pointer for a marshal method at the specified index.
    /// </summary>
    IntPtr GetFunctionPointer(int methodIndex);
}
```

This interface is used to obtain function pointers for Java `native` methods. We basically reimplement the Marshal Methods design.

### 5.3 Proxy Behavior by Type Category

| Type Category | Implements `IAndroidCallableWrapper`? | `GetFunctionPointer` Behavior | `CreateInstance` Behavior |
|---------------|--------------------------------------|-------------------------------|---------------------------|
| Concrete class with JCW | ✅ Yes | Returns UCO function pointers | Creates instance of the type |
| MCW (framework binding) | ❌ No | N/A (not implemented) | Creates instance of the type |
| Interface | ❌ No | N/A (not implemented) | Creates instance of **Invoker** |

---

## 6. Type Map Attributes

> **Generation:** The `TypeMap` assembly-level attributes and all `*_Proxy` types described in this section
> are **generated at build time** by an MSBuild task that emits IL directly into a dedicated assembly
> (the "TypeMap assembly", e.g., `TypeMapAssembly.dll`). They are never hand-written or part of
> `generator` output.

### 6.1 TypeMap Attribute Structure

Each Java peer type is registered using assembly-level attributes:

```csharp
// TypeMap<TUniverse>(string jniClassName, Type proxyType, Type trimTarget)
// - jniClassName: Java class name used as lookup key
// - proxyType: The proxy type RETURNED by TypeMap lookups
// - trimTarget: Ensures trimmer preserves mapping when target is used

[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(MainActivity_Proxy), typeof(MainActivity))]
```

### 6.2 Proxy Self-Application Pattern

The proxy type applies itself as an attribute to itself:

```csharp
// TypeMap returns the proxy type
[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(MainActivity_Proxy), typeof(MainActivity))]

// Proxy applies ITSELF as an attribute to ITSELF
// ACW types also implement IAndroidCallableWrapper for GetFunctionPointer (§5.2)
[MainActivity_Proxy]  // Self-application
public sealed class MainActivity_Proxy : JavaPeerProxyAttribute, IAndroidCallableWrapper
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Uses GetUninitializedObject + base class .ctor call (see §9.3)
        var instance = (MainActivity)RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
        ((Activity)instance)..ctor(handle, transfer); // C#-pseudocode, this call must be generated directly in IL
        return instance;
    }

    public override JavaPeerContainerFactory GetContainerFactory()
        => JavaPeerContainerFactory.Create<MainActivity>();
    
    // IAndroidCallableWrapper - only on ACW types
    public IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch {
        0 => (IntPtr)(delegate* unmanaged<...>)&n_OnCreate,
        _ => IntPtr.Zero
    };
}

// At runtime:
Type proxyType = typeMap["com/example/MainActivity"];  // Returns typeof(MainActivity_Proxy)
JavaPeerProxyAttribute proxy = proxyType.GetCustomAttribute<JavaPeerProxyAttribute>();  // Returns MainActivity_Proxy instance
IJavaPeerable instance = proxy.CreateInstance(handle, transfer);  // Returns MainActivity instance
```

**Why this works:**
1. TypeMap returns the proxy type, not the target type
2. The .NET runtime's `GetCustomAttribute<T>()` instantiates attributes in an AOT-safe manner
3. The `trimTarget` parameter ensures the mapping is preserved when the target type survives trimming

### 6.3 Interface-to-Invoker Mapping

Interfaces cannot be instantiated directly. The interface proxy's `CreateInstance` directly returns an Invoker instance. Interfaces do NOT implement `IAndroidCallableWrapper` since Java never calls back into them:

```csharp
[IOnClickListener_Proxy]
public sealed class IOnClickListener_Proxy : JavaPeerProxyAttribute  // No IAndroidCallableWrapper
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new IOnClickListenerInvoker(handle, transfer); // Directly create Invoker instance - no separate lookup needed

    public override JavaPeerContainerFactory GetContainerFactory()
        => JavaPeerContainerFactory.Create<IOnClickListener>();
}
```

---

## 7. Generated Proxy Types

This section shows complete proxy examples combining the base class (§5) and attributes (§6). These examples reference `[UnmanagedCallersOnly]` wrappers (UCOs, detailed in §13), Java-callable constructors (`nctor_N`, detailed in §10), and LLVM IR stubs (§12).

### 7.1 User Class Proxy (ACW Type)

User classes that generate JCWs implement BOTH `JavaPeerProxyAttribute` AND `IAndroidCallableWrapper`:

```csharp
[MainActivity_Proxy]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class MainActivity_Proxy : JavaPeerProxyAttribute, IAndroidCallableWrapper
{
    // IAndroidCallableWrapper implementation
    public IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch
    {
        0 => (IntPtr)(delegate*<IntPtr, IntPtr, IntPtr, void>)&n_onCreate_mm_0,
        1 => (IntPtr)(delegate*<IntPtr, IntPtr, void>)&nctor_0,
        _ => IntPtr.Zero
    };

    // JavaPeerProxyAttribute implementation
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        var instance = (MainActivity)RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
        ((Activity)instance)..ctor(handle, transfer); // C#-pseudocode, generated in IL
        return instance;
    }

    public override JavaPeerContainerFactory GetContainerFactory()
        => JavaPeerContainerFactory.Create<MainActivity>();

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
    public static void nctor_0(IntPtr jnienv, IntPtr jobject)
    {
        if (JniEnvironment.WithinNewObjectScope)
            return;
        if (Java.Lang.Object.PeekObject(jobject) != null)
            return;

        var instance = (MainActivity)RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
        ((IJavaPeerable)instance).SetPeerReference(new JniObjectReference(jobject));
        CallActivationCtor(instance, jobject, JniHandleOwnership.DoNotTransfer);    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
    static extern void CallActivationCtor(MainActivity instance, IntPtr handle, JniHandleOwnership transfer);
}
```

### 7.2 MCW Type Proxy (No JCW)

MCW types (framework bindings with `DoNotGenerateAcw=true`) only implement `JavaPeerProxyAttribute`, NOT `IAndroidCallableWrapper`:

```csharp
[android_widget_TextView_Proxy]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public sealed class android_widget_TextView_Proxy : JavaPeerProxyAttribute
{
    // NO IAndroidCallableWrapper - this is an MCW type, Java never calls back into it

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new TextView(handle, transfer);

    public override JavaPeerContainerFactory GetContainerFactory()
        => JavaPeerContainerFactory.Create<TextView>();
}
```

---

## 8. Method Index Ordering Contract

### 8.1 The Problem

LLVM IR stubs (detailed in §12) call `GetFunctionPointer(jniName, methodIndex)`. Both LLVM IR and the C# `GetFunctionPointer` switch must use identical indexing.

### 8.2 The Contract

**ORDERING RULE:** Methods are indexed in the following order:

1. **Regular marshal methods** (indices 0 to n-1)
   - Enumerate in declaration order from `[Register]` and `[Export]` attributes

2. **Java-callable constructors** (indices n to m-1)
   - One per user-defined constructor that Java can invoke (`nctor_N`)

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

// Second: Java-callable constructors
foreach (var ctor in type.GetJavaCallableConstructors())
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
   a. Check BaseType for XI ctor
   b. Check BaseType for JI ctor
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
    var obj = RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
    ((Activity)obj)..ctor(handle, transfer);  // Direct call to Activity..ctor (protected), won't cause an exception (only possible to generate directly in IL)
    return obj;
}
```

**Note:** When using base class constructor, derived type field initializers do NOT run. This matches existing behavior. See §5 for details on `IgnoresAccessChecksToAttribute`.

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
    var obj = RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
    ((Activity)obj)..ctor(ref reference, JniObjectReferenceOptions.Copy);
    JNIEnv.DeleteRef(handle, transfer);  // Clean up original handle
    return obj;
}
```

**Known issue (legacy behavior):** If the constructor throws an exception, the handle leaks. The existing `TypeManager` code has the same issue. A future improvement could wrap the constructor call in try-finally to ensure cleanup:

```csharp
// Potential improvement for final implementation:
try {
    result = CreateInstanceUnsafe(ref reference, JniObjectReferenceOptions.Copy);
} finally {
    JNIEnv.DeleteRef(handle, transfer);
}
```

---

## 10. Java Callable Wrapper Constructor Generation

**IMPORTANT:** This section describes generating **Java constructors** in the JCW `.java` files. This is distinct from [Section 9: Activation Constructor Handling](#9-activation-constructor-handling) which describes **managed activation constructors** used by `CreateInstance`.

### 10.1 Overview

When Java code creates an instance of a JCW class, the Java constructor must:
1. Call the superclass constructor
2. Activate the .NET peer (call managed code to create the corresponding .NET object)

```java
// Generated Java constructor pattern:
public MainActivity () {
    super ();
    if (getClass () == MainActivity.class) {
        // Call native Java-callable constructor (index depends on ctor signature)
        nctor_0 ();
    }
}

private native void nctor_0 ();
```

### 10.2 Constructor Generation Algorithm

The Java constructor generation must match what the legacy `CecilImporter.AddConstructors()` does:

```
FOR EACH type T in hierarchy (from topmost base with DoNotGenerateAcw=false → current type):
    FOR EACH constructor C in T:
        IF C has [Export] attribute:
            Generate Java ctor with export handling
        ELSE IF C has [Register("<init>", signature, connector)] attribute:
            Generate Java ctor with that signature
        ELSE IF baseCtors is not null:
            IF C's parameters match any baseCtor:
                Generate Java ctor (inherit signature pattern)
            ELSE IF any baseCtor has no parameters:
                Generate Java ctor (use no-arg super())
```

**Key insight:** User types like `MainActivity` typically have NO `[Register]` on their constructors. The Java constructor is generated because:
1. The base type `Activity` has `[Register("<init>", "()V", ...)]` on its no-arg ctor
2. `MainActivity` has a compatible no-arg ctor (implicit or explicit)
3. Therefore, a Java constructor is generated for `MainActivity`

---

## 11. Export Attribute Support

### 11.1 Approach

Handle `[Export]` identically to `[Register]` at build time:

```csharp
[Export("myCustomMethod")]
public void MyMethod(int value) { ... }

// Treated same as:
// [Register("myCustomMethod", "(I)V", "n_myCustomMethod")]
```

### 11.2 JNI Signature Derivation

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

## 12. LLVM IR Generation

### 12.1 Per-Method Stub Template

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

### 12.2 Callback Signature

```c
void (*typemap_get_function_pointer)(
    const char16_t* jniName,    // UTF-16 Java class name (NOT null-terminated)
    int32_t jniNameLength,      // Length in char16_t units
    int32_t methodIndex,        // Index into proxy's GetFunctionPointer switch
    intptr_t* fnptr             // Out: resolved function pointer
);
```

**UTF-16 class names:** Stored as UTF-16 so they can be viewed directly as `ReadOnlySpan<char>` on the managed side without string allocation.

### 12.3 Symbol Visibility

JNI symbols MUST have `default` visibility:

```llvm
define default void @Java_...  ; NOT hidden!
```

### 12.4 File Organization

| File | Content |
|------|---------|
| `marshal_methods_{TypeName}.ll` | Per-type JNI stubs |
| `marshal_methods_init.ll` | Global `typemap_get_function_pointer` declaration |

---

## 13. UCO Wrapper Generation

### 13.1 Regular Method UCO

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

### 13.2 Java-Callable Constructor UCO

```csharp
[UnmanagedCallersOnly]
public static void nctor_{Index}(IntPtr jnienv, IntPtr jobject)
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

### 13.3 Blittable Parameter Handling

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

## 14. Alias Handling

### 14.1 When Aliases Are Needed

Multiple .NET types registered with the same Java class name, for example when two libraries define a binding for the same Java type:

```csharp
[Register("com/example/Handler")]
class HandlerA : Java.Lang.Object { }

[Register("com/example/Handler")]  // Same!
class HandlerB : Java.Lang.Object { }
```

### 14.2 Alias Holder Pattern

The typemap does not map to `JavaPeerProxyAttribute` directly in this case, but it maps to an "alias holder" class. This class has an attribute which lists all the string keys of the target proxies. This way the types corresponding to these keys can be trimmed.

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

```c#
IEnumerable<Type> ResolveAliases (Type type)
{
	var aliasesAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute> ();
	if (aliasesAttr is null) {
		yield return type;
		yield break;
	}

	foreach (var aliasKey in aliasesAttr.AliasKeys) {
		if (_externalTypeMap.TryGetValue (aliasKey, out Type? aliasedType)) {
			yield return aliasedType;
		}
	}
}
```

At the moment, we don't expect many collisons in most apps. This level of indirection for these edge cases is acceptable.

### 14.3 LLVM IR Must Use Indexed Names

JCW for `HandlerB` must look up function pointers directly for `"com/example/Handler[1]"`, NOT `"com/example/Handler"`:

```llvm
@class_name = internal constant [...] c"c\00o\00m\00/\00...\00[\001\00]\00"
```

The bracket characters `[` and `]` cannot appear in valid JNI names, guaranteeing no collisions.

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
   - Some types are TRIMMED                         - ALL .java → .class│
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

For Native AOT, we need a list of types which are included in the trimmed typemap: https://github.com/dotnet/runtime/issues/120204

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

#### Comparison Table

| Aspect | Debug | Release |
|--------|-------|---------|
| Trimming | OFF | ON |
| .ll/.o files | Link ALL | Link surviving only |
| Java classes | Compile ALL | R8 removes dead |
| ProGuard config | Not needed | Generated for surviving types |
| APK size | Larger | Optimized |
| Build speed | Faster | Slower (trimming overhead) |
| TypeMapAssembly.dll | ALL types | Trimmed |

### 15.4 Post-Trimming Filtering (Release Only)

**Problem:** We generate JCW .java files and LLVM IR .ll files for ALL types before trimming. After trimming, some .NET types are removed, but their Java classes and native stubs still exist.

**Solution:** After trimming, scan surviving assemblies and filter:

1. **Determine surviving types:** Scan trimmed assemblies for types with `[Register]` attribute still present, for ILC: https://github.com/dotnet/runtime/issues/120204
2. **Filter .o files:** Only link `marshal_methods_{TypeHash}.o` for surviving types
3. **Generate ProGuard config:** Only emit `-keep class` rules for surviving types

**Key insight:** The same "surviving types" list drives BOTH `.o` file filtering AND Proguard rule generation.

**When to skip filtering:** If `$(PublishTrimmed)` is false (Debug), skip the post-trimming filter entirely and link all .o files.

### 15.5 File Naming Convention

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

### 15.6 R8/ProGuard Configuration

R8 is used to shrink unused Java classes from the DEX file. Proper configuration is critical for achieving small DEX sizes comparable to MonoVM.

### 15.7 Incremental Build Strategy

| Type Source | Strategy |
|-------------|----------|
| SDK types (Mono.Android) | Pre-generate during SDK build, ship as artifacts |
| NuGet package types | Generate on first build, cache by package version |
| User types | Always regenerate (typically few types) |

### 15.8 TypeMap Attributes and Trimming

The trimmable type map system uses `TypeMapAttribute` to point to **proxy types**, which have direct references to the real types.

#### Key Design: TypeMapAttribute Points to PROXY Types

```csharp
// TypeMapAttribute points to the PROXY, not the original type
[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]

// The proxy has DIRECT REFERENCES to the real type's methods:
class MainActivity_Proxy {
    public static Java.Lang.Object CreateInstance(IntPtr handle, JniHandleOwnership ownership) {
        var instance = (MainActivity)RuntimeHelpers.GetUninitializedObject(typeof(MainActivity));
        ((Activity)instance)..ctor(handle, ownership);  // C#-pseudocode, generated in IL
        return instance;
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
[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]

// TRIMMABLE: TypeMapAttribute(string, Type, Type) - only if trimTarget is USED
[assembly: TypeMap<JavaObjects>("android/widget/TextView", typeof(TextView_Proxy), typeof(TextView))]
```

### 15.9 Type Detection Rules: Unconditional vs Trimmable

This section defines the **exact rules** for determining which types are preserved **unconditionally** vs which are **trimmable**.

#### Rule Summary

| Detection Criteria | Preservation | Reason |
|-------------------|--------------|--------|
| User type with `[Activity]`, `[Service]`, etc. attribute | **Unconditional** | Android creates these |
| User type subclassing Android component (Activity, etc.) | **Unconditional** | Android creates these |
| Custom view referenced in layout XML | **Unconditional** | Android inflates these |
| Interface with `[Register]` | **Trimmable** | Only if .NET implements/uses |
| Implementor type (ends in "Implementor") | **Trimmable** | Only if C# event is used |
| `[Register]` with `DoNotGenerateAcw = true` | **Trimmable** | MCW - only if .NET uses |
| Invoker type | **Not in TypeMap** | Instantiated from the corresponding interface's `JavaPeerProxyAttribute.CreateInstance` |

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

##### Rule 2b: Manual AndroidManifest.xml Component References → Unconditional (Not Yet Implemented)

Users can manually add component entries to `AndroidManifest.xml` without using the `[Activity]`, `[Service]`, etc. attributes:

```xml
<!-- AndroidManifest.xml - manually added by user -->
<activity android:name="com.example.MyManualActivity" />
<service android:name="com.example.MyBackgroundService" />
<receiver android:name="com.example.MyBroadcastReceiver" />
<provider android:name="com.example.MyContentProvider" />
```

**Current Behavior:** These types are NOT automatically discovered as roots. If the .NET type doesn't have the corresponding attribute (`[Activity]`, etc.) and isn't otherwise referenced, **it may be trimmed**, causing a runtime `ClassNotFoundException`.

**Recommended Approach:** We should scan the merged `AndroidManifest.xml` for component references (similar to how we scan layout XML for custom views) and add them to the TypeMap as unconditional entries. The scanning should look for:

- `<activity android:name="...">` → Root the .NET Activity subclass
- `<service android:name="...">` → Root the .NET Service subclass  
- `<receiver android:name="...">` → Root the .NET BroadcastReceiver subclass
- `<provider android:name="...">` → Root the .NET ContentProvider subclass
- `<application android:backupAgent="...">` → Root the BackupAgent subclass

**Implementation Note:** The `android:name` attribute can be:
1. A fully-qualified Java class name: `com.example.MainActivity`
2. A short name with package prefix: `.MainActivity` (expands to `{package}.MainActivity`)

Both forms need to be resolved to find the corresponding .NET type via the ACW map.

**Workaround (Current):** Users must either:
1. Use the proper C# attribute (`[Activity]`, `[Service]`, etc.) on their type
2. Manually add a `[DynamicallyAccessedMembers]` attribute to preserve the type
3. Add explicit trimmer roots via `TrimmerRootDescriptor`

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

#### Types Referenced by Application Attributes

BackupAgent and ManageSpaceActivity types referenced in `[Application]` attributes are preserved via **cross-reference analysis**, not via proxy references:

1. During scanning, we find `[Application(BackupAgent = typeof(MyBackupAgent))]`
2. We add `MyBackupAgent` to a "forced unconditional" set
3. When generating TypeMapAttribute for `MyBackupAgent`, we use the 2-arg (unconditional) variant

#### ILLink Step Replacements

| ILLink Step | Replacement |
|-------------|-------------|
| `PreserveApplications` | Cross-reference analysis: BackupAgent/ManageSpaceActivity types from [Application] get unconditional TypeMapAttribute |
| `PreserveJavaExceptions` | Proxy refs string ctor directly - exception types get unconditional TypeMap |
| `PreserveJavaInterfaces` | Proxy refs all interface methods directly - no separate preservation needed |
| `MarkJavaObjects` | ✅ Already done (proxy refs activation ctor) |
| `PreserveRegistrations` | ✅ Already done (unconditional TypeMap to proxy) |

#### Benefits

1. **Cross-runtime** - Works with both ILLink and ILC
2. **Declarative** - Preservation is expressed in metadata, not imperative code
3. **No DynamicDependency needed** - Proxy direct references do the work
4. **No custom ILLink steps** - Can be deleted entirely
5. **NativeAOT compatible** - Enables skipping ILLink for NativeAOT builds

---

## 16. JavaPeerContainerFactory - AOT-Safe Generic Container Creation

#### Problem

When marshaling Java collections to .NET, we need to create generic containers like `JavaList<T>`, `JavaDictionary<TKey, TValue>`, and arrays `T[]`. The traditional reflection-based approaches are not compatible with Native AOT:

```csharp
// NOT AOT-safe:
Array.CreateInstance(typeof(T), length);
Activator.CreateInstance(typeof(JavaList<>).MakeGenericType(typeof(T)));
```

#### Solution: JavaPeerContainerFactory

The `JavaPeerContainerFactory` is an abstract base class that provides factory methods for creating containers. Each `JavaPeerProxyAttribute` has a `GetContainerFactory()` method that returns a `JavaPeerContainerFactory<T>` singleton already typed to the target type.

**Key insight**: Generic instantiation like `new T[length]` or `new JavaList<T>()` requires knowing `T` at compile time. By having each proxy return a factory that is already typed to its specific `T`, these operations use direct `new` expressions which are fully AOT-safe.

#### Architecture

```
JavaPeerProxyAttribute (attribute on each bound type)
    │
    └── abstract JavaPeerContainerFactory GetContainerFactory()
                      │
                      ▼
         JavaPeerContainerFactory<T> : JavaPeerContainerFactory
                      │
         ┌────────────┼────────────┐
         ▼            ▼            ▼
    CreateArray   CreateList   CreateDictionary
    new T[len]    JavaList<T>  JavaDictionary<K,V>
```

#### API

```csharp
public abstract class JavaPeerContainerFactory
{
    // Array creation (T[], T[][], T[][][])
    internal abstract Array CreateArray(int length, int rank);

    // List creation from JNI handle
    internal abstract IList CreateList(IntPtr handle, JniHandleOwnership transfer);

    // Collection creation from JNI handle
    internal abstract ICollection CreateCollection(IntPtr handle, JniHandleOwnership transfer);

    // Dictionary creation (uses visitor pattern for two type parameters)
    internal virtual IDictionary? CreateDictionary(
        JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer);

    // Factory method
    public static JavaPeerContainerFactory Create<T>() where T : class, IJavaPeerable
        => JavaPeerContainerFactory<T>.Instance;
}
```

#### Generic Implementation

```csharp
internal sealed class JavaPeerContainerFactory<T> : JavaPeerContainerFactory 
    where T : class, IJavaPeerable
{
    internal static readonly JavaPeerContainerFactory<T> Instance = new();

    internal override Array CreateArray(int length, int rank) => rank switch {
        1 => new T[length],
        2 => new T[length][],
        3 => new T[length][][],
        _ => throw new ArgumentOutOfRangeException(...)
    };

    internal override IList CreateList(IntPtr h, JniHandleOwnership t) 
        => new JavaList<T>(h, t);
    internal override ICollection CreateCollection(IntPtr h, JniHandleOwnership t) 
        => new JavaCollection<T>(h, t);
    
    internal override IDictionary? CreateDictionary(
        JavaPeerContainerFactory keyFactory, IntPtr h, JniHandleOwnership t)
    {
        return keyFactory.CreateDictionaryWithValueFactory(this, h, t);
    }

    // Visitor pattern: called by value factory to provide both K and V types
    internal override IDictionary CreateDictionaryWithValueFactory<TValue>(
        JavaPeerContainerFactory<TValue> valueFactory, IntPtr h, JniHandleOwnership t)
        => new JavaDictionary<T, TValue>(h, t);
}
```

#### Dictionary Creation - Visitor Pattern

Dictionaries require **two** type parameters (`TKey`, `TValue`). Since each factory only knows one type, they use a **visitor pattern**:

```csharp
// To create JavaDictionary<View, Activity> from a Java Map handle:
var valueFactory = typeMap.GetProxyForType(typeof(Activity)).GetContainerFactory();
var keyFactory = typeMap.GetProxyForType(typeof(View)).GetContainerFactory();

// valueFactory (JavaPeerContainerFactory<Activity>) calls:
valueFactory.CreateDictionary(keyFactory, handle, transfer);
    // which calls: keyFactory.CreateDictionaryWithValueFactory(this, handle, transfer)
    // which creates: new JavaDictionary<View, Activity>(handle, transfer)
```

The visitor pattern allows both type parameters to be known at compile time within the generic method, enabling AOT-safe instantiation.

#### PoC Usage: TypeMapAttributeTypeMap.CreateArray

Arrays are created when marshaling Java arrays to .NET (e.g., `ITrustManager[]` during SSL handshake):

```csharp
// In TypeMapAttributeTypeMap.cs
public Array CreateArray(Type elementType, int length, int rank)
{
    if (!TryGetJniNameForType(elementType, out string? jniName))
        throw new InvalidOperationException($"No JNI name for {elementType}");
    
    if (!_externalTypeMap.TryGetValue(jniName, out Type? proxyType))
        throw new InvalidOperationException($"No proxy registered for {jniName}");
    
    var proxy = GetProxyForType(proxyType);
    return proxy.GetContainerFactory().CreateArray(length, rank);  // AOT-safe!
}
```

This is called from `JNIEnv.ArrayCreateInstance()` which is used by the `CreateNativeArrayToManaged` converters.

#### PoC Usage: JavaConvert Generic Collection Marshalling

`JavaConvert.cs` uses the factory for marshaling `IList<T>`, `ICollection<T>`, and `IDictionary<K,V>`:

```csharp
// In JavaConvert.cs - TryCreateGenericListConverter
static Func<IntPtr, JniHandleOwnership, object?>? TryCreateGenericListConverter(Type listType)
{
    var elementType = listType.GetGenericArguments()[0];
    
    // For primitives and strings, use explicit typed converters
    if (elementType == typeof(string))
        return (h, t) => JavaList<string>.FromJniHandle(h, t);
    if (elementType == typeof(int))
        return (h, t) => JavaList<int>.FromJniHandle(h, t);
    // ... other primitives ...
    
    // For Java peer types, use the factory pattern
    if (typeof(IJavaPeerable).IsAssignableFrom(elementType)) {
        var proxy = JNIEnvInit.TypeMap?.GetProxyForType(elementType);
        if (proxy == null) return null;
        
        var factory = proxy.GetContainerFactory();
        return (h, t) => factory.CreateList(h, t);  // AOT-safe!
    }
    
    return null;
}
```

The same pattern is used for:
- `TryCreateGenericCollectionConverter` → `factory.CreateCollection(h, t)`
- `TryCreateGenericDictionaryConverter` → `valueFactory.CreateDictionary(keyFactory, h, t)`

**Why primitives need explicit converters**: Primitive types (`int`, `string`, `bool`, etc.) don't have proxies in the TypeMap because they're not Java peer types. They use statically-typed `JavaList<string>.FromJniHandle()` calls which are already AOT-safe.

#### Supported Container Types

| Container | Method | Result |
|-----------|--------|--------|
| Array | `CreateArray(10, 1)` | `new T[10]` |
| 2D Array | `CreateArray(10, 2)` | `new T[10][]` |
| 3D Array | `CreateArray(10, 3)` | `new T[10][][]` |
| List | `CreateList(h, t)` | `new JavaList<T>(h, t)` |
| Collection | `CreateCollection(h, t)` | `new JavaCollection<T>(h, t)` |
| Dictionary | `CreateDictionary(keyFactory, h, t)` | `new JavaDictionary<K, V>(h, t)` |

#### Benefits

1. **AOT-safe**: Direct `new` expressions with compile-time known types
2. **Trimmer-safe**: When element type is preserved, its proxy and factory are preserved
3. **No reflection**: All container creation uses direct instantiation
4. **Unified pattern**: Same factory handles arrays, lists, collections, dictionaries
5. **Efficient**: Singleton factories, no allocations per call
6. **Type-safe**: Generic constraints ensure only valid peer types are used


## 17. Native AOT Specifics

This section covers the unique considerations when targeting Native AOT compilation for Android. While the TypeMap architecture is designed to be AOT-compatible from the ground up, Native AOT introduces additional constraints and opportunities beyond standard ILLink-based trimming.

### 17.1 Why Native AOT is Different

Native AOT uses the ILC (IL Compiler) which performs whole-program analysis and compilation to native code. Key differences from MonoVM/CoreCLR with ILLink:

| Aspect | ILLink + MonoVM | Native AOT (ILC) |
|--------|-----------------|------------------|
| Trimming | ILLink removes unused IL | ILC does whole-program trimming |
| JIT | MonoVM JITs remaining IL | No JIT - everything AOT compiled |
| Reflection | Partially supported | Severely restricted |
| Dynamic code | `MakeGenericType` works at runtime | Must be statically known |
| Binary output | Trimmed IL assemblies + runtime | Single native shared library |

**Key Insight:** ILLink is disabled for Native AOT builds because ILC performs its own trimming. The TypeMap assembly is still generated, but ILC's trimmer processes it instead of ILLink.

### 17.2 Forbidden Patterns

The following patterns work with MonoVM but fail with Native AOT:

```csharp
// ❌ Dynamic generic instantiation
var listType = typeof(JavaList<>).MakeGenericType(elementType);
var list = Activator.CreateInstance(listType, handle, transfer);

// ❌ Array.CreateInstance
var array = Array.CreateInstance(elementType, length);

// ❌ Reflection-based activation
var instance = Activator.CreateInstance(type, handle, transfer);
```

**Why these fail:** ILC must know all types at compile time. Dynamic type construction cannot be resolved statically.

**Solution:** The `JavaPeerProxyAttribute` and `JavaPeerContainerFactory` patterns replace all dynamic instantiation with statically-typed factory methods that ILC can analyze.

### 17.3 JNI Callback Implementation

With MonoVM, JNI callbacks can use managed-to-native trampolines. Native AOT requires explicit `UnmanagedCallersOnly` methods:

```csharp
// Native AOT JNI callback pattern
[UnmanagedCallersOnly]
static void n_OnCreate(IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
{
    var __this = Java.Lang.Object.GetObject<MainActivity>(jnienv, native__this, JniHandleOwnership.DoNotTransfer);
    var savedInstanceState = Java.Lang.Object.GetObject<Bundle>(jnienv, native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
    __this.OnCreate(savedInstanceState);
}
```

**LLVM IR Generation:** For Native AOT, we generate LLVM IR files (`.ll`) that define the JNI entry points. These are compiled alongside the ILC output and linked into the final shared library. This approach:
- Provides direct native-to-managed transitions via `Java_`-prefixed symbols
- Enables ILC to see and optimize the call paths

### 17.4 Symbol Export Requirements

Native AOT shared libraries use hidden visibility by default. JNI callbacks must be explicitly exported:

```llvm
; LLVM IR with explicit visibility
define void @"Java_crc64..._n_1onCreate__Landroid_os_Bundle_2"(...) #0 {
  ; ...
}

attributes #0 = { "visibility"="default" }
```

**Reasoning:** Android's ART runtime looks up JNI methods by symbol name. Hidden symbols won't be found, causing `UnsatisfiedLinkError` at runtime.

### 17.5 Crypto and TLS Integration

HTTPS/TLS support requires native crypto libraries. For Native AOT:

1. **Crypto shared library** (`libSystem.Security.Cryptography.Native.Android.so`) must be linked with `--whole-archive` to prevent symbol stripping
2. **JNI initialization** must register crypto callbacks before any TLS operations
3. **Java classes** (in `libSystem.Security.Cryptography.Native.Android.jar`) must be included in DEX compilation
4. **ProGuard rules** must preserve crypto Java classes (`net.dot.android.crypto.**`)

**Why whole-archive:** The crypto library exports JNI callbacks that are called from Java. Without `--whole-archive`, the linker sees no references from the main binary and strips them.

### 17.6 TypeMap Runtime Initialization

The TypeMap must be initialized before any Java-to-.NET transitions occur. For Native AOT:

```csharp
// During JNI_OnLoad or equivalent
JNIEnvInit.TypeMap = new TypeMapAttributeTypeMap(typeMapAssembly);
```

**Timing is critical:** If a Java callback arrives before the TypeMap is initialized, the runtime cannot resolve the target .NET type, causing a crash.

### 17.7 Build Pipeline Differences

| Phase | ILLink Build | Native AOT Build |
|-------|--------------|------------------|
| TypeMap generation | Before ILLink | Before ILC |
| Trimming | ILLink trims IL | ILC trims during compilation |
| JCW stubs | Java source files | LLVM IR files |
| Output | Trimmed DLLs + MonoVM | Single `.so` + minimal runtime |
| JNI registration | `RegisterNatives` at startup | Static exports in `.so` |

### 17.8 Debugging Native AOT Issues

Common failure patterns and diagnosis:

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| `ClassNotFoundException` | JCW not generated for nested/inner type | Check type naming, ensure outer class is processed |
| `UnsatisfiedLinkError` | Symbol not exported | Add explicit export, check visibility |
| SIGSEGV in JNI callback | TypeMap not initialized or type not found | Verify initialization order, check type registration |
| `NotSupportedException` | Invoker type trimmed | Ensure interface is preserved when abstract types are used |
| TLS handshake failure | Crypto symbols stripped | Use `--whole-archive` for crypto library |

### 17.9 Future Considerations

**External Type Mapping API (.NET 10+):** The `System.Runtime.InteropServices.TypeMapping` API will provide first-class support for external type maps. This will simplify Native AOT integration by:
- Eliminating the need for assembly-level attributes scanned via reflection
- Providing ILC intrinsics for type lookup
- Enabling better trimming through compile-time analysis

Until this API is available, the `TypeMapAttribute<T>` pattern with self-applying proxy attributes remains the most AOT-compatible approach.

