# Type Mapping API Specification for .NET Android

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

### 9.3 UnsafeAccessor for Protected Constructors

When the activation constructor is protected or in a base class:

```csharp
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
{
    var instance = (MyActivity)RuntimeHelpers.GetUninitializedObject(typeof(MyActivity));
    CallBaseActivationCtor((Activity)instance, handle, transfer);
    return instance;
}

[UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
static extern void CallBaseActivationCtor(Activity instance, IntPtr handle, JniHandleOwnership transfer);
```

**Note:** When using base class constructor via `UnsafeAccessor`, derived type field initializers do NOT run. This matches legacy behavior.

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

The TypeMap V3 system uses `TypeMapAttribute` to point to **proxy types**, which have direct references to the real types.

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

### 15.7 Legacy Trimmer Step Semantics (IMPORTANT)

Understanding how the legacy ILLink custom steps work is critical for migration. There are two fundamentally different operations:

#### Marking vs Preserving

| Operation | Method | Effect | When Called |
|-----------|--------|--------|-------------|
| **Mark** | `Annotations.Mark(type)` | Roots a type - it WILL be in final output | Unconditionally during assembly processing |
| **Preserve** | `Annotations.AddPreservedMethod(type, method)` | Keeps method IF type is already marked | Only after type is already marked |

**Key Insight:** Most legacy steps do NOT mark types unconditionally. They only preserve additional members on types that are ALREADY marked through normal code references.

#### MarkJavaObjects: The Only Unconditional Marker

`MarkJavaObjects` is the only step that calls `Annotations.Mark()` to unconditionally root types:

```csharp
// ProcessAssembly - called once per assembly, unconditionally marks types
public void ProcessAssembly(AssemblyDefinition assembly, ...) {
    foreach (var type in assembly.MainModule.Types) {
        // 1. Custom HttpMessageHandler from settings
        if (assemblyQualifiedName == androidHttpClientHandlerType) {
            Annotations.Mark(type);  // UNCONDITIONAL!
            continue;
        }
        
        // 2. Custom views from layout XML files
        if (customViewMap.ContainsKey(type.FullName)) {
            Annotations.Mark(type);  // UNCONDITIONAL!
            continue;
        }
        
        // 3. Types with [Activity], [Service], etc. attributes
        if (ShouldPreserveBasedOnAttributes(type)) {
            Annotations.Mark(type);  // UNCONDITIONAL!
            continue;
        }
    }
}

// ProcessType - called when type is ALREADY MARKED, preserves members
public void ProcessType(TypeDefinition type) {
    // Only called if type is already marked!
    PreserveJavaObjectImplementation(type);  // Uses AddPreservedMethod
    if (IsImplementor(type))
        PreserveImplementor(type);  // Uses AddPreservedMethod
}
```

**Attributes that trigger unconditional marking (`ShouldPreserveBasedOnAttributes`):**
- `Android.App.ActivityAttribute`
- `Android.App.ApplicationAttribute`
- `Android.App.InstrumentationAttribute`
- `Android.App.ServiceAttribute`
- `Android.Content.BroadcastReceiverAttribute`
- `Android.Content.ContentProviderAttribute`

#### Other Steps: Preserve Only, Don't Mark

| Step | Does it Mark? | What it does |
|------|---------------|--------------|
| `PreserveJavaInterfaces` | NO | Preserves interface methods on already-marked interfaces |
| `PreserveRegistrations` | NO | Preserves handler/connector methods on already-marked methods |
| `PreserveApplications` | NO | Preserves backup agent types referenced in [Application] |
| `PreserveJavaExceptions` | NO | Preserves string ctor on exception types |

**This means:** If a type like `IContentHandler` or `OnClickListenerImplementor` is NOT referenced by user code, it is NOT marked, and none of these preservation steps run for it. The type is trimmed away.

#### Custom Views from Layout XML

**CRITICAL:** `MarkJavaObjects` unconditionally marks types found in Android layout XML files:

```xml
<!-- layout.xml -->
<com.example.MyCustomView
    android:layout_width="match_parent"
    android:layout_height="wrap_content" />
```

The build process:
1. `GenerateLayoutBindings` task parses layout XML files
2. Creates `AndroidCustomViewMapFile` with type names
3. `MarkJavaObjects.ProcessAssembly` reads this file
4. Calls `Annotations.Mark()` for each type in the map

**V3 Requirement:** The TypeMap generator must also process layout files and generate unconditional TypeMapAttribute entries for custom views.

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

**V3 Requirement:** The TypeMap generator must process the custom view map file and generate unconditional entries.

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
// Application proxy references BackupAgent
class MyApp_Proxy {
    static Type GetBackupAgentType() => typeof(MyBackupAgent);  // Direct reference!
}

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

#### ILLink Step Replacements

| ILLink Step | Replacement |
|-------------|-------------|
| `PreserveApplications` | unconditional TypeMap to proxy (proxy refs BackupAgent directly) |
| `PreserveJavaExceptions` | unconditional TypeMap to proxy (proxy refs string ctor directly) |
| `PreserveJavaInterfaces` | unconditional TypeMap to proxy (proxy refs all methods directly) |
| `MarkJavaObjects` | ✅ Already done (proxy refs activation ctor) |
| `PreserveRegistrations` | ✅ Already done (unconditional TypeMap to proxy) |

#### Decision: unconditional vs trimmable

```
Is this type registered in AndroidManifest.xml?
└─ YES → unconditional (unconditional) - Android can create it anytime
└─ NO → Is this a JCW (user's .NET type with Java wrapper)?
        └─ YES → unconditional (unconditional) - Java code might call it
        └─ NO → Is this an MCW (binding for existing Java class)?
                └─ YES → trimmable (conditional) - only if .NET code uses it
                └─ NO → unconditional (unconditional) - default to safe
```

#### Benefits

1. **Cross-runtime** - Works with both ILLink and ILC
2. **Declarative** - Preservation is expressed in metadata, not imperative code
3. **No DynamicDependency needed** - Proxy direct references do the work
4. **No custom ILLink steps** - Can be deleted entirely
5. **NativeAOT compatible** - Enables skipping ILLink for NativeAOT builds

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

- [ ] UnsafeAccessor for protected/base constructors
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

---

*Document version: 1.0*
*Last updated: 2026-01-26*
