# Using External Type Mapping API in dotnet/android

## Motivation

One of main features of Java Interop is how seamlessly developers can implement Java interfaces or extend Java classes
in their C# code and pass instances of these objects to other Java objects. A good example of this is creating a custom
Android Activity, which can be thought of as a "page" of the app, and overriding lifecycle methods of the activity.
The Android Java runtime can then calls into the C# methods when the activty is displayed or before the activity will be
closed.

**Current Solution:** The existing system uses native type maps and runtime code generation to enable this interop. At build time, `GenerateJavaStubs` generates Java stub classes and native code, while `TypeManager` provides runtime type lookup via `GetJavaToManagedType()` and instance creation through reflection-based `CreateInstance()`. However, this approach is incompatible with Native AOT due to its reliance on `Type.GetType()` calls and runtime assumptions about available reflection metadata.

**Proposed Solution:** This design leverages the new .NET 10 Type Mapping API to create a drop-in replacement for the existing system that works with both Native AOT and traditional runtimes. The goal is to maintain the same developer experience while making the underlying implementation AOT-safe.

To facilitate this interop, we have two types of C# classes relevant for this area:
1. "Managed-callable wrappers" (MCW)
    - bindings for Java classes instantiable and callable from managed code
2. "Java-callable wrappers" (JCW) or also called ACW (Android-callable wrappers)
    - .NET classes projected into Java which can be created in and called into from Java
    - Note: It is not common to declare completely new Java classes in .NET and directly call them from custom Java code,
      although it is possible and it is a supported scenario. The more common scenario is to extend an MCW and override
      its virtual methods.

Relevant docs: `dotnet/java-interop/Documentation/Architecture.md`

## Experimental gists (possibly outdated)

- Type Map experiment - mapping type workaround: https://gist.github.com/simonrozsival/cf21c475d7fb779c3c17747c75c5c266
    - workaround for https://github.com/dotnet/runtime/issues/120160

## Proposed design leveraging Type Mapping API

**Goal:** Trimming and AOT-safe design compatible with both Native AOT and Release CoreCLR (or Mono) builds. This will completely replace the existing native type lookup system - at build time, a switch will allow developers to choose between the new Type Map implementation or the legacy native implementation.

**Non-goal:** Debug builds using CoreCLR or Mono -- Mono does not support dynamic type map. We might choose to look into extending the use of Type Mapping APIs for debug builds in the future when Mono is deprecated.
**Non-goal:** Non-shipping code (meaning everything that does not target Android but "desktop JVM" - mostly in the java-interop repo) does not need to be taken into account.

### Sub-problems

1. Generating code
    - Generating IL
    - Generating Java
    - Generating native code (LLVM IR)
2. Runtime code execution
    - Annotated types in the type map for trim- and AOT-safe instantiation via reflection
    - Resolving UCO function pointers for reverse p/invokes

### Annotated types in the type map for trim- and AOT-safe instantiation via reflection

The type map proxy type will contain an annotated `Type` property for the target type. This will make sure all ACW and MCW objects can be created using reflection.

**Important:** The JNI name for each type should be read from the `[Register]` attribute (which implements `IJniNameProviderAttribute`) on the target type itself. The `[Register]` attribute instances are preserved by the linker (see `src/Microsoft.Android.Sdk.ILLink/PreserveLists/Mono.Android.xml` line 28) and are already used by other linker steps. If a type doesn't have an explicit `[Register]` attribute, fall back to deriving the JNI name using `JavaNativeTypeManager.ToJniName(type)`.

```c#
// Registered class - binds to an existing Java class "A"
[Register("A", DoNotGenerateAcw = true)]
class A : Java.Lang.Object
{
}

// Generated proxy attribute type - NOTE: applies itself as an attribute!
[A_Proxy]  // Self-application is key for AOT-safe GetCustomAttribute<JavaPeerProxy>()
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
sealed class A_Proxy : JavaPeerProxy
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    public override Type TargetType => typeof(A);
}

// Usage in runtime

abstract class JavaPeerProxy : Attribute
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    public abstract Type TargetType { get; }
}

// How it works at runtime:
// 1. TypeMapping API returns: typeof(A_Proxy)
// 2. Call: typeof(A_Proxy).GetCustomAttribute<JavaPeerProxy>()
// 3. .NET runtime instantiates the A_Proxy attribute (AOT-safe!)
// 4. Access: proxy.TargetType to get typeof(A)

public object CreateInstance(IntPtr handle, JniTransferOptions transfer, Type targetType)
{
    // 1. Get JNI class from the handle
    IntPtr javaClass = JNIEnv.GetClass(handle);
    string jniName = GetClassName(javaClass);
    
    // 2. Look up in type map - returns the PROXY type (e.g., typeof(A_Proxy))
    var externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object>();
    if (!externalTypeMap.TryGetValue(jniName, out Type proxyType)) {
        throw new InvalidOperationException($"No type mapping found for {jniName}");
    }
    
    // 3. Get the proxy instance via GetCustomAttribute (AOT-safe!)
    // This works because A_Proxy applies [A_Proxy] to itself
    JavaPeerProxy proxy = proxyType.GetCustomAttribute<JavaPeerProxy>(inherit: false);
    Type actualType = proxy.TargetType; // e.g., typeof(A)
    
    // Note: we assume that the type will be a class (invoker if interface/abstract)
    Debug.Assert(actualType.IsClass);

    // 4. Find the right constructor and invoke it
    ConstructorInfo ctor = actualType.GetConstructor([typeof(IntPtr), typeof(JniTransferOptions)]);
    return ctor.Invoke([handle, transfer]);
}
```

There is a related problem to `CreateInstace` (called from `Java.Lang.Object.GetObject<T>(IntPtr handle, JniTransferOptions transfer)`) and that is "Activation". This creates an instance of a .NET class from Java class constructor:
```java
class MyGeneratedAcw extends ...
{
    public MyGeneratedAcw(...)
    {
        if (getClass() == MyGeneratedAcw.class) {
            TypeManager.Activate(this, "The.Dot.Net.FullName.MyGeneratedAcw", ...);
        }
    }
}
```

#### Mapping interfaces to invokers

**Background:** The current `GetInvokerType()` method in `JavaObjectExtensions` uses `Assembly.GetType()` with constructed type names (e.g., "MyTypeInvoker"), making it incompatible with Native AOT. Additionally, `TypeManager.CreateInstance()` relies on `Type.GetConstructor()` calls that require proper trim annotations to preserve constructor metadata.

We might want to use the _proxy_ with a separate universe to map interfaces to their invoker interfaces:
```c#
[assembly: TypeMapAssociation<InvokerUniverse>(typeof(IA), typeof(IAInvoker))]

class InvokerUniverse;

[Register("a", DoNotGenerateAcw = true)]
interface IA : IJavaPeerable { ... }

class IAInvoker : Java.Lang.Object, IA { ... }
```

### Resolving UCO function pointers for Marshal Methods

Each proxy type stored in the type map will also contain all the Marshal Method UCOs and a method that will return the function pointer to the n-th UCO. The native code which will be generated alongside this code will ask for the matching UCO by specifying the right index.

**Performance Considerations:** This approach introduces a performance regression compared to the current system, which avoids string operations entirely in the native lookup path. The current `MonodroidRuntime::get_function_pointer` uses integer indices for O(1) lookups. The proposed solution requires string-based Java class name lookups, which will be slower.

**Alternative Solution:** We could maintain a dual type map approach - generate both the Type Map for AOT-safety and a simplified integer-indexed lookup table that reuses the current native function signature `void(*get_function_pointer)(int32_t assemblyIndex, int32_t typeIndex, int32_t methodIndex, intptr_t* fnptr)`. The string operation would only be `int.ToString()`, which is acceptable. This would require generating a secondary `"int" -> Type` mapping alongside the main Java class name mapping.

**Existing System Limitations:** The current native type map system cannot be used with the new Type Map approach because we need to generate all the code, inc. the native code, pre-trimming.

**NativeAOT Complexity:** Pre-resolving function pointers at build time is more complex than it appears. We need a solution that works for both CoreCLR and NativeAOT runtimes. Additionally, we cannot rely on dynamic linking by symbol name because it causes trimming issues with `[UnmanagedCallersOnly("entrypoint")]` methods in ILC - these methods are not trimmed away currently when they have an explicit entry point and this would effectively root all the registered types in the final code.

```c#
[assembly: TypeMap<Java.Lang.Object>("B", typeof(B_Aliases), typeof(B_Aliases))]

[assembly: TypeMap<Java.Lang.Object>("B[0]", typeof(B1_Proxy), typeof(B1))]
[assembly: TypeMapAssociation<Java.Lang.Object>(typeof(B1), typeof(B_Aliases))]

[assembly: TypeMap<Java.Lang.Object>("B[1]", typeof(B2_Proxy), typeof(B2))]
[assembly: TypeMapAssociation<Java.Lang.Object>(typeof(B2), typeof(B_Aliases))]

// Registered class - binds to an existing Java class "A"
[Register("B")]
class B1 : A
{
    public override void X(...) { ... }
}

[Register("B")]
class B2 : A
{
    // ...
}

[JavaInteropAliases("B[0]", "B[1]")] // B1 - AliasKeys[0], B2 - AliasKeys[1]
class B_Aliases;

// Generated code
[B_Proxy]
class B_Proxy : JavaPeerProxy
{
    [UnamanagedCallersOnly]
    public static void X(...)
    {
        try
        {
            var b = Java.Lang.Object.GetObject<B>(...)
            // ...
            b.X(...);
        }
        catch (Exception ex)
        {
            // ...
        }
    }

    public override IntPtr GetFunctionPointer(int methodIndex)
        => methodIndex switch
        {
            0 => (IntPtr)(delegate*<...>)&X,
            // ...
        };
}

// Usage in runtime

abstract class JavaPeerProxy // name TBD, but it's not intended for use by end developers, so it is not important
{
    // ...

    public abstract IntPtr GetFunctionPointer(int methodIndex);
}

sealed class JavaInteropAliasesAttribute(string[] aliasKeys) : Attribute
{
    public readonly string[] AliasKeys = aliasKeys;
}

partial class Resolver
{
    [UnmanagedCallersOnly]
    public static IntPtr GetFunctionPointer(IntPtr javaClass, int aliasIndex, int methodIndex)
    {
        // 1. Use javaClass to look up the corresponding type(s) in the type map
        string javaClassName = JNIEnv.GetClassName(javaClass);
        Type mappedType = s_typeMap[javaClassName];

        // 2. If there are multiple .NET types mapped to this java class, choose the right one using aliasIndex
        if (mappedType.GetCustomAttribute<JavaInteropAliasesAttribute> is {} aliasesAttribute)
        {
            string aliasKey = aliasesAttribute.AliasKeys[aliasIndex];
            mappedType = s_typeMap[aliasKey];
        }

        // 3. Use methodIndex to get the function pointer to the right UCO
        JavaPeerProxy proxy = mappedType.GetCustomAttribute<JavaPeerProxy>() ?? throw ...;
        return proxy.GetFunctionPointer(methodIndex);
    }
}
```

## Generating code

We already have code that classifies types and methods and which generates the IL of marshal methods. We need to adapt
this code to generate the right proxy classes, alias classes, and the assembly type map attributes.

We might choose to keep using Mono.Cecil and reuse as much code as possible, or take this as an opportunity to remove
Mono.Cecil from this build step.

### Generated Code Structure

Each ACW has 3 parts: the IL proxy class (with UCO methods), a .java with a matching Java class with `native` methods to invoke the UCO, native "glue" code which maps to the Java `native` methods through JNI naming convention and which lazily resolves the .NET unmanaged function pointer to the corresponding UCO and just calls the fnptr forwarding all the arguments.

Each MCW needs a matching proxy which has just a subset of what the ACW IL has. Namely, it doesn't need any UCO methods and it only has whatever is needed to create managed instance of the target type as a managed wrapper of a Java object instance.

---

## Plan

1. Build Task Boilerplate
    - Set up a build task which generates proxy classes and the corresponding type map attribues.
    - DoD: The new assembly contains all the IL we expect when viewed in ILSpy.
2. Create Peer Instances via Type Map
    - Include the new assembly in the app
    - Generate `[DynamicallyAccessedMembers(AllConstructors)] Type TargetType { get; }` for all proxy types in the typemap
    - Replace the current native type map lookups
        - Relevant code: `dotnet/android/src/Mono.Android/Java.Interop/TypeManager.cs`
            - methods: `CreateInstance`, `GetJavaToManagedTypeCore`, `JavaObjectExtensions.GetInvokerType`
    - DoD: All dotnet/android unit tests are passing.
3. Activation via Type Map
    - Modify the activation mechanism to use the type map instead of `Type.GetType`
        - Relevant code: `dotnet/android/src/Mono.Android/Java.Interop/TypeManager.cs`
            - methods: `n_Activate`
        - Potential problem: We need to find the constructor by the signature by matching against a string containing type names of individual parameters. Can we do this without `Type.GetType` by iterating over all constructors and matching the names of the params against the signature string? Are we guaranteed to have the typename metadata for all types? doesn't ILC trim it in some cases?
    - DoD: All dotnet/android unit tests are passing.
4. Generate marshal methods in type map proxy types
    - Migrate the logic from `GenerateJavaStubs` to the pre-trimming build task (inc. `.java` + `.ll` codegen).
    - Bundle the .java and native code (.ll -> .o) into the binary (also reuse the existing logic that creates the bundle)
        - Missing ILLink/ILC functionality: we need the list of types included in the typemap so we know which .java and .o files to include in the final bundle
    - DoD: All UCO methods are generated in IL as we expect when viewed in ILSpy.
5. Resolve function pointers using typemap
    - Generate "get n-th function pointer" lookup methods for all proxy types
    - Replace the function pointer lookup to use the typemap
        - Relevant code: `dotnet/android/src/native/mono/monodroid/xamarin-android-app-context.cc` - `MonodroidRuntime::get_function_pointer`
    - Problem: currently the LLVM IR code expects `void(*get_function_pointer)(int32_t assemblyIndex, int32_t typeIndex, int32_t methodIndex, intptr_t* fnptr)` and I think we will need to change this signature to `void(*get_function_pointer)(intptr_t javaClass, char* typeName, int32_t methodIndex, intptr_t* fnptr)` or something like that (we could have an `int typeIndex` and a corresponding way of getting the n-th type through the "mapping type" through the typemap)
        - Alternative: generate a separate typemap universe with a direct `"int" -> Type` mapping where each type has a unique type ID and there is no type aliasing causing problems. This will simplify the code but it will add even more generated IL and it will contribute negatively to app size.
    - DoD: All dotnet/android unit tests are passing.
6. Build performance
    - We need to make sure we re-generate as little as possible for types that come from NuGets or from the SDK. We could consider splitting the typemap across two assemblies:
        - first for 1:1 mappings in the SDK and NuGet assemblies - this one will be large
        - second for all app specific interop types and 1:N mappings incl. those from the SDK and the NuGets - this one should be small
    - What other options do we have?


### Implementation Notes

#### Getting JNI names from types

When implementing `TryGetJniNameForType()` in `TypeMapAttributeTypeMap`, use the following approach:

```csharp
public bool TryGetJniNameForType(Type type, [NotNullWhen(true)] out string? jniName)
{
    // 1. Try to get explicit JNI name from [Register] attribute (or any IJniNameProviderAttribute)
    //    Use inherit: false because each type must have its own JNI name!
    var jniNameProvider = type.GetCustomAttribute<IJniNameProviderAttribute>(inherit: false);
    if (jniNameProvider != null && !string.IsNullOrEmpty(jniNameProvider.Name)) {
        jniName = jniNameProvider.Name.Replace('.', '/');
        return true;
    }
    
    // 2. Fallback: derive JNI name using naming conventions for types without explicit [Register]
    jniName = JavaNativeTypeManager.ToJniName(type);
    return !string.IsNullOrEmpty(jniName);
}
```

**Why `inherit: false`?** Each .NET type maps to exactly one Java class with its own unique JNI name. Using `inherit: true` would cause derived classes to incorrectly inherit their base class's JNI name, breaking the Java interop entirely.

**RegisterAttribute preservation:** `RegisterAttribute` instances are preserved by the linker (see `src/Microsoft.Android.Sdk.ILLink/PreserveLists/Mono.Android.xml` line 28) and are NOT removed during linking. Only `PreserveAttribute`, `IntDefinitionAttribute`, and `GeneratedEnumAttribute` are explicitly removed via `RemoveAttributeInstances` in `ILLink.LinkAttributes.xml`.

### Open questions


- Do we want/need to support `[Export]` and/or `[JavaCallable]`?
    - Could we make it obsolete and ask developers to migrate to `[Register]`?
    - Or can we easily support both with the new typemap without needing any runtime codegen?
- Can we improve per-item value marshalling of `ArrayList` where items are primitive types (`int`)?
    - Currently we waste a lot of time by looking up all individual items of the list...? - talk to grendel once we can start looking into this, they haven't been able to fix this so far

### Notes

- We need a linker script with a list of all the `Java_*` methods so that the native linker doesn't rmeove them

---

## Future improvements

### Reflection-less wrapper object creation

Instead of calling the `(IntPtr, JniTransferOptions)` constructor via reflection, the Proxy class could override an abstract method `object CreateInstance(IntPtr, JniTransferOptions)` and simply call `new` with the target type directly:

```c#
[A_Proxy]
class A_Proxy
{
    public override object CreateInstance(IntPtr handle, JniTransferOptions transfer)
        => new A(handle, transfer);
}

// Usage in runtime

public object CreateInstance(IntPtr handle, JniTransferOptions transfer, Type targetType)
{
    // 1. Find the best matching type in the type map
    IntPtr javaClass = JNIEnv.GetClass(handle);
    JavaPeerProxy bestMatchingProxy = GetBestMatchingType(javaClass, targetType);

    // 2. Just call CreateInstance
    return bestMatchingProxy.CreateInsance(handle, transfer);
}
```

### Reflection-less ACW activation

Instead of calling into a `TypeManager.n_Activate` from Java with the constructor signature as string and the parameters,the generated Java constructors could call an UCO generated for its matching constructor and the UCO would be resolved the same way we already resolve other UCO function pointers. The only difference would be that the UCO method would have different shape from the current Marshal Method UCOs.

```c#
[A_Proxy]
class A_Proxy
{
    [UnmanagedCallersOnly]
    public static void Activate_1(IntPtr jniEnv, IntPtr javaThis, ...)
    {
        try
        {
            A instance = (A)RuntimeHelpers.GetUninitializedObject(typeof(A));
            instance.Handle = javaThis;
            // ...
            CallCtor(instance, ...); // in IL, we can just directly call the .ctor method (unless it's private?)
        }
        catch (Exception ex)
        {
            // ...
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
        extern void CallCtor(A @this, ...);
    }

    public override IntPtr GetFunctionPointer(int methodIndex)
        => methodIndex switch
        {
            ...
            5 => (IntPtr)(delegate*<IntPtr, IntPtr, ...>)&Activate_1,
            ...  
        };
}
```

---