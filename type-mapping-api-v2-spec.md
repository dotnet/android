# Type Mapping API v2 Specification for .NET Android

> This document supersedes `type-mapping-api-codegen-spec.md` and incorporates learnings from the PoC implementation.

## 1. Overview

### 1.1 Purpose

This specification defines the architecture for enabling Java-to-.NET interoperability in .NET Android applications using the .NET Type Mapping API. The design is fully compatible with Native AOT and trimming, replacing the legacy reflection-based `TypeManager` system.

### 1.2 Goals

- **AOT-Safe**: All type instantiation and method resolution works with Native AOT
- **Trimming-Safe**: Proper annotations ensure required types survive aggressive trimming
- **Single Activation Path**: All types (framework + user) use the same activation mechanism
- **Performance**: Minimize runtime overhead through caching at both native and managed layers
- **Developer Experience**: No changes required to existing .NET Android application code

### 1.3 Non-Goals

- Debug builds using Mono (Mono does not implement the .NET Type Mapping API). Debug builds continue to use the existing reflection-based TypeManager until Mono is deprecated.
- Non-shipping code (desktop JVM targets in java-interop repo)
- `[Export]` attribute support (open question for future work)

---

## 2. Background

### 2.1 Managed-Callable Wrappers (MCW)

MCWs are .NET bindings for Java classes. They allow managed code to instantiate and call Java objects:

```csharp
// MCW - wraps existing Java class android.widget.TextView
[Register("android/widget/TextView", DoNotGenerateAcw = true)]
public class TextView : View { ... }
```

**`DoNotGenerateAcw = true`:** This flag indicates the type is a pure MCW (binding for an existing Java class). No JCW `.java` file is generated for it, but a proxy type IS still generated for type map lookups and peer creation.

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
┌─────────────────────────────────────────────────────────────────────┐
│                          BUILD TIME                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────────┐    ┌──────────────────┐    ┌────────────────┐ │
│  │ Scan Assemblies │───▶│ Generate Proxies │───▶│ Generate JCWs  │ │
│  │ for Java Peers  │    │ + TypeMap Attrs  │    │ (.java files)  │ │
│  └──────────────────┘    └──────────────────┘    └────────────────┘ │
│                                 │                        │         │
│                                 ▼                        ▼         │
│                          ┌──────────────────┐    ┌────────────────┐ │
│                          │ Generate LLVM IR │    │ Compile to    │ │
│                          │ (.ll files)      │    │ .dex          │ │
│                          └──────────────────┘    └────────────────┘ │
│                                 │                                  │
│                                 ▼                                  │
│                          ┌──────────────────┐                     │
│                          │ Compile to .o    │                     │
│                          │ Link into DSO    │                     │
│                          └──────────────────┘                     │
│                                                                   │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                          RUNTIME                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Java calls native method (e.g., n_onCreate)                      │
│         │                                                         │
│         ▼                                                         │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ LLVM-generated stub (Java_com_example_MainActivity_n_1...) │   │
│  │   1. Check cached function pointer                         │   │
│  │   2. If null, call typemap_get_function_pointer            │   │
│  │   3. Call resolved UCO method                              │   │
│  └──────────────────────────────────────────────────────────────┘   │
│         │                                                         │
│         ▼                                                         │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │ TypeMapAttributeTypeMap.GetFunctionPointer(className, index) │ │
│  │   1. Lookup type in TypeMapping API                          │ │
│  │   2. Get cached JavaPeerProxy instance                       │ │
│  │   3. Return proxy.GetFunctionPointer(methodIndex)            │ │
│  └────────────────────────────────────────────────────────────────┘ │
│         │                                                         │
│         ▼                                                         │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Generated [UnmanagedCallersOnly] UCO method                │   │
│  │   - Calls original n_* callback or nc_activate_* method    │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                   │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ITypeMap` | `Mono.Android.dll` | Interface abstracting type mapping and peer creation |
| `TypeMapAttributeTypeMap` | `Mono.Android.dll` | `ITypeMap` implementation using Type Mapping API (CoreCLR/NativeAOT) |
| `LlvmIrTypeMap` | `Mono.Android.dll` | `ITypeMap` implementation using native LLVM IR maps (Mono). Note: `GetFunctionPointer` throws `NotSupportedException` because Mono uses the legacy native registration mechanism. |
| `GenerateTypeMapAttributesStep` | ILLink step (PoC) / MSBuild task (production) | Generates proxies, TypeMap attributes, JCWs, LLVM IR |
| `JavaPeerProxy` | `Mono.Android.dll` | Base class for generated proxy types |
| `DynamicNativeMembersRegistration` | `Mono.Android.dll` | Handles `[Export]` registration (reflection-based, guarded) |
| LLVM IR stubs | Generated `.ll` files | Native JNI method stubs with caching |

### 3.3 ITypeMap Interface

The `ITypeMap` interface abstracts type mapping operations, making `JniValueManager` and `JniTypeManager` completely agnostic of the underlying type map implementation:

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
    
    // Peer instance creation (main entry point for wrapping Java objects)
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);
    
    // Marshal method function pointer resolution
    IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex);
}
```

### 3.4 Runtime Selection and Dependency Injection

At initialization, the runtime selects the appropriate `ITypeMap` implementation based on feature switches and injects it into the managers:

```csharp
// In JNIEnvInit.Initialize()
ITypeMap typeMap = CreateTypeMap();
var typeManager = new AndroidTypeManager(typeMap, ...);
var valueManager = CreateValueManager(typeMap);

private static ITypeMap CreateTypeMap()
{
    if (RuntimeFeature.IsCoreClrRuntime)
        return new TypeMapAttributeTypeMap();  // AOT-safe, uses generated attributes
    else if (RuntimeFeature.IsMonoRuntime)
        return new LlvmIrTypeMap();            // Uses native LLVM IR type maps
    else
        throw new NotSupportedException();
}
```

### 3.5 Feature Switches

| Switch | Default | Purpose |
|--------|---------|---------|
| `Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime` | `true` | Selects Mono runtime path with `LlvmIrTypeMap` |
| `Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime` | `false` | Selects CoreCLR/NativeAOT path with `TypeMapAttributeTypeMap` |
| `Microsoft.Android.Runtime.RuntimeFeature.IsAssignableFromCheck` | `true` | Enables runtime type compatibility checks in `LlvmIrTypeMap` |

### 3.6 Dynamic Native Member Registration

Code that requires reflection (e.g., `[Export]` attribute handling) is isolated in `DynamicNativeMembersRegistration`:

```csharp
[RequiresUnreferencedCode("Dynamic native member registration requires unreferenced code...")]
static class DynamicNativeMembersRegistration
{
    public static void RegisterNativeMembers(JniType nativeClass, Type type, ReadOnlySpan<char> methods)
    {
        // Completely disabled on CoreCLR - TypeMaps handle registration at build time
        if (RuntimeFeature.IsCoreClrRuntime)
        {
            Logger.Log(LogLevel.Info, "DynamicNativeMembersRegistration", "Skipping on CoreCLR");
            return;
        }
        
        // ... Mono path: dynamic registration via Mono.Android.Export.dll
    }
}
```

This separation ensures:
1. **Trimming safety**: `[RequiresUnreferencedCode]` clearly marks the reflection boundary
2. **AOT compatibility**: CoreCLR/NativeAOT path never executes reflection-based registration
3. **Clean architecture**: Managers don't need to know about registration details

---

## 4. Type Map Attributes

### 4.1 TypeMap Attribute Structure

Each Java peer type is registered in the type map using assembly-level attributes:

```csharp
// TypeMap<TUniverse>(string jniClassName, Type type, Type trimTarget)
// - jniClassName: Java class name used as lookup key
// - type: The proxy type RETURNED by TypeMap lookups
// - trimTarget: The actual target type (ensures trimmer preserves the mapping when target is used)

// Single type mapping - most common case
[assembly: TypeMap<Java.Lang.Object>("android/app/Activity", typeof(Activity_Proxy), typeof(Activity))]

// Aliased types - see Section 4.3 for full explanation
// (multiple .NET types share same Java class name)
```

### 4.2 Interface-to-Invoker Mapping

**Problem:** Interfaces and abstract classes cannot be instantiated directly. When Java returns an object that implements `IOnClickListener`, we need to wrap it in a concrete .NET type that implements the interface - this is called an "invoker" type. Whenever the interface survives trimming, the invoker type needs to survive trimming too.

**Example:**
```csharp
// Interface binding - cannot be instantiated
[Register("android/view/View$OnClickListener", DoNotGenerateAcw = true)]
public interface IOnClickListener : IJavaObject { ... }

// Invoker - concrete implementation that wraps Java objects
internal class IOnClickListenerInvoker : Java.Lang.Object, IOnClickListener { ... }
```

**Solution:** The interface proxy's `CreateInstance` method directly instantiates the invoker:

```csharp
// Interface proxy - no TypeMapAssociation needed
[IOnClickListener_Proxy]
public sealed class IOnClickListener_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new IOnClickListenerInvoker(handle, transfer);  // Direct instantiation
}
```

> **EXPERIMENTAL (2026-01-24):** We removed `TypeMapAssociation<InvokerUniverse>` for interface-to-invoker mappings. The invoker type is baked directly into the interface proxy's `CreateInstance` method at build time, eliminating the need for runtime lookup. This simplifies the architecture but may need to be rolled back if trimming issues arise (e.g., if the invoker gets trimmed because it's only referenced from generated code). The trimmer should preserve the invoker via the proxy's `CreateInstance` body, but this needs validation.

**Why this works for trimming:**

The interface proxy references the invoker type directly in its `CreateInstance` method body. When the trimmer preserves the interface proxy (via the `TypeMap` attribute's `trimTarget`), it follows the method body and preserves the invoker constructor call. This is simpler than using `TypeMapAssociation` and avoids maintaining a separate invoker lookup table at runtime.

### 4.2.1 Proxy Attributes for Interfaces, Invokers, and Implementors

This section clarifies the relationship between different types in the interface binding pattern and what their proxy attributes should do.

**Type Categories:**

| Type | Example | Has JCW? | `DoNotGenerateAcw` | Needs Proxy? |
|------|---------|----------|-------------------|--------------|
| Interface | `IOnClickListener` | No | Yes | Yes (limited) |
| Invoker | `IOnClickListenerInvoker` | No | Yes | Yes |
| Implementor | `IOnClickListenerImplementor` | Yes | No | Yes |

**Interface Proxy Behavior:**

Interfaces have entries in `_externalTypeMap` (to find the interface from a Java class name), but their proxy has LIMITED functionality:

```csharp
[IOnClickListener_Proxy]
public sealed class IOnClickListener_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
    {
        // Interfaces don't have JCWs - no Java code calls native methods on them directly.
        // If this is called, it's a bug in the LLVM IR generation.
        throw new NotSupportedException(
            "GetFunctionPointer should not be called for interface types. " +
            "Native callbacks are resolved via Implementor proxies.");
    }

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Delegate to the invoker - this is the recommended pattern
        return new IOnClickListenerInvoker(handle, transfer);
    }
}
```

**Key insight:** The interface's `CreateInstance` should directly create an instance of the **Invoker** type. This eliminates an extra lookup step at runtime. The alternative (throwing `NotSupportedException` and requiring explicit `TryGetInvokerType` calls) adds complexity without benefit.

**Invoker Proxy Behavior:**

Invokers extend `Java.Lang.Object` and wrap existing Java objects. They have `DoNotGenerateAcw=true` because they bind to existing Java interfaces (no JCW needed). Invokers have activation constructors and may have `n_*` callback methods.

```csharp
[IOnClickListenerInvoker_Proxy]
public sealed class IOnClickListenerInvoker_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
    {
        // Invokers don't have their own JCWs. Their n_* methods are static helpers
        // used by Implementors, not called directly from Java on the Invoker.
        throw new NotSupportedException(
            "GetFunctionPointer should not be called for Invoker types. " +
            "Native callbacks are resolved via Implementor proxies.");
    }

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Create an instance of the invoker wrapping the Java object
        return new IOnClickListenerInvoker(handle, transfer);
    }
}
```

**Implementor Proxy Behavior:**

Implementors ARE Java-callable - they have JCWs. When Java calls methods on an Implementor, the native callbacks (defined in the Invoker) are called. The Implementor's proxy provides function pointers to UCO wrappers for these callbacks:

```csharp
[View_IOnClickListenerImplementor_Proxy]
public sealed class View_IOnClickListenerImplementor_Proxy : JavaPeerProxy
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
    static void n_OnClick_mm_0(IntPtr jnienv, IntPtr obj, IntPtr p0)
    {
        // ... wrapper that calls IOnClickListenerInvoker.n_OnClick_...
    }
}
```

**Why Invokers Don't Need `GetFunctionPointer`:**

1. **No JCW:** Invokers have `DoNotGenerateAcw=true` - no Java code is generated for them
2. **Callback location:** The `n_*` callbacks in Invokers are *static helper methods* called by the generated UCO wrappers, not JNI native methods registered with the VM
3. **LLVM IR target:** LLVM IR stubs are generated for JCW native methods. Since Invokers have no JCW, there are no LLVM IR stubs for them
4. **Implementor indirection:** When an Implementor's JCW calls a native method, the LLVM IR looks up the Implementor (not the Invoker) and calls its `GetFunctionPointer`

**Registration in TypeMap:**

| TypeMap | Interface | Invoker | Implementor |
|---------|-----------|---------|-------------|
| `_externalTypeMap` (string → Type) | ✅ Yes | ❌ No (same Java name as interface) | ✅ Yes |
| `_invokerTypeMap` (Type → Type) | ✅ Yes (as key) | ✅ Yes (as value) | ❌ No |
| Proxy attribute needed | ✅ Yes | ✅ Yes | ✅ Yes |

**`JniTypeManager.GetInvokerType` Considerations:**

The `ITypeMap.TryGetInvokerType` method remains necessary for type resolution scenarios where we have an interface type and need to find its invoker. However, with the recommended pattern where interface proxies directly return invoker instances from `CreateInstance`, the explicit `TryGetInvokerType` call in `CreatePeer` becomes a fallback rather than the primary path.

If an interface's proxy properly delegates to the invoker in `CreateInstance`, `TryGetInvokerType` is only called when:
1. Legacy code explicitly requests the invoker type
2. Type resolution for reflection scenarios

For these edge cases, `TryGetInvokerType` should continue to work normally. Throwing `NotSupportedException` would break legitimate use cases like type inspection and advanced interop scenarios.

### 4.3 Type Aliases

When multiple .NET types share the same Java class name, we need a way to:
1. Look up all types for a given Java class name
2. Select the correct type at runtime based on context
3. Allow independent trimming of aliased types

**Example scenario:** Two sibling classes both registered with the same Java name:

```csharp
[Register("com/example/MyHandler")]
class MyHandlerA : Java.Lang.Object { ... }

[Register("com/example/MyHandler")]  // Same Java name!
class MyHandlerB : Java.Lang.Object { ... }
```

If the app only uses `MyHandlerA`, we want `MyHandlerB` to be trimmed - but we still need the alias system to work for the surviving type.

**Solution:** An alias holder type with indexed TypeMap entries and bidirectional associations:

```csharp
// 1. Base Java name maps to the alias holder
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler", typeof(MyHandler_Aliases), typeof(MyHandler_Aliases))]

// 2. Indexed names map to individual proxy types
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler[0]", typeof(MyHandlerA_Proxy), typeof(MyHandlerA))]
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler[1]", typeof(MyHandlerB_Proxy), typeof(MyHandlerB))]

// 3. Associations from target types back to alias holder (enables trimming)
// NOTE: Uses AliasesUniverse, separate from Java.Lang.Object used for invokers!
[assembly: TypeMapAssociation<AliasesUniverse>(typeof(MyHandlerA), typeof(MyHandler_Aliases))]
[assembly: TypeMapAssociation<AliasesUniverse>(typeof(MyHandlerB), typeof(MyHandler_Aliases))]

// 4. Alias holder with keys to look up individual types
[JavaInteropAliases("com/example/MyHandler[0]", "com/example/MyHandler[1]")]
sealed class MyHandler_Aliases { }
```

**Why `TypeMapAssociation` enables trimming:**

The `TypeMapAssociation<AliasesUniverse>(typeof(TargetType), typeof(AliasHolder))` creates a **reverse mapping** from each target type to its alias holder. This is **only for the trimmer** - it is never queried at runtime. The association enables independent trimming of aliased types:

- When the trimmer encounters `MyHandlerA`, it follows the association to preserve `MyHandler_Aliases`
- `MyHandlerB` can still be trimmed if it's not used elsewhere in the code
- At runtime, when looking up `"com/example/MyHandler"`, the alias holder lets us enumerate which aliases (`[0]`, `[1]`, etc.) survived trimming
- Only the preserved indexed entries will be in the TypeMap

Without `TypeMapAssociation`, if `MyHandlerA` is used but `MyHandler_Aliases` is not directly referenced, the alias holder would be trimmed and we'd lose the ability to enumerate aliases.

**Why `AliasesUniverse` instead of `Java.Lang.Object`:**

The `TypeMapAssociation<Java.Lang.Object>` is already used for interface-to-invoker mappings (see Section 4.2). If we used the same universe for alias associations, there would be key collisions when a type has both an invoker AND participates in aliases. Using a separate `AliasesUniverse` prevents this collision. Since alias associations are never queried at runtime (only used by the trimmer), this is purely a linker/trimmer concern.

**Runtime flow:**
1. Look up `"com/example/MyHandler"` → get `MyHandler_Aliases`
2. Read `[JavaInteropAliases]` attribute → get `["com/example/MyHandler[0]", "com/example/MyHandler[1]"]`
3. Look up each indexed key → only keys that survived trimming will be present (e.g., only `[0]` if `MyHandlerB` was trimmed)
4. Use context (Java class hierarchy) to select the correct type from surviving aliases

**IMPORTANT - Indexed names for function pointer lookup:**

When calling `GetFunctionPointer`, the LLVM IR must use the **indexed alias name**, not the base name. For example, the JCW for `MyHandlerB` must look up `"com/example/MyHandler[1]"`, not `"com/example/MyHandler"`. This ensures the correct proxy's function pointers are resolved. See Section 12.4 for details.

### 4.4 Proxy Self-Application Pattern

**Problem:** We generate the type map independently of the target code. The `MainActivity` class exists in another assembly that we cannot modify - we cannot apply attributes to it.

**Solution:** The proxy type applies itself as an attribute to itself, and the TypeMap stores the **proxy type** as the lookup result:

```csharp
// 1. TypeMap registration - proxy is the lookup result, target is for trimming
[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(MainActivity_Proxy), typeof(MainActivity))]
//                                                               ^^^^^^^^^^^^^^^^^^^^^^^^^^  ^^^^^^^^^^^^^^^^^^^^
//                                                               lookup returns this         trimTarget: preserves MainActivity_Proxy if MainActivity is preserved

// 2. Proxy applies ITSELF as an attribute to ITSELF
[MainActivity_Proxy]  // <-- Self-application!
public sealed class MainActivity_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex) => ...;
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new MainActivity(handle, transfer);
}

// 3. At runtime
Type proxyType = typeMap["com/example/MainActivity"];  // Returns typeof(MainActivity_Proxy)
JavaPeerProxy proxy = proxyType.GetCustomAttribute<JavaPeerProxy>();  // Returns MainActivity_Proxy instance
IJavaPeerable instance = proxy.CreateInstance(handle, transfer);  // Returns MainActivity instance
```

**Why this works:**

1. **TypeMap returns the proxy type** - When we look up `"com/example/MainActivity"`, we get `typeof(MainActivity_Proxy)`, not `typeof(MainActivity)`

2. **Self-application enables AOT-safe instantiation** - The proxy applies itself as an attribute (`[MainActivity_Proxy]`), so `proxyType.GetCustomAttribute<JavaPeerProxy>()` returns an instance of the proxy. This uses .NET's built-in AOT-safe attribute instantiation.

3. **Trimming works via `trimTarget`** - The third TypeMap argument (`typeof(MainActivity)`) ensures that whenever the trimmer encounters `MainActivity`, it preserves the type mapping. This is how we maintain the connection between the target type and its proxy without modifying the target.

4. **Proxy provides the factory methods** - `GetFunctionPointer()` and `CreateInstance()` on the proxy enable reverse P/Invokes and peer creation without reflection on the target type.

---

## 5. JavaPeerProxy Design

### 5.1 Base Class

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

**`JniHandleOwnership` enum:** Specifies how the JNI object reference should be handled:
- `DoNotTransfer` - The caller retains ownership; the peer does not delete the reference
- `TransferLocalRef` - Ownership transfers to the peer; peer will delete the local reference
- `TransferGlobalRef` - Ownership transfers to the peer; peer will delete the global reference

For activation (Java creating .NET peer), `DoNotTransfer` is used because Java owns the reference.

### 5.2 AOT-Safe Proxy Instantiation

The proxy applies itself as an attribute. The TypeMap returns the proxy type, and we use `GetCustomAttribute<JavaPeerProxy>()` to get an instance:

```csharp
// TypeMap stores: ("android/app/Activity", typeof(Activity_Proxy), typeof(Activity))
//                                          ^^^^^^^^^^^^^^^^^^^^^  ^^^^^^^^^^^^^^^^
//                                          lookup result          trimTarget

Type proxyType = _externalTypeMap["android/app/Activity"];  // Returns typeof(Activity_Proxy)
JavaPeerProxy proxy = proxyType.GetCustomAttribute<JavaPeerProxy>();  // Returns Activity_Proxy instance
IJavaPeerable instance = proxy.CreateInstance(handle, transfer);  // Returns Activity instance
```

**Key insight:** The .NET runtime's `GetCustomAttribute<T>()` instantiates attributes in an AOT-safe manner. By having the proxy apply itself as an attribute, we get AOT-safe proxy instantiation without `Activator.CreateInstance()`.

### 5.3 Proxy Behavior by Type Category

Not all Java peer types behave the same way. The proxy's `GetFunctionPointer` and `CreateInstance` methods have different semantics depending on the type category:

| Type Category | Has JCW? | `GetFunctionPointer` Behavior | `CreateInstance` Behavior |
|---------------|----------|-------------------------------|---------------------------|
| Concrete class with JCW | Yes | Returns UCO function pointers | Creates instance of the type |
| MCW (framework binding) | No | Throws `NotSupportedException` | Creates instance of the type |
| Interface | No | Throws `NotSupportedException` | Creates instance of **Invoker** |
| Abstract class | No (unless subclassed) | Throws or returns UCO fnptrs | Creates instance of **Invoker** |
| Invoker | No | Throws `NotSupportedException` | Creates instance of the invoker |
| Implementor | Yes | Returns UCO function pointers | Creates instance of the implementor |

**Why Interfaces and Invokers throw from `GetFunctionPointer`:**

1. **No JCW means no LLVM IR stubs:** `GetFunctionPointer` is called from LLVM IR native stubs. These stubs are only generated for types that have JCWs. Types with `DoNotGenerateAcw=true` don't have JCWs, so no LLVM IR calls their `GetFunctionPointer`.

2. **Callback methods live elsewhere:** When an interface's callback is invoked from Java, it's through an Implementor's JCW. The Implementor's proxy provides the function pointers that wrap the static `n_*` callback methods (which are typically defined in the Invoker class).

3. **Defensive programming:** If `GetFunctionPointer` is somehow called on an interface or invoker proxy, it indicates a bug in code generation. Throwing `NotSupportedException` with a descriptive message helps diagnose such issues.

**Why Interface `CreateInstance` returns an Invoker instance:**

When `CreatePeer` finds that a Java object's type maps to an interface, it needs to create a concrete wrapper. The most efficient approach is for the interface's proxy to directly return an Invoker instance:

```csharp
// Interface proxy
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    => new IOnClickListenerInvoker(handle, transfer);  // Returns Invoker!
```

This avoids an extra lookup step via `TryGetInvokerType` at runtime. The `TryGetInvokerType` API remains available for scenarios that explicitly need the invoker type (not an instance).

---

## 6. Generated Proxy Types

### 6.1 Proxy with Direct Constructor Call

When the type has its own activation constructor:

```csharp
[com_example_MainActivity_Proxy]  // Self-application
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

### 6.2 Proxy with Base Class Constructor

When the type lacks an activation constructor but a base class has one:

```csharp
[com_example_MyActivity_Proxy]  // Self-application
public sealed class com_example_MyActivity_Proxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        var instance = (MyActivity)RuntimeHelpers.GetUninitializedObject(typeof(MyActivity));
        CallBaseActivationCtor(instance, handle, transfer);
        return instance;
    }
    
    // Note: First parameter is the base class type (Activity), but we pass derived instance
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
    static extern void CallBaseActivationCtor(Activity instance, IntPtr handle, JniHandleOwnership transfer);
    
    // ... activation UCO uses same pattern
}
```

### 6.3 Interface Proxy (delegates to Invoker)

For interfaces, the proxy's `CreateInstance` returns an Invoker instance, and `GetFunctionPointer` throws:

```csharp
[android_view_View_IOnClickListener_Proxy]
public sealed class android_view_View_IOnClickListener_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
    {
        // Interfaces don't have JCWs, so no LLVM IR stubs call this method.
        // If called, it's a bug in code generation.
        throw new NotSupportedException(
            "GetFunctionPointer is not supported for interface types. " +
            "The interface 'Android.Views.View.IOnClickListener' does not have a JCW.");
    }

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Directly create an Invoker instance - no need for TryGetInvokerType at runtime
        return new Android.Views.View.IOnClickListenerInvoker(handle, transfer);
    }
}
```

### 6.4 Invoker Proxy

Invokers are concrete classes that wrap Java interface implementations. They have activation constructors but no JCW:

```csharp
[android_view_View_IOnClickListenerInvoker_Proxy]
public sealed class android_view_View_IOnClickListenerInvoker_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex)
    {
        // Invokers have DoNotGenerateAcw=true, so no LLVM IR stubs.
        // The n_* methods in Invokers are static helpers called by Implementor UCO wrappers.
        throw new NotSupportedException(
            "GetFunctionPointer is not supported for Invoker types. " +
            "The invoker 'Android.Views.View.IOnClickListenerInvoker' does not have a JCW.");
    }

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new Android.Views.View.IOnClickListenerInvoker(handle, transfer);
}
```

### 6.5 Implementor Proxy

Implementors are .NET classes that implement Java interfaces and ARE callable from Java. They have JCWs and need full proxy support:

```csharp
[mono_android_view_View_OnClickListenerImplementor_Proxy]
public sealed class mono_android_view_View_OnClickListenerImplementor_Proxy : JavaPeerProxy
{
    public override IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch
    {
        // onClick callback - calls the static n_OnClick_... method from the Invoker
        0 => (IntPtr)(delegate*<IntPtr, IntPtr, IntPtr, void>)&n_OnClick_mm_0,
        // Activation
        1 => (IntPtr)(delegate*<IntPtr, IntPtr, void>)&nc_activate_0,
        _ => IntPtr.Zero
    };

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new Android.Views.View.IOnClickListenerImplementor(handle, transfer);

    [UnmanagedCallersOnly]
    public static void n_OnClick_mm_0(IntPtr jnienv, IntPtr obj, IntPtr v)
    {
        AndroidRuntimeInternal.WaitForBridgeProcessing();
        try {
            // Call the static callback method from the Invoker
            Android.Views.View.IOnClickListenerInvoker.n_OnClick_Landroid_view_View_(jnienv, obj, v);
        } catch (Exception ex) {
            AndroidEnvironmentInternal.UnhandledException(jnienv, ex);
        }
    }

    [UnmanagedCallersOnly]
    public static void nc_activate_0(IntPtr jnienv, IntPtr jobject) { /* ... */ }
}
```

### 6.6 UCO Wrapper Details

**`WaitForBridgeProcessing()`:** Regular method UCO wrappers (like `n_onCreate_mm_0`) call `AndroidRuntimeInternal.WaitForBridgeProcessing()` before invoking the callback. This ensures the GC bridge has finished processing any pending cross-heap references, preventing race conditions between Java GC and .NET GC. **Note:** Activation UCOs (`nc_activate_N`) do NOT call `WaitForBridgeProcessing()` because activation happens during object construction before the GC bridge is involved.

**Exception handling:** All UCO wrappers catch exceptions and forward them to `AndroidEnvironmentInternal.UnhandledException()` to prevent native crashes and allow proper exception propagation to Java.

**`WithinNewObjectScope` check (activation only):** When managed code calls `new MyActivity()`, the C# constructor calls the Java superclass constructor, which in turn calls `nc_activate_0()`. The `WithinNewObjectScope` check detects this scenario and skips activation because the object is already being constructed from the managed side.

---

## 7. Activation Constructor Handling

### 7.1 Two Constructor Styles

The codegen supports both activation constructor signatures:

| Style | Signature | Origin |
|-------|-----------|--------|
| **XI (Xamarin.Android)** | `(IntPtr handle, JniHandleOwnership transfer)` | Classic Android binding |
| **JI (Java.Interop)** | `(ref JniObjectReference reference, JniObjectReferenceOptions options)` | Newer Java.Interop |

**Search order:** XI first, then JI as fallback.

### 7.2 Constructor Search Algorithm

```
1. Check if type T has XI ctor → use directly
2. Check if type T has JI ctor → use directly  
3. Walk up hierarchy (repeat until found or exhausted):
   a. Check BaseType for XI ctor → use with [UnsafeAccessor]
   b. Check BaseType for JI ctor → use with [UnsafeAccessor]
4. If no ctor found → emit build error
```

**Why XI is preferred over JI:** XI constructors (`IntPtr, JniHandleOwnership`) are the established pattern for Xamarin.Android bindings. JI constructors (`ref JniObjectReference, JniObjectReferenceOptions`) are the newer Java.Interop pattern. Most existing types use XI, so checking it first is more efficient. Either style works correctly for activation.

### 7.3 Field Initializer Behavior

When calling a base class constructor via `[UnsafeAccessor]` on a derived instance created with `RuntimeHelpers.GetUninitializedObject()`:

| Scenario | Field Initializers |
|----------|-------------------|
| Type **has** its own activation ctor | ✓ Run correctly |
| Type **missing** ctor, use base class ctor | ✗ Do NOT run |

This is **identical** to the previous reflection-based behavior and maintains backwards compatibility.

### 7.4 Types Without Any Activation Constructor

If a type and its entire hierarchy lack an activation constructor, a throwing `CreateInstance` is generated:

```csharp
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
{
    throw new MissingMethodException(
        $"No activation constructor found for type '{typeof(ProblematicType).FullName}'. " +
        "Add a constructor with signature (IntPtr, JniHandleOwnership) or " +
        "(ref JniObjectReference, JniObjectReferenceOptions).");
}
```

---

## 8. LLVM IR Generation

### 8.1 Per-Method Native Stubs

Each marshal method gets a native JNI stub with function pointer caching:

```llvm
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8
@fn_ptr_0 = internal unnamed_addr global ptr null, align 8
; UTF-16 encoded class name (24 characters = 48 bytes)
@class_name = internal constant [48 x i8] c"c\00o\00m\00/\00e\00x\00a\00m\00p\00l\00e\00/\00M\00a\00i\00n\00A\00c\00t\00i\00v\00i\00t\00y\00", align 2

define default void @Java_com_example_MainActivity_n_1onCreate__Landroid_os_Bundle_2(
    ptr %env, ptr %obj, ptr %p0) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_0, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  ; 24 = character count (UTF-16 code units)
  call void %get_fn(ptr @class_name, i32 24, i32 0, ptr @fn_ptr_0)
  %resolved_ptr = load ptr, ptr @fn_ptr_0, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  tail call void %fn(ptr %env, ptr %obj, ptr %p0)
  ret void
}
```

### 8.2 Callback Signature

```c
void (*typemap_get_function_pointer)(
    const char16_t* className,  // UTF-16 Java class name (NOT null-terminated)
    int32_t classNameLength,    // Length in char16_t units (character count)
    int32_t methodIndex,        // Index into proxy's GetFunctionPointer switch
    intptr_t* fnptr             // Out: resolved function pointer
);
```

**Why UTF-16 without null terminator:** The class name is stored as UTF-16 so it can be viewed directly as `ReadOnlySpan<char>` on the managed side without memory copying or string allocation. This enables zero-allocation lookups if the TypeMap API eventually supports `ReadOnlySpan<char>` keys.

### 8.3 Generated Files

| File | Purpose |
|------|---------|
| `marshal_methods_{TypeName}.ll` | Per-type JNI stubs including activation |
| `marshal_methods_init.ll` | Global `typemap_get_function_pointer` declaration |

---

## 9. Runtime Type Map

### 9.1 Initialization

```csharp
public TypeMapAttributeTypeMap()
{
    _externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object>();
    _invokerTypeMap = TypeMapping.GetOrCreateProxyTypeMapping<Java.Lang.Object>();
    // Note: No _aliasTypeMap needed - TypeMapAssociation<AliasesUniverse> is only for trimmer, not queried at runtime
}
```

### 9.2 Key Methods

| Method | Purpose |
|--------|---------|
| `TryGetTypesForJniName(string, out IEnumerable<Type>)` | Java class name → .NET type(s) |
| `TryGetInvokerType(Type, out Type)` | Interface/abstract → invoker |
| `TryGetJniNameForType(Type, out string)` | .NET type → Java class name |
| `GetProxyForType(Type, out JavaPeerProxy?)` | Get cached proxy instance for type |
| `GetFunctionPointer(string, int, out IntPtr)` | Resolve UCO function pointer |
| `CreatePeer(IntPtr, JniHandleOwnership)` | Create managed wrapper for Java object |

### 9.3 Caching Strategy

**Native layer (LLVM IR):**
- Per-method function pointer cache (`@fn_ptr_N` globals)
- First call resolves, subsequent calls use cached pointer

**Managed layer (TypeMapAttributeTypeMap):**
- `Dictionary<Type, JavaPeerProxy?>` with explicit `Lock` for proxy instances
- Proxy obtained once via `GetCustomAttribute`, then cached

**Thread safety note (native layer):** The LLVM IR uses non-atomic load/store for function pointer caching. If two threads call the same native method simultaneously when the cache is empty, both will resolve the function pointer. This is safe because:
1. Both threads get the same function pointer value
2. The worst case is redundant resolution, not incorrect behavior
3. Once cached, all subsequent calls use the cached value

---

## 10. Build Pipeline Integration

### 10.1 Current Implementation (PoC)

Code generation runs in an ILLink custom step **before MarkStep**:

```
ILLink (with GenerateTypeMapAttributesStep)
    ├── Scan assemblies for Java peers
    ├── Generate TypeMap attributes
    ├── Generate proxy types with UCO methods
    ├── Generate .java files for JCWs
    └── Generate .ll files for native stubs
```

### 10.2 Production Target Architecture

Code generation should move to a dedicated MSBuild task for:
- Better incremental build support
- Caching of SDK/NuGet type maps
- Separation of concerns from trimming

### 10.3 Target Execution Order

```
MSBuild Task (GenerateTypeMaps)
    └── Generates proxies, TypeMap attrs, .java, .ll
           │
           ▼
ILLink
    └── Trims assemblies (type map attrs are linker roots)
           │
           ▼
Java Compilation
    └── javac compiles .java → .class → .dex
           │
           ▼
LLVM Compilation
    └── .ll → .o
           │
           ▼
Native Linking
    └── .o files linked into app DSO
```

---

## 11. Blittable Type Handling

`[UnmanagedCallersOnly]` methods cannot use `bool` parameters—they must use `byte`.

**Generated wrapper pattern:**
```csharp
// Original callback: n_setEnabled(JNIEnv*, jobject, jboolean)
[UnmanagedCallersOnly]
public static void n_setEnabled_mm_0(IntPtr jnienv, IntPtr obj, byte enabled)
{
    // Convert byte to bool if needed inside wrapper
    OriginalType.n_setEnabled(jnienv, obj, enabled);  // n_setEnabled takes byte
}
```

---

## 12. Alias Handling

### 12.1 Problem

Multiple .NET types can map to the same Java class name:

```csharp
[Register("com/example/MyHandler")]
class MyHandlerA : Java.Lang.Object { }

[Register("com/example/MyHandler")]  // Same Java name!
class MyHandlerB : Java.Lang.Object { }
```

### 12.2 Solution

An alias holder type collects all types sharing a Java class name, with bidirectional associations for trimming:

```csharp
// Base name maps to alias holder
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler", typeof(MyHandler_Aliases), typeof(MyHandler_Aliases))]

// Indexed names map to proxy types
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler[0]", typeof(MyHandlerA_Proxy), typeof(MyHandlerA))]
[assembly: TypeMap<Java.Lang.Object>("com/example/MyHandler[1]", typeof(MyHandlerB_Proxy), typeof(MyHandlerB))]

// Associations from target types back to alias holder (enables independent trimming)
[assembly: TypeMapAssociation<Java.Lang.Object>(typeof(MyHandlerA), typeof(MyHandler_Aliases))]
[assembly: TypeMapAssociation<Java.Lang.Object>(typeof(MyHandlerB), typeof(MyHandler_Aliases))]

// Alias holder with keys to look up individual types
[JavaInteropAliases("com/example/MyHandler[0]", "com/example/MyHandler[1]")]
sealed class MyHandler_Aliases { }
```

See Section 4.3 for detailed explanation of why `TypeMapAssociation` is required.

### 12.3 Runtime Resolution

```csharp
public bool TryGetTypesForJniName(string jniName, out IEnumerable<Type> types)
{
    if (!_externalTypeMap.TryGetValue(jniName, out Type type))
    {
        types = [];
        return false;
    }
    
    // Check for aliases
    var aliasAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute>();
    if (aliasAttr != null)
    {
        var result = new List<Type>();
        foreach (var key in aliasAttr.AliasKeys)
        {
            // Single lookup per key - trimmed types won't be in the map
            if (_externalTypeMap.TryGetValue(key, out Type aliasedType))
            {
                result.Add(aliasedType);
            }
        }
        types = result;
        return result.Count > 0;
    }
    
    types = [type];
    return true;
}
```

### 12.4 Alias Disambiguation

When multiple types share the same Java class name, different scenarios require different lookup strategies:

**Scenario 1: Function pointer lookup (LLVM IR → managed)**

The LLVM IR stubs **must use the indexed alias name**, not the base name. Each JCW knows its fixed alias index at build time:

```llvm
; For MyHandlerB (alias index 1), use "com/example/MyHandler[1]"
@class_name = internal constant [...] c"c\00o\00m\00/\00e\00x\00a\00m\00p\00l\00e\00/\00M\00y\00H\00a\00n\00d\00l\00e\00r\00[\001\00]\00"

; NOT "com/example/MyHandler" - that would resolve to MyHandler_Aliases, not the proxy!
```

This ensures `GetFunctionPointer` resolves to the correct proxy type (`MyHandlerB_Proxy`) and returns the right UCO function pointers.

**Scenario 2: Peer creation (`CreatePeer`)**

When Java calls into .NET and we need to wrap a Java object, we start with the base Java class name and must find the most specific .NET type:

1. Look up `"com/example/MyHandler"` → get `MyHandler_Aliases`
2. Enumerate surviving aliases via `[JavaInteropAliases]`
3. Walk the Java class hierarchy to find the most specific .NET type that matches

**Scenario 3: .NET-to-Java type resolution (`TryGetJniNameForType`)**

When we have a .NET type and need its Java name, we use the `[Register]` attribute on the type itself - no alias lookup needed.

---

## 13. Performance Considerations

### 13.1 String-Based vs Integer-Based Lookup

The original spec considered integer indices for O(1) lookups. The current implementation uses string-based class name lookups.

**Trade-offs:**

| Aspect | Integer Indices | String-Based |
|--------|-----------------|--------------|
| Lookup speed | O(1) array index | O(1) hash lookup + string comparison |
| Complexity | Secondary type map needed | Direct integration with TypeMap API |
| Memory | Extra index table | Class name stored once |
| Caching | Still needed | Same caching approach |

**Decision:** String-based lookup with aggressive caching. The overhead is negligible after first call due to caching.

### 13.2 Caching Layers

1. **Native layer:** Per-method function pointer caching in LLVM IR globals
2. **Managed layer:** Proxy instance caching in `ConcurrentDictionary`
3. **JNI name caching (recommended):** Cache `string → Type[]` lookups

---

## 14. Trimming and AOT Safety

### 14.1 Linker Roots

TypeMap attributes create linker roots via `typeof()` references:

```csharp
[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", 
    typeof(MainActivity_Proxy),      // Rooted (lookup result)
    typeof(MainActivity))]           // Rooted (trimTarget - preserves mapping when target is used)
```

### 14.2 Attribute Preservation

`[Register]` attributes are preserved by the linker (configured in `PreserveLists/Mono.Android.xml`).

### 14.3 Constructor Preservation

The `CreateInstance` factory method uses direct `new` or `[UnsafeAccessor]`—no reflection required:

```csharp
// Direct - compiler ensures constructor exists
public override IJavaPeerable CreateInstance(IntPtr h, JniHandleOwnership t)
    => new MainActivity(h, t);

// UnsafeAccessor - AOT-safe accessor to base class ctor
[UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
static extern void CallCtor(Activity inst, IntPtr h, JniHandleOwnership t);
```

---

## 15. Open Questions

### 15.1 `[Export]` Support

**Status:** Not implemented.

**Options:**
1. Require migration to `[Register]` (breaking change)
2. Support both with same codegen pattern
3. Deprecate `[Export]` with fallback to reflection

### 15.2 Incremental Build Strategy

**Current:** Regenerate all type maps on every build.

**Proposed:**
1. **SDK types:** Pre-generate during SDK build, ship as artifacts
2. **NuGet types:** Generate on first build, cache by package version
3. **User types:** Always regenerate (typically few types)

### 15.3 Alternative Alias Key Strategy

**Current approach:** Indexed keys using brackets (e.g., `"android/app/Activity[0]"`, `"android/app/Activity[1]"`)

**Alternative idea:** Use C# class name as the alias key instead of indices:

```csharp
[Register("android/app/Activity")]
class MyActivityA : Activity { ... }

// Current (indexed):
[assembly: TypeMap<...>("android/app/Activity[0]", typeof(MyActivityA_Proxy), typeof(MyActivityA))]
[JavaInteropAliases("android/app/Activity[0]", ...)]

// Alternative (C# name):
[assembly: TypeMap<...>("MyActivityA", typeof(MyActivityA_Proxy), typeof(MyActivityA))]
[JavaInteropAliases("MyActivityA", ...)]
```

**Trade-offs:**

| Aspect | Indexed (`[0]`, `[1]`) | C# Class Name |
|--------|------------------------|---------------|
| Collision safety | ✅ Guaranteed - brackets can't appear in JNI names or C# identifiers | ⚠️ Risk if two classes have same simple name in different namespaces, or if C# name matches a JNI name |
| Codegen complexity | ❌ Requires tracking alias indices | ✅ Simpler - use class name directly |
| Debuggability | ❌ Less intuitive | ✅ More readable |
| LLVM IR generation | ❌ Must map type → index | ✅ Just use class name |

**Decision:** Using indexed approach for safety. The bracket characters `[` and `]` cannot appear in any valid JNI name or C# identifier, guaranteeing no collisions.

---

## 16. Implementation Checklist

### 16.1 Completed (PoC)

- [x] Basic type map attribute generation
- [x] Proxy type generation with UCO methods
- [x] JCW Java file generation for user types
- [x] LLVM IR generation for native stubs
- [x] Function pointer resolution via TypeMap
- [x] Single activation path for all types (no legacy TypeManager)
- [x] XI and JI constructor style support
- [x] `[UnsafeAccessor]` for base class constructor calls
- [x] Alias handling for multiple types per Java class (partial - see 16.2)
- [x] ITypeMap interface extraction for runtime abstraction
- [x] DynamicNativeMembersRegistration separation with `[RequiresUnreferencedCode]`
- [x] Feature switch-based runtime selection (`IsCoreClrRuntime`/`IsMonoRuntime`)
- [x] UTF-16 class names for zero-allocation `ReadOnlySpan<char>` lookups

### 16.2 Required Before Merge

- [ ] Move codegen from ILLink step to MSBuild task
- [ ] Complete activation UCO generation (currently returns immediately, see Section 6.6 for intended behavior)
- [ ] Generate `TypeMapAssociation<Java.Lang.Object>` for alias→alias holder mappings (method exists but never called)
- [ ] Generate proxy types for interfaces that delegate `CreateInstance` to Invokers (see Section 6.3)
- [ ] Generate proxy types for Invokers with `NotSupportedException` in `GetFunctionPointer` (see Section 6.4)
- [ ] Generate Implementor proxies with UCO wrappers that call Invoker's `n_*` methods (see Section 6.5)
- [ ] Trimming validation with `TrimMode=full`
- [ ] Exception handling verification in UCO wrappers
- [ ] Full test suite pass

### 16.3 Post-Merge Improvements

- [ ] Performance benchmarks (startup, callback overhead)
- [ ] JNI name → Type caching
- [ ] Incremental build support for SDK types
- [ ] Documentation updates

### 16.4 Future Enhancements

- [ ] `[Export]` method support
- [ ] Source generator alternative for faster inner loop
- [ ] Per-constructor UCOs (instead of signature matching)

---

## 17. Key File Locations in PoC

### 17.1 Build-Time Components

| Component | File |
|-----------|------|
| Main codegen | `src/Microsoft.Android.Sdk.ILLink/GenerateTypeMapAttributesStep.cs` |
| Build targets | `src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.ILLink.targets` |

### 17.2 Runtime Type Map Infrastructure

| Component | File |
|-----------|------|
| ITypeMap interface | `src/Mono.Android/Java.Interop/ITypeMap.cs` |
| CoreCLR/NativeAOT impl | `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` |
| Legacy CoreCLR/Mono impl | `src/Mono.Android/Java.Interop/LlvmIrTypeMap.cs` |
| Feature switches | `src/Mono.Android/Microsoft.Android.Runtime/RuntimeFeature.cs` |
| Dynamic registration | `src/Mono.Android/Java.Interop/DynamicNativeMembersRegistration.cs` |

### 17.3 Type Managers and Value Managers

| Component | File |
|-----------|------|
| Android type manager | `src/Mono.Android/Java.Interop/AndroidTypeManager.cs` |
| Android value manager | `src/Mono.Android/Java.Interop/AndroidValueManager.cs` |
| Managed value manager | `src/Mono.Android/Java.Interop/ManagedValueManager.cs` |
| JNI initialization | `src/Mono.Android/Android.Runtime/JNIEnvInit.cs` |

### 17.4 Attributes and Proxy Infrastructure

| Component | File |
|-----------|------|
| Proxy base class | `src/Mono.Android/Java.Interop/JavaPeerProxy.cs` |
| Alias attribute | `src/Mono.Android/Java.Interop/JavaInteropAliasesAttribute.cs` |
| Aliases universe | `src/Mono.Android/Java.Interop/AliasesUniverse.cs` |

---

## Appendix A: Migration from Legacy TypeManager

### A.1 Removed Components

| Component | Status |
|-----------|--------|
| `TypeManager.n_Activate` | Removed - no longer needed |
| `TypeManager.Activate()` | Removed - replaced by `nc_activate_N` UCOs |
| `marshal_methods_typemanager.ll` | Not generated |
| Integer-based assembly/type indices | Replaced by string-based class names |

### A.2 Changed Behavior

| Aspect | Legacy | New |
|--------|--------|-----|
| Framework JCW activation | `TypeManager.Activate()` | `nc_activate_N()` UCO |
| User JCW activation | `TypeManager.Activate()` | `nc_activate_N()` UCO |
| Type resolution | `Type.GetType()` | TypeMap API |
| Instance creation | `Activator.CreateInstance()` | `proxy.CreateInstance()` factory |

---

## Appendix B: Example Generated Artifacts

### B.1 Assembly Attributes (Mono.Android.dll)

```csharp
[assembly: TypeMap<Java.Lang.Object>("com/example/MainActivity", typeof(com_example_MainActivity_Proxy), typeof(MainActivity))]
// Note: Interface-to-invoker mappings are handled directly in the interface proxy's CreateInstance method,
// not via TypeMapAssociation attributes
```

### B.2 Java JCW (com/example/MainActivity.java)

```java
package com.example;

public class MainActivity
    extends android.app.Activity
    implements mono.android.IGCUserPeer
{
    public MainActivity() {
        super();
        if (getClass() == MainActivity.class) {
            nc_activate_0();
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

### B.3 LLVM IR Stubs (marshal_methods_MainActivity.ll)

```llvm
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8
@fn_ptr_onCreate = internal unnamed_addr global ptr null, align 8
@fn_ptr_activate = internal unnamed_addr global ptr null, align 8
; UTF-16 encoded class name (24 characters = 48 bytes, no null terminator)
@class_name = internal constant [48 x i8] c"c\00o\00m\00/\00e\00x\00a\00m\00p\00l\00e\00/\00M\00a\00i\00n\00A\00c\00t\00i\00v\00i\00t\00y\00", align 2

define default void @Java_com_example_MainActivity_n_1onCreate__Landroid_os_Bundle_2(
    ptr %env, ptr %obj, ptr %p0) #0 {
entry:
  %cached = load ptr, ptr @fn_ptr_onCreate, align 8
  %is_null = icmp eq ptr %cached, null
  br i1 %is_null, label %resolve, label %call
resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  ; 24 = character count (UTF-16 code units)
  call void %get_fn(ptr @class_name, i32 24, i32 0, ptr @fn_ptr_onCreate)
  %resolved = load ptr, ptr @fn_ptr_onCreate, align 8
  br label %call
call:
  %fn = phi ptr [ %cached, %entry ], [ %resolved, %resolve ]
  tail call void %fn(ptr %env, ptr %obj, ptr %p0)
  ret void
}

define default void @Java_com_example_MainActivity_nc_1activate_10(
    ptr %env, ptr %obj) #0 {
entry:
  %cached = load ptr, ptr @fn_ptr_activate, align 8
  %is_null = icmp eq ptr %cached, null
  br i1 %is_null, label %resolve, label %call
resolve:
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  ; 24 = character count (UTF-16 code units)
  call void %get_fn(ptr @class_name, i32 24, i32 1, ptr @fn_ptr_activate)
  %resolved = load ptr, ptr @fn_ptr_activate, align 8
  br label %call
call:
  %fn = phi ptr [ %cached, %entry ], [ %resolved, %resolve ]
  tail call void %fn(ptr %env, ptr %obj)
  ret void
}

attributes #0 = { noinline nounwind "frame-pointer"="non-leaf" }
```

---

*Document version: 2.19*
*Last updated: 2026-01-24*
*Based on PoC implementation in dotnet/android repository*
