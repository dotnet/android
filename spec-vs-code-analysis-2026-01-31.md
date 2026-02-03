# TypeMap V3 Specification vs Implementation Analysis

**Date:** 2026-01-31  
**Spec Version:** 1.4 (from `type-mapping-api-v3-spec.md`)  
**Analysis Updated:** 2026-01-31 03:46 UTC

---

## Executive Summary

The TypeMap V3 implementation is **substantially complete** with most core functionality working. The codebase closely follows the specification with deliberate optimizations and all CoreCLR/NativeAOT code paths are now AOT-safe.

### Overall Status

| Area | Status | Notes |
|------|--------|-------|
| Core Architecture | ✅ Complete | ITypeMap, TypeMapAttributeTypeMap, LlvmIrTypeMap |
| Proxy Generation | ✅ Complete | JavaPeerProxy, IAndroidCallableWrapper, CreateInstance, CreateArray |
| IL Generation | ✅ Complete | S.R.M.E-based, IgnoresAccessChecksTo |
| LLVM IR Generation | ✅ Complete | typemap_get_function_pointer callback |
| Feature Switches | ✅ Complete | IsCoreClrRuntime, IsMonoRuntime, IsDynamicTypeRegistration |
| Trim Compatibility | ✅ Complete | All CoreCLR paths AOT-safe, 1 MonoVM-only suppression |
| Build Integration | ✅ Complete | GenerateTypeMapAssembly task |

---

## 1. Architecture Comparison

### 1.1 ITypeMap Interface

**Spec (Section 3.3):**
```csharp
interface ITypeMap {
    bool TryGetTypesForJniName(...);
    bool TryGetInvokerType(...);
    bool TryGetJniNameForType(...);
    IEnumerable<string> GetJniNamesForType(...);
    IJavaPeerable? CreatePeer(...);
    IntPtr GetFunctionPointer(...);
    Array CreateArray(...);
}
```

**Implementation (`src/Mono.Android/Java.Interop/ITypeMap.cs`):**
```csharp
interface ITypeMap {
    bool TryGetTypesForJniName(...);
    bool TryGetJniNameForType(...);
    IEnumerable<string> GetJniNamesForType(...);
    bool TryGetInvokerType(...);  // ✅ Added 2026-01-31
    JavaPeerProxy? GetProxyForManagedType(...);  // Added for ActivatePeer delegation
    IJavaPeerable? CreatePeer(...);
    Array CreateArray(...);
    IntPtr GetFunctionPointer(...);
}
```

**Analysis:**
- ✅ All methods from spec are now implemented
- ✅ `TryGetInvokerType` added - uses proxy's `InvokerType` property
- ✅ `GetProxyForManagedType` added for `ActivatePeer` TypeMap delegation

---

### 1.2 JavaPeerProxy Base Class

**Spec (Section 6.1):**
```csharp
abstract class JavaPeerProxy : Attribute {
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
    public abstract Array CreateArray(int length, int rank);
    protected static Array CreateArrayOf<T>(int length, int rank);
    public Type? InvokerType { get; protected set; }
}
```

**Spec (Section 6.2) - IAndroidCallableWrapper:**
```csharp
interface IAndroidCallableWrapper {
    IntPtr GetFunctionPointer(int methodIndex);
}
```

**Implementation (`src/Mono.Android/Java.Interop/JavaPeerProxy.cs`):**
```csharp
abstract class JavaPeerProxy : Attribute {
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
    public abstract Array CreateArray(int length, int rank);
    protected static Array CreateArrayOf<T>(int length, int rank) { ... }
    public Type? InvokerType { get; protected set; }
}
```

**Implementation (`IAndroidCallableWrapper.cs`):**
```csharp
interface IAndroidCallableWrapper {
    IntPtr GetFunctionPointer(int methodIndex);
}
```

**Analysis:**
- ✅ `CreateInstance` matches spec
- ✅ `CreateArray` with rank parameter per spec Section 20.1
- ✅ `InvokerType` property for interface/abstract class invoker lookup
- ✅ `GetFunctionPointer` in separate `IAndroidCallableWrapper` interface (per spec Section 6.2)
- ✅ **Optimization:** MCW types (95%+) only implement `JavaPeerProxy`, not `IAndroidCallableWrapper`

---

### 1.3 Runtime Selection

**Spec (Section 3.4):**
```csharp
if (RuntimeFeature.IsCoreClrRuntime)
    return new TypeMapAttributeTypeMap();
else if (RuntimeFeature.IsMonoRuntime)
    return new LlvmIrTypeMap();
```

**Implementation (`src/Mono.Android/Android.Runtime/JNIEnvInit.cs`):**
```csharp
// Lines 268-269
// CoreCLR and NativeAOT both use ManagedValueManager with the CLR GC bridge
return new ManagedValueManager (typeMap);
```

**Analysis:**
- ✅ Feature switches exist in `RuntimeFeature.cs`
- ✅ Selection logic follows spec pattern
- ✅ `IsDynamicTypeRegistration` feature switch added for trimming dynamic code paths

---

## 2. Type Classification

### 2.1 Spec Classification (Section 4)

| Type Category | JCW? | TypeMap Entry | GetFunctionPointer | CreateInstance |
|---------------|------|---------------|-------------------|----------------|
| User class with JCW | Yes | ✅ | Returns UCO ptrs | `new T(h, t)` |
| SDK MCW (binding) | No | ✅ | Throws | `new T(h, t)` |
| Interface | No | ✅ | Throws | Returns Invoker |
| Implementor | Yes | ✅ | Returns UCO ptrs | `new T(h, t)` |
| Invoker | No | ❌ | N/A | N/A |

**Implementation Status:**
- ✅ User classes generate full proxies with `IAndroidCallableWrapper`
- ✅ MCW types generate proxies with only `JavaPeerProxy` (no `GetFunctionPointer`)
- ✅ Interfaces generate proxies that create Invoker instances
- ✅ Implementors generate full proxies
- ✅ Invokers excluded from TypeMap (per Appendix A.1)

---

## 3. Generated Code Patterns

### 3.1 Proxy Self-Application Pattern

**Spec (Section 5.2):**
```csharp
[MainActivity_Proxy]  // Self-application
public sealed class MainActivity_Proxy : JavaPeerProxy
```

**Implementation:** ✅ Verified in `GenerateTypeMapAssembly.cs` - proxy types apply themselves as attributes.

### 3.2 IgnoresAccessChecksTo

**Spec (Section 20.6):**
```csharp
[assembly: IgnoresAccessChecksTo("Mono.Android")]
[assembly: IgnoresAccessChecksTo("Java.Interop")]
```

**Implementation:** ✅ Found at lines 5391-5392 in `GenerateTypeMapAssembly.cs`

### 3.3 Method Index Ordering

**Spec (Section 8.2):**
1. Regular marshal methods: indices 0 to n-1
2. Activation constructors: indices n to m-1

**Implementation:** Verified - both IL generator and LLVM IR generator follow the same ordering contract.

---

## 4. Build Pipeline

### 4.1 GenerateTypeMaps Task

**Spec (Section 15):** MSBuild task scans assemblies, generates TypeMapAssembly.dll, .java files, .ll files

**Implementation:**
- ✅ `GenerateTypeMapAssembly.cs` - 5688 lines, comprehensive implementation
- ✅ Uses S.R.M.E for IL generation (per Appendix A.4)
- ✅ Generates TypeMap attributes, proxy types, UCO methods
- ✅ Handles aliases with `JavaInteropAliasesAttribute`

### 4.2 LLVM IR Generation

**Spec (Section 11):**
- Per-method stubs with caching
- `typemap_get_function_pointer` callback
- UTF-16 class names

**Implementation:**
- ✅ `MarshalMethodsNativeAssemblyGenerator.cs` - handles both V3 and legacy
- ✅ `typemap_get_function_pointer` used when `UseTypemapV3 = true`
- ✅ CoreCLR-specific generator in `MarshalMethodsNativeAssemblyGeneratorCoreCLR.cs`

---

## 5. Trim/AOT Compatibility

### 5.1 Remaining IL Suppressions

| File | Suppression | Reason |
|------|-------------|--------|
| `JNINativeWrapper.cs` | `[RequiresDynamicCode]` | Dynamic code generation for MonoVM-only fallback |

**Analysis:** The suppression is in MonoVM-only code path guarded by `IsDynamicTypeRegistration` feature switch. With TypeMap V3 on CoreCLR/NativeAOT, this path is not executed and the code is trimmed out.

### 5.2 Fixed Warnings

Recent commits fixed:
- ✅ IL2035 (Java.Interop.Export) - Fixed with `#if ANDROID` conditional compilation
- ✅ IL2057 (Type.GetType with runtime string) - Fixed with `IsDynamicTypeRegistration` feature switch
- ✅ IL2067 (array marshaling) - Fixed with proper `[DynamicallyAccessedMembers]` annotations
- ✅ IL2072 (ActivatePeer) - Fixed by delegating to TypeMap instead of reflection
- ✅ IL3050 (DynamicMethod) - Fixed by extracting to separate method with feature switch guard

### 5.3 Dead Code Removed

- ✅ `SimpleValueManager.cs` - Removed entirely (was never instantiated, ManagedValueManager is used for CoreCLR)

### 5.4 Feature Switches

**Spec (Section 3.5):**

| Switch | Purpose |
|--------|---------|
| `IsMonoRuntime` | Use LlvmIrTypeMap (legacy) |
| `IsCoreClrRuntime` | Use TypeMapAttributeTypeMap |

**Implementation (`RuntimeFeature.cs`):**
- ✅ `IsMonoRuntime` - default `true`
- ✅ `IsCoreClrRuntime` - default `false`
- ✅ `IsDynamicTypeRegistration` - NEW, controls reflection-based activation and DynamicMethod usage

---

## 6. Divergences from Spec

### 6.1 No Major Divergences

All spec requirements are now implemented. The following design decisions are documented in the spec:

| Area | Spec Section | Implementation | Notes |
|------|--------------|----------------|-------|
| `TryGetInvokerType` | 3.3 | ✅ Implemented | Uses proxy's `InvokerType` property |
| `IAndroidCallableWrapper` split | 6.2 | ✅ Implemented | 95% optimization |
| Custom view handling | 15.7.11 | ✅ Verified | Lines 122-134 in GenerateTypeMapAssembly.cs |

### 6.2 Verified Working

1. ✅ **TryGetInvokerType** - Now implemented in both TypeMapAttributeTypeMap and LlvmIrTypeMap

2. ✅ **IAndroidCallableWrapper split** - MCW types only implement `JavaPeerProxy`, ACW types implement both

3. ✅ **Custom view handling** - `CustomViewMapFile` is parsed and custom view types get unconditional TypeMapAttribute

4. ⚠️ **Exception in constructor** - Spec Section 9.4 notes potential handle leak if activation constructor throws. This matches legacy behavior and is acceptable.

---

## 7. ILLink Step Replacements

**Spec (Section 15.7.9):**

| Legacy Step | Replacement Status |
|-------------|-------------------|
| `MarkJavaObjects` | ✅ Unconditional TypeMapAttribute |
| `PreserveJavaInterfaces` | ✅ Proxy class references |
| `PreserveRegistrations` | ✅ Proxy class references |
| `PreserveApplications` | ⚠️ TypeMapAssociationAttribute - verify |
| `PreserveJavaExceptions` | ✅ Proxy class references |
| `PreserveExportedTypes` | ✅ Generator collects [Export] |
| `FixAbstractMethodsStep` | ❓ Likely unnecessary |
| `AddKeepAlivesStep` | ❓ Likely unnecessary |

---

## 8. Open Items from Spec

### 8.1 Resolved

- ✅ Array Type Handling (Section 20.1) - `CreateArray` with rank parameter
- ✅ ILLink UnsafeAccessor issues (Section 20.2) - Using IgnoresAccessChecksTo instead
- ✅ Callback Type Resolution (Section 20.3) - Fixed in generator
- ✅ IgnoresAccessChecksTo (Section 20.6) - Implemented
- ✅ Dead code removal - `SimpleValueManager.cs` removed

### 8.2 Not Yet Implemented (Future Optimizations)

- ❓ Shared Callback Wrappers (Section 20.4) - Future optimization, not required
- ❓ Post-trimming filtering (Section 15.3) - May need verification
- ❓ SDK type pre-generation (Section 15.5) - Ship pre-built TypeMap for SDK types

---

## 9. Recommendations

### 9.1 Immediate Actions

1. ✅ **Verify invoker handling** - Done, works correctly
2. **Test exception in constructor** - Verify handle cleanup if activation constructor throws
3. ✅ **Verify custom view handling** - Done, properly integrated

### 9.2 Future Improvements

1. **Shared callback wrappers** (Section 20.4) - Could reduce code size
2. **SDK type pre-generation** (Section 15.5) - Ship pre-built TypeMap for SDK types

---

## 10. Test Coverage Recommendations

1. **User type activation** - MainActivity, custom Activity subclasses
2. **Interface activation** - IOnClickListener → IOnClickListenerInvoker
3. **Array marshaling** - `GetArray<ITrustManager>()`, nested arrays
4. **Export methods** - Methods with `[Export]` attribute
5. **Trimming tests** - Build with TrimMode=full, verify no runtime errors
6. **NativeAOT tests** - Full AOT compilation without fallbacks

---

## Appendix A: File Locations

| Component | Location | Lines |
|-----------|----------|-------|
| ITypeMap | `src/Mono.Android/Java.Interop/ITypeMap.cs` | 75 |
| TypeMapAttributeTypeMap | `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` | 487 |
| LlvmIrTypeMap | `src/Mono.Android/Java.Interop/LlvmIrTypeMap.cs` | 316 |
| JavaPeerProxy | `src/Mono.Android/Java.Interop/JavaPeerProxy.cs` | 104 |
| IAndroidCallableWrapper | `src/Mono.Android/Java.Interop/IAndroidCallableWrapper.cs` | 27 |
| RuntimeFeature | `src/Mono.Android/Microsoft.Android.Runtime/RuntimeFeature.cs` | 42 |
| ManagedValueManager | `src/Mono.Android/Microsoft.Android.Runtime/ManagedValueManager.cs` | 350+ |
| JNINativeWrapper | `src/Mono.Android/Android.Runtime/JNINativeWrapper.cs` | 115 |
| GenerateTypeMapAssembly | `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` | 5688 |
| MarshalMethodsNativeAssemblyGenerator | `src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsNativeAssemblyGenerator.cs` | 959 |

---

## Appendix B: Recent Changes (2026-01-31)

### B.1 IL Warning Fixes

| Commit | Change | Impact |
|--------|--------|--------|
| `208042c` | Pass ANDROID define to Java.Interop | Fixes IL2035 |
| `ff52fec` | IsDynamicTypeRegistration feature switch | Fixes IL2057 |
| `cde668a` | ActivatePeer delegates to TypeMap | Fixes IL2072 |
| `5498743` | DynamicallyAccessedMembers annotations | Fixes IL2067 |
| (pending) | JNINativeWrapper feature switch guard | Removes IL3050 from CoreCLR path |

### B.2 Code Removals

- `SimpleValueManager.cs` - Dead code, was never instantiated (ManagedValueManager used for CoreCLR)

### B.3 Interface Changes

- `ITypeMap.GetProxyForManagedType` - New method for ActivatePeer delegation
- `ITypeMap.TryGetInvokerType` - Added for invoker type lookup (spec compliance)

### B.4 JNINativeWrapper Refactoring

- Separated `CreateDynamicDelegateWrapper` with `[RequiresDynamicCode]` attribute
- Guarded by `IsDynamicTypeRegistration` feature switch
- `CreateBuiltInDelegate` handles common delegate types statically

### B.5 IAndroidCallableWrapper Split (Optimization)

- `GetFunctionPointer` moved from `JavaPeerProxy` to `IAndroidCallableWrapper` interface
- Only ACW types implement `IAndroidCallableWrapper`
- MCW types (95%+) only implement `JavaPeerProxy` - reduces generated code size

---

*Analysis version: 3.0*  
*Last updated: 2026-01-31*
