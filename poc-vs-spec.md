# POC vs Spec: Type Mapping API v3 Implementation Analysis

This document compares the Type Mapping API v3 specification (`type-mapping-api-v3-spec.md`) against the current proof-of-concept implementation in the codebase.

## Executive Summary

The implementation covers most of the core specification features but has some notable gaps and divergences:

| Category | Status | Notes |
|----------|--------|-------|
| **Core Runtime** | ✅ Implemented | `TypeMapAttributeTypeMap`, `JavaPeerProxy`, `ITypeMap` |
| **Build Task** | ✅ Implemented | `GenerateTypeMapAssembly` runs pre-ILLink |
| **LLVM IR Generation** | ✅ Implemented | Per-type stubs, function pointer caching |
| **UCO Wrappers** | ✅ Implemented | `[UnmanagedCallersOnly]` methods in proxies |
| **Alias Handling** | ✅ Implemented | Alias holder types with indexed names |
| **Invoker Exclusion** | ✅ Implemented | Invokers excluded from TypeMap |
| **Export Attribute** | ✅ Implemented | Detection + UCO generation for `[Export]`/`[ExportField]` |
| **Implementor Routing** | ✅ Implemented | UCO wrappers route to Invoker callbacks |
| **Interface→Invoker** | ✅ Implemented | Interface `CreateInstance` returns Invoker |
| **Runtime Selection** | ✅ Implemented | Feature switches match spec |
| **TypeManager.Activate** | ✅ Implemented | Backward compat for framework JCWs |
| **TryGetInvokerType** | ❌ Intentionally Omitted | Invokers are internal implementation detail |
| **JI Constructor Style** | ✅ Implemented | Converts XI params to JI; enum value read at build time |
| **Post-Trim Filtering** | ❌ Not Implemented | All `.o` files linked, not filtered |
| **UTF-16 Class Names** | ❌ Not Implemented | Uses UTF-8 instead (minor perf impact) |
| **Blittable Params** | ⚠️ Simplified | Uses IntPtr for all params |
| **Exception String Ctor** | ⚠️ Unverified | Spec mentions explicit preservation |

---

## Detailed Analysis

### 1. ITypeMap Interface

**Spec Section 3.3** defines:
```csharp
interface ITypeMap
{
    bool TryGetTypesForJniName(string jniSimpleReference, out IEnumerable<Type>? types);
    bool TryGetInvokerType(Type type, out Type? invokerType);  // <-- MISSING
    bool TryGetJniNameForType(Type type, out string? jniName);
    IEnumerable<string> GetJniNamesForType(Type type);
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);
    IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex);
}
```

**Implementation** (`src/Mono.Android/Java.Interop/ITypeMap.cs`):
```csharp
interface ITypeMap
{
    bool TryGetTypesForJniName(string jniSimpleReference, out IEnumerable<Type>? types);
    bool TryGetJniNameForType(Type type, out string? jniName);
    IEnumerable<string> GetJniNamesForType(Type type);
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);
    IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex);
}
```

**Divergence:**
- ❌ `TryGetInvokerType` is **intentionally omitted** - Invokers are an internal implementation detail and nothing outside of our code should query them. The runtime handles invoker resolution internally via the proxy's `CreateInstance` returning the invoker for interfaces/abstract types.

---

### 2. JavaPeerProxy Base Class

**Spec Section 6.1** defines:
```csharp
abstract class JavaPeerProxy : Attribute
{
    public abstract IntPtr GetFunctionPointer(int methodIndex);
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}
```

**Implementation** (`src/Mono.Android/Java.Interop/JavaPeerProxy.cs`):
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public abstract class JavaPeerProxy : Attribute
{
    public abstract IntPtr GetFunctionPointer(int methodIndex);
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}
```

**Status:** ✅ **Matches spec exactly**

---

### 3. TypeMapAttributeTypeMap Runtime

**Spec Section 14.2** describes managed caching:

**Implementation** (`src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`):
- ✅ Uses `ConcurrentDictionary` for thread-safe caching (better than spec's `Dictionary + Lock`)
- ✅ Caches proxy instances, alias lookups, JNI name lookups, class-to-type mappings
- ✅ Uses `TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object>()` intrinsic
- ✅ Hierarchy walking for type resolution (`FindTypeInHierarchy`)
- ✅ JNI class caching with global refs

**Divergence:**
- Spec uses `Dictionary + Lock`, implementation uses `ConcurrentDictionary` (improvement)

---

### 4. Method Index Ordering Contract

**Spec Section 8** establishes:
1. Regular marshal methods: indices 0 to n-1
2. Activation constructors: indices n to m-1

**Implementation** (`GenerateTypeMapAssembly.cs`):

In `GenerateUcoWrappers`:
```csharp
// Step 1: Generate UCO wrappers for regular marshal methods (n_methodName)
// These come FIRST in the LLVM IR stub index order (indices 0..n-1)
var regularMethods = peer.RegularMarshalMethods;
// ... generates these first

// Step 2: Generate UCO wrappers for activation constructors (nc_activate_X)
// These come SECOND in the LLVM IR stub index order (indices n..m-1)
```

In `GenerateLlvmIrFile`:
```csharp
// Total function pointers: regular methods + activation methods
int totalFnPointers = regularMethods.Count + numActivateMethods;

// Generate regular method stubs (indices 0..n-1)
for (int i = 0; i < regularMethods.Count; i++) { ... }

// Generate nc_activate stubs (indices n..m-1)
int activateBaseIndex = regularMethods.Count;
```

**Status:** ✅ **Both generators follow the same ordering contract**

---

### 5. LLVM IR Generation

**Spec Section 11** describes:
- Per-method stub template with cached function pointers
- UTF-16 class names for direct `ReadOnlySpan<char>` access
- `default` visibility for JNI symbols

**Spec callback signature:**
```c
void (*typemap_get_function_pointer)(
    const char16_t* className,  // UTF-16 Java class name (NOT null-terminated)
    int32_t classNameLength,    // Length in char16_t units
    int32_t methodIndex,
    intptr_t* fnptr
);
```

**Implementation** (`GenerateTypeMapAssembly.cs:2407-2624`, `xamarin-app.hh:375`):
- ✅ Per-type `.ll` files with cached `@fn_ptr_N` globals
- ❌ Uses **UTF-8** class names (`const char* class_name`)
- ✅ `define default void @Java_...` visibility
- ✅ `marshal_methods_init.ll` declares `@typemap_get_function_pointer`

**Native callback signature (actual):**
```cpp
using get_function_pointer_typemap_fn = void(*)(
    const char* class_name,      // UTF-8 instead of UTF-16
    int32_t class_name_length,
    int32_t method_index,
    void** target_ptr
);
```

**Divergence:**
- Spec calls for UTF-16 class names, implementation uses UTF-8
- Runtime does `className.ToString()` to convert span to string (allocation)
- This means the managed side allocates on every lookup (minor overhead, cached after first call)

**Impact:** Minor performance concern on first lookup per type, but functional. The allocation is mitigated by LLVM IR caching at the native layer.

---

### 6. UCO Wrapper Generation

**Spec Section 12** describes:
```csharp
[UnmanagedCallersOnly]
public static void n_{MethodName}_mm_{Index}(IntPtr jnienv, IntPtr obj, ...)
{
    AndroidRuntimeInternal.WaitForBridgeProcessing();
    try {
        TargetType.n_{MethodName}_{JniSignature}(jnienv, obj, ...);
    } catch (Exception ex) {
        AndroidEnvironmentInternal.UnhandledException(jnienv, ex);
    }
}
```

**Spec Section 12.3** - Blittable parameter handling:
- `[UnmanagedCallersOnly]` requires blittable parameters
- Replace `bool` with `byte` in signatures

**Implementation** (`GenerateTypeMapAssembly.cs:3785-3812, 3896-3900`):
- ✅ Generates `[UnmanagedCallersOnly]` methods
- ✅ Calls `WaitForBridgeProcessing()` (referenced but may not be called for all methods)
- ✅ Has exception handling with try/catch
- ✅ Uses `GetUninitializedObject` for activation
- ⚠️ Uses `IntPtr` for ALL parameters instead of proper JNI types
- ⚠️ Uses `RaiseThrowable(FromException(ex))` instead of `UnhandledException`

**Parameter handling divergence:**
```csharp
// Spec says use proper JNI-blittable types:
public static void n_setEnabled_mm_0(IntPtr jnienv, IntPtr obj, byte enabled)

// Implementation uses IntPtr for everything:
sigEncoder.Parameters(paramCount, ..., parameters => {
    parameters.AddParameter().Type().IntPtr();  // jnienv
    parameters.AddParameter().Type().IntPtr();  // obj
    for (int p = 2; p < paramCount; p++) {
        parameters.AddParameter().Type().IntPtr();  // ALL additional params are IntPtr
    }
});
```

**Impact:** The IntPtr-for-all approach works because JNI uses pointer-sized values, but:
- Loss of type safety in generated IL
- May need adjustment for proper bool/byte handling if JNI passes 1-byte values

---

### 7. Activation Constructor Handling

**Spec Section 9** describes:
- XI constructor: `(IntPtr handle, JniHandleOwnership transfer)`
- JI constructor: `(ref JniObjectReference, JniObjectReferenceOptions)`
- `[UnsafeAccessor]` for protected base class constructors

**Implementation** (`GenerateTypeMapAssembly.cs:1287-1325`):
```csharp
ActivationConstructorStyle GetActivationConstructorStyle(MetadataReader reader, TypeDefinition typeDef)
{
    // Check for XI style first: (IntPtr, JniHandleOwnership)
    if (p0 == "System.IntPtr" && p1 == "Android.Runtime.JniHandleOwnership") {
        return ActivationConstructorStyle.XI;
    }
    // Then check for JI style: (ref JniObjectReference, JniObjectReferenceOptions)
    if ((p0 == "Java.Interop.JniObjectReference&" || p0 == "Java.Interop.JniObjectReference") &&
        p1 == "Java.Interop.JniObjectReferenceOptions") {
        return ActivationConstructorStyle.JI;
    }
    return ActivationConstructorStyle.None;
}
```

- ✅ `ActivationConstructorStyle` enum: `None`, `XI`, `JI`
- ✅ `ActivationCtorBaseTypeName` for inherited constructors
- ✅ `GenerateUnsafeAccessorMethod` generates correct signature for both styles
- ✅ `GenerateCreateInstanceBody` emits correct IL for JI style (with ref param + cleanup)
- ✅ `JniObjectReferenceOptions.Copy` value read from Java.Interop assembly at build time

**JI Constructor IL Pattern:**
```csharp
// Matches legacy LlvmIrTypeMap.CreateProxy pattern
var reference = new JniObjectReference(handle);
var result = CreateInstanceUnsafe(ref reference, JniObjectReferenceOptions.Copy);
JNIEnv.DeleteRef(handle, transfer);
return result;
```

**Build-time Enum Reading:**
The `JniObjectReferenceOptions.Copy` value is read from the Java.Interop assembly during scanning, not hardcoded, ensuring it stays in sync if the enum definition changes.

**Status:** ✅ **Fully matches spec**

---

### 8. Alias Handling

**Spec Section 13** describes indexed names: `com/example/Handler[0]`, `com/example/Handler[1]`

**Implementation** (`GenerateTypeMapAssembly.cs:2186-2262`):
```csharp
if (peers.Count > 1) {
    aliasHolderName = peers[0].ManagedTypeName... + "_Aliases";
    GenerateAliasHolderType(aliasHolderName);
    typeMapAttrs.Add((jniName, qualifiedAliasHolderName, ...));
}

// Later:
string entryJniName = peers.Count > 1 ? $"{jniName}[{i}]" : jniName;
```

**Status:** ✅ **Matches spec exactly**

---

### 9. Invoker Exclusion (Appendix A.1)

**Spec Appendix A.1** states Invokers should be excluded from TypeMap.

**Implementation** (`GenerateTypeMapAssembly.cs:2359-2377`):
```csharp
bool IsInvokerType(JavaPeerInfo peer)
{
    if (!peer.DoNotGenerateAcw)
        return false;
    return peer.ManagedTypeName.EndsWith("Invoker", StringComparison.Ordinal);
}
```

And in `Generate()`:
```csharp
var filteredPeers = javaPeers.Where(p => !IsInvokerType(p)).ToList();
```

**Status:** ✅ **Implemented per spec**

---

### 9b. Implementor Callback Routing (Appendix A.3)

**Spec Appendix A.3** describes that Implementor UCO wrappers must call Invoker's static callback methods.

**Implementation** (`GenerateTypeMapAssembly.cs:304-309, 3842-3848`):

`MarshalMethodInfo` stores callback type info:
```csharp
public string? CallbackTypeName { get; set; }      // e.g., "IOnClickListenerInvoker"
public string? CallbackAssemblyName { get; set; }  // e.g., "Mono.Android"
```

UCO wrapper generation routes to the correct callback:
```csharp
if (!mm.CallbackTypeName.IsNullOrEmpty() && !mm.CallbackAssemblyName.IsNullOrEmpty()) {
    // Callback is in a different type (e.g., IOnClickListenerInvoker)
    callbackTypeRef = GetOrAddTypeReference(mm.CallbackTypeName!, mm.CallbackAssemblyName!);
} else {
    // Callback is in the target type itself
    callbackTypeRef = targetTypeRef;
}
```

**Status:** ✅ **Implemented per spec**

---

### 10. Type Detection Rules (Section 15.8)

**Spec defines:**
- Unconditional: User Android components, custom views from XML
- Trimmable: Interfaces, Implementors, MCW types (DoNotGenerateAcw=true)

**Implementation** (`GenerateTypeMapAssembly.cs:2248-2250`):
```csharp
bool isImplementor = peer.ManagedTypeName.EndsWith("Implementor", StringComparison.Ordinal);
bool isCustomView = _customViewTypes.Contains(peer.ManagedTypeName);
bool isUnconditional = isCustomView || (!peer.DoNotGenerateAcw && !peer.IsInterface && !isImplementor);
```

**Status:** ✅ **Matches spec logic exactly**

---

### 10b. Interface CreateInstance Returns Invoker (Section 5.3)

**Spec Section 5.3** describes interface proxies returning Invokers:
```csharp
public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
{
    // Directly create Invoker instance
    return new IOnClickListenerInvoker(handle, transfer);
}
```

**Implementation** (`GenerateTypeMapAssembly.cs:4447-4470`):
```csharp
if ((peer.IsInterface || peer.IsAbstract) && !string.IsNullOrEmpty(peer.InvokerTypeName)) {
    // For Interfaces/Abstract types, instantiate the Invoker
    var invokerTypeRef = AddExternalTypeReference(peer.InvokerAssemblyName!, peer.InvokerTypeName!);
    // ... generate: newobj InvokerType::.ctor(IntPtr, JniHandleOwnership)
}
```

**Status:** ✅ **Implemented per spec**

---

### 11. Native Callback Wiring

**Spec Section 11.2** describes `typemap_get_function_pointer` callback:
```c
void (*typemap_get_function_pointer)(
    const char16_t* className,
    int32_t classNameLength,
    int32_t methodIndex,
    intptr_t* fnptr
);
```

**Implementation** (`src/native/clr/include/xamarin-app.hh:374-376`):
```cpp
extern "C" [[gnu::visibility("default")]] get_function_pointer_typemap_fn typemap_get_function_pointer;
```

And in `src/native/clr/host/host.cc:567`:
```cpp
typemap_get_function_pointer = init.getFunctionPointerFn;
```

**Status:** ✅ **Native callback exists and is wired**

---

### 12. Export Attribute Support

**Spec Section 10 and Appendix A.6** state `[Export]` should be handled like `[Register]`.

**Implementation** (`GenerateTypeMapAssembly.cs:1557-1604`):
- ✅ `IsExportAttribute` detection exists
- ✅ `IsExportFieldAttribute` detection exists
- ✅ `GetMethodExportAttribute` extracts export name
- ✅ JNI signature derived from .NET method signature via `BuildJniMethodSignature`
- ✅ Native callback names generated as `n_{exportName}`
- ✅ `[ExportField]` handled with getter methods

**Status:** ✅ **Fully implemented per spec**

Example from code:
```csharp
string? exportName = GetMethodExportAttribute(reader, method);
if (exportName != null) {
    string jniSignature = BuildJniMethodSignature(paramTypes, returnType);
    methods.Add(new MarshalMethodInfo {
        JniName = exportName,
        JniSignature = jniSignature,
        NativeCallbackName = $"n_{exportName}",
        ...
    });
}
```

---

### 13. Exception Handling Preservation

**Spec Section 15.7.7** mentions `PreserveJavaExceptions` step replacement:
> Exception proxy types call the string constructor

**Implementation:**
- The spec describes that proxy types should directly reference exception constructors
- Need to verify if exception proxies are generated with string ctor references

---

### 14. Application Associated Types

**Spec Section 15.7.6** describes `TypeMapAssociationAttribute` for BackupAgent/ManageSpaceActivity.

**Implementation** (`GenerateTypeMapAssembly.cs:2296-2309`):
```csharp
// 8c. Generate TypeMapAssociation attributes for BackupAgent/ManageSpaceActivity
foreach (var peer in javaPeers) {
    if (peer.AssociatedTypes.Count > 0) {
        foreach (var associatedType in peer.AssociatedTypes) {
            AddApplicationAssociationAttribute(peer.ManagedTypeName, associatedType);
            associationCount++;
        }
    }
}
```

And scanning (`GenerateTypeMapAssembly.cs:846-848`):
```csharp
if (attrTypeName == "Android.App.ApplicationAttribute") {
    associatedTypes = GetTypePropertyValues(reader, attr, "BackupAgent", "ManageSpaceActivity");
}
```

**Status:** ✅ **Implemented per spec**

---

## Summary of Gaps/Divergences

### Missing Features:
1. **`TryGetInvokerType`** - Intentionally omitted. Invokers are an internal implementation detail; nothing outside our code should query them.
2. **Post-Trimming Filtering** - All `.o` files linked instead of filtering for surviving types only

### Divergences:
1. **Class name encoding** - Spec says UTF-16, implementation uses UTF-8 (causes string allocation on lookup)
2. **Caching mechanism** - Spec uses `Dictionary + Lock`, implementation uses `ConcurrentDictionary` (improvement)
3. **Exception handling** - Spec uses `UnhandledException`, impl uses `RaiseThrowable(FromException())`
4. **UCO parameter types** - Spec uses proper JNI-blittable types, impl uses `IntPtr` for all parameters

### Unverified:
1. **Exception string constructor preservation** - need to verify proxy generation for exception types

---

## Additional Implementation Details

### 15. ILLink Custom Steps Status

**Spec Section 15.7** provides detailed analysis of ILLink step replacements.

| Step | Spec Replacement | Implementation Status |
|------|------------------|----------------------|
| `MarkJavaObjects` | TypeMap unconditional attrs | ✅ Implemented via `isUnconditional` flag |
| `PreserveJavaInterfaces` | Proxy class refs | ✅ UCO wrappers call interface methods |
| `PreserveRegistrations` | Proxy class refs | ✅ UCO wrappers call handler methods |
| `PreserveApplications` | TypeMapAssociationAttr | ✅ `AssociatedTypes` handling |
| `PreserveJavaExceptions` | Proxy string ctor refs | ⚠️ Not explicitly verified |
| `PreserveExportedTypes` | TypeMap unconditional attrs | ✅ `[Export]` collected |

### 16. Runtime Selection and Feature Switches

**Spec Section 3.4-3.5** describes runtime selection.

**Implementation** (`RuntimeFeature.cs`, `JNIEnvInit.cs:229-241`):

Feature switches (exactly as spec):
```csharp
// RuntimeFeature.cs
[FeatureSwitchDefinition(...)]
internal static bool IsMonoRuntime { get; }  // Default: true
internal static bool IsCoreClrRuntime { get; }  // Default: false
```

Runtime selection:
```csharp
// JNIEnvInit.CreateTypeMap()
if (RuntimeFeature.IsCoreClrRuntime) {
    return new TypeMapAttributeTypeMap();
} else if (RuntimeFeature.IsMonoRuntime) {
    return new LlvmIrTypeMap();
}
```

**Status:** ✅ **Matches spec exactly**

### 17. TypeManager.Activate Backward Compatibility

**Not in spec but implemented:**

The implementation maintains backward compatibility with legacy framework JCWs (e.g., `mono/android/TypeManager`) that call `TypeManager.activate()`:

```csharp
// TypeMapAttributeTypeMap.GetFunctionPointer()
if (classNameStr == "mono/android/TypeManager" && methodIndex == 0) {
    result = Java.Interop.TypeManager.GetActivateFunctionPointer();
}
```

This returns a function pointer to `n_Activate_mm`:
```csharp
[UnmanagedCallersOnly]
internal static void n_Activate_mm(IntPtr jnienv, IntPtr jclass, ...)
{
    try {
        TypeManager.n_Activate(jnienv, jclass, ...);
    } catch (Exception ex) {
        AndroidEnvironment.UnhandledException(ex);
    }
}
```

**Impact:** Allows gradual migration - framework JCWs can still use the old activation path while user types use the new proxy-based activation.

### 18. Build Pipeline Integration

**Spec Section 15** describes the build pipeline.

**Implementation:**
- ✅ `GenerateTypeMapAssembly` task runs before ILLink
- ✅ Generates `_Microsoft.Android.TypeMaps.dll`
- ✅ Generates JCW `.java` files
- ✅ Generates LLVM IR `.ll` files per type
- ✅ `CustomViewMapFile` input for layout XML types
- ❌ **Post-trimming filtering NOT implemented** - ALL `.ll` files are linked, not just surviving types

**Divergence from Spec:**

The spec (Section 15.3) describes:
> After trimming, scan surviving assemblies and filter:
> 1. **Determine surviving types:** Scan trimmed assemblies for types with `[Register]` still present
> 2. **Filter .o files:** Only link `marshal_methods_{TypeHash}.o` for surviving types
> 3. **Generate ProGuard config:** Only emit `-keep class` rules for surviving types

Current implementation (`Microsoft.Android.Sdk.ILLink.targets:202-207`):
```xml
<_TypeMapMarshalMethodsSource Include="$(_AndroidMarshalMethodsDir)marshal_methods_*.ll" />
```
This includes **ALL** generated marshal methods files without filtering.

**Impact:**
- Larger native library size in release builds (dead JNI stubs not removed)
- ProGuard config (`GenerateProguardConfiguration.cs`) still runs as ILLink step but generates rules for ALL types

**Required Work:**
1. Add post-trimming MSBuild task to:
   - Scan trimmed assemblies for surviving `[Register]` types
   - Filter `.ll`/`.o` files to only those matching surviving types
2. Move `GenerateProguardConfiguration` from ILLink step to post-trimming MSBuild task

### 19. Performance Optimizations

**Spec Appendix A.8** mentions:
- Parallel assembly scanning: ✅ Implemented via `Parallel.ForEach`
- Invoker exclusion: ✅ 20% reduction implemented
- Two-layer caching: ✅ Native (LLVM globals) + managed (ConcurrentDictionary)

### 20. Error Handling (Section 16)

**Spec Section 16** defines error behaviors.

**Build-time errors:**
| Error | Status | Implementation |
|-------|--------|----------------|
| XA4212 (custom IJavaObject) | ⚠️ Disabled | `// XA4212 check disabled for PoC` |
| No activation ctor | ✅ Implemented | `EmitThrowNotSupported(...)` |

**Runtime error behaviors:**
| Scenario | Spec | Implementation |
|----------|------|----------------|
| Unknown class name | Return `IntPtr.Zero` | ✅ `if (!TryGetValue(...)) result = IntPtr.Zero` |
| Invalid method index | Return `IntPtr.Zero` | ✅ `default: return IntPtr.Zero` |
| Interface GetFunctionPointer | Throw | ✅ `EmitThrowNotSupported(...)` |
| No activation ctor | Throw | ✅ `EmitThrowNotSupported(...)` |

---

## Recommendations

### High Priority:
1. **Post-Trimming Filtering Task** - Implement MSBuild task to filter `.o` files based on surviving types. Currently ALL marshal methods are linked, wasting binary size in release builds.

### Medium Priority:
2. **ProGuard Integration** - Convert `GenerateProguardConfiguration` from ILLink step to post-trimming MSBuild task that generates rules only for surviving types.

### Low Priority:
3. **UTF-16 Class Names** - Consider UTF-16 class names in LLVM IR for zero-allocation span access (spec recommendation).

4. **Document Improvements** - Document that ConcurrentDictionary is an intentional improvement over spec's Lock approach.

5. **Verify Exception Handling** - Ensure exception type proxy generation includes string constructor references.

6. **Proper JNI Blittable Types** - Consider using proper JNI-blittable parameter types in UCO wrappers instead of IntPtr for all (improves type safety).

---

## Implementation Completeness Summary

Based on the spec's Section 17 Implementation Checklist:

### 17.1 Core Infrastructure
- [x] MSBuild task: `GenerateTypeMaps` → `GenerateTypeMapAssembly`
- [x] Assembly scanning for Java peers
- [x] TypeMap attribute generation
- [x] Proxy type generation with UCO methods
- [x] GetFunctionPointer switch statements
- [x] CreateInstance factory methods

### 17.2 Constructor Handling
- [x] UnsafeAccessor for protected/base constructors
- [x] XI constructor support (fully implemented)
- [x] JI constructor support (fully implemented with `JniTypeConversionHelpers`)
- [x] Constructor search up hierarchy

### 17.3 Native Integration
- [x] LLVM IR stub generation
- [x] JCW Java file generation
- [x] Implementor JCW + UCO generation
- [x] Function pointer callback wiring

### 17.4 Performance
- [ ] SDK type pre-generation and caching (not implemented)
- [ ] NuGet package type caching (not implemented)
- [x] Parallel assembly scanning

### 17.5 Validation
- [ ] Full trimming validation (`TrimMode=full`)
- [ ] Performance benchmarks
- [ ] Test suite

---

*Last updated: 2026-01-27*
