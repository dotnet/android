# NativeAOT TypeMap Learnings

This document captures the key learnings from adapting Trimmable Type Map for NativeAOT in .NET for Android. It focuses on architectural concepts, design decisions, and challenges encountered.

## Table of Contents

1. [Overview](#overview)
2. [Runtime Differences](#runtime-differences)
3. [Trimmable Type Map Architecture](#typemap-v3-architecture)
4. [JNI Native Method Generation](#jni-native-method-generation)
5. [Trimming and Dead Code Elimination](#trimming-and-dead-code-elimination)
6. [Build Pipeline Integration](#build-pipeline-integration)
7. [Size Optimization Results](#size-optimization-results)
8. [Challenges and Solutions](#challenges-and-solutions)
9. [Future Considerations](#future-considerations)

---

## Overview

Trimmable Type Map is a new type mapping system for .NET for Android that uses compile-time code generation instead of runtime reflection. The goal is to make the type mapping system fully trimmable and compatible with NativeAOT.

**Key goals:**
- Eliminate runtime reflection for type lookups
- Support aggressive trimming (ILC removes unused types)
- Generate all JNI native methods at build time
- Produce minimal binary size

## Runtime Differences

### MonoVM vs CoreCLR vs NativeAOT

| Aspect | MonoVM | CoreCLR | NativeAOT |
|--------|--------|---------|-----------|
| JIT | Yes | Yes | No |
| Reflection | Full | Full | Limited |
| Dynamic code | Yes | Yes | No |
| Native callbacks | Runtime registration | Runtime registration | Compile-time generation |
| Trimming | ILLink | ILLink | ILC (more aggressive) |

### Key NativeAOT Constraints

1. **No JIT compilation** - All code must be AOT compiled
2. **No `Reflection.Emit`** - Cannot generate code at runtime
3. **No `Assembly.Load` for new assemblies** - All assemblies must be known at compile time
4. **Limited reflection** - Only types/members preserved by ILC are available
5. **Function pointers** - Must be obtained at compile time, not via dlsym at runtime

### Implications for TypeMap

- **Marshal methods must be pre-generated** - Cannot use JIT to generate JNI callbacks
- **Type activation must be explicit** - Cannot rely on `Activator.CreateInstance` with unknown types
- **Type mapping data must survive trimming** - Unused mappings will be removed by ILC

## Trimmable Type Map Architecture

### Core Concept: Proxy Types

Trimmable Type Map generates a "proxy type" for each Java peer type. The proxy contains:
- Activation constructor delegates
- Type registration with the type mapping system

```
Java Type: android/app/Activity
  ↓
.NET Type: Android.App.Activity
  ↓
Proxy Type: _Microsoft.Android.TypeMaps.Android_App_Activity_Proxy
```

### Type Resolution Flow

1. **Java → .NET lookup**: JNI returns a Java object reference
2. **Get Java class name**: `GetObjectClass()` + `GetClassName()`
3. **Find .NET type**: Query TypeMap with Java class name
4. **Activate instance**: Use proxy's activation delegate

### Attribute-Based Registration

Types are registered using attributes that ILC can analyze:
- `[JavaPeerRegistrar]` - Marks the TypeMaps assembly
- `[JavaPeerProxy]` - Marks each proxy type with Java class name

## JNI Native Method Generation

### The Problem

When Java calls a native method, the JVM looks for a C function with a specific name:
```
Java_package_ClassName_methodName
```

In MonoVM/CoreCLR, these are registered dynamically. In NativeAOT, they must exist as compiled native functions.

### The Solution: LLVM IR Generation

At build time, we generate LLVM IR files containing:
1. **Native callback stubs** - C functions with JNI names that call into .NET
2. **Activation callbacks** - For creating .NET instances from Java

These are compiled to `.o` files and linked into the final native library.

### Marshal Method Flow

```
Java code
  ↓ (JNI call)
Java_mono_android_runtime_InputStreamAdapter_n_read (generated native stub)
  ↓ (function pointer call)
.NET method: InputStreamAdapter.Read()
  ↓ (managed code)
Return value back through JNI
```

### Function Pointer Passing

NativeAOT cannot use `dlsym` to look up function pointers at runtime. Instead:
- Function pointers are obtained at compile time using `&Method` syntax
- Passed to native code during initialization via JNI
- Stored in native side for later callback

## Trimming and Dead Code Elimination

### ILC Trimming Behavior

ILC (IL Compiler) aggressively removes unused code:
- Types not referenced → removed
- Methods not called → removed
- Proxy types for unused Java peers → removed

This is beneficial for size but requires careful coordination.

### The Filtering Problem

We generate marshal methods for ALL potential Java peer types (~8000+), but after ILC trimming, only a small subset survives (~60 for a simple app).

Without filtering:
- 308 marshal method `.o` files (~793 KB)
- 758 exported JNI symbols
- All linked into final binary (unused)

With filtering:
- 9 `.o` files (~22 KB)
- 24 exported symbols
- 97% reduction in marshal method code

### DGML/Metadata-Based Filtering

ILC generates a `metadata.csv` file listing surviving types. We parse this to:
1. Identify which proxy types survived trimming
2. Filter `.o` files to only link survivors
3. Filter exports to only include defined symbols

## Build Pipeline Integration

### Build Order Dependencies

```
GenerateTypeMapAssembly (generates proxies, LLVM IR, Java files)
  ↓
IlcCompile (compiles .NET to native, trims unused code)
  ↓
FilterMarshalMethodsByIlcMetadata (filters based on ILC output)
  ↓
CompileNativeAssembly (compiles LLVM IR to .o files)
  ↓
LinkNative (links filtered .o files into final .so)
```

### Key MSBuild Targets

| Target | Purpose |
|--------|---------|
| `_GenerateTypeMapAssembly` | Generate TypeMaps assembly, LLVM IR, Java files |
| `IlcCompile` | NativeAOT compilation (produces metadata.csv) |
| `_CompileTypeMapMarshalMethodsForNativeAot` | Compile and link marshal methods |

### ILC Integration Points

- `IlcGenerateMetadataLog=true` - Required for filtering
- `AfterTargets="IlcCompile"` - Filter runs after ILC
- `BeforeTargets="LinkNative"` - Filter runs before linking

## Size Optimization Results

### Test Application Metrics

| Metric | Before Optimization | After Optimization |
|--------|--------------------|--------------------|
| Generated .o files | 308 | 308 (no change) |
| Linked .o files | 308 | 9 |
| Export symbols | 758 | 24 |
| Marshal method code | 793 KB | 22 KB |
| Native library size | 5.4 MB | 5.1 MB |

### Where Size Comes From

1. **ILC-compiled .NET code** - Bulk of the native library
2. **Marshal method stubs** - JNI callback implementations
3. **Runtime libraries** - NativeAOT runtime, GC, etc.
4. **Native dependencies** - Compression, crypto, etc.

## Challenges and Solutions

### Challenge 1: Mono.Android ACW Types

**Problem**: The generator was skipping ALL types from `Mono.Android` assembly, including ACW (Android Callable Wrapper) types that need JCW generation.

**Solution**: Changed the filter from assembly-based to behavior-based:
- MCW types (`DoNotGenerateAcw=true`) - Skip, they're bindings to existing Java
- ACW types (`DoNotGenerateAcw=false`) - Generate JCW and marshal methods

### Challenge 2: Inherited [Register] Attributes

**Problem**: ACW types like `OutputStreamAdapter` override methods from MCW base classes without explicit `[Register]` attributes. The generator wasn't finding these.

**Solution**: Scan base type hierarchy for ACW types to find `[Register]` on overridden methods.

### Challenge 3: Export/Object File Mismatch

**Problem**: JNI symbol names use Java package format, but our type identifiers use .NET namespace format. Nested types also have different representations.

**Solution**: Use `nm` tool to extract actual defined symbols from `.o` files, then filter exports based on what's actually defined.

### Challenge 4: Function Pointer Availability

**Problem**: `dlsym` cannot find managed function pointers in NativeAOT.

**Solution**: Pass function pointers directly from managed to native during initialization, similar to CoreCLR approach.

### Challenge 5: Build Order Dependencies

**Problem**: Need ILC metadata to filter, but ILC runs after initial code generation.

**Solution**: 
1. Generate ALL marshal methods initially
2. Compile to `.o` files
3. After ILC, filter which `.o` files to actually link

### Challenge 6: Abstract/Interface Type Activation

**Problem**: When `CreateInstance` is called for an abstract type or interface (e.g., `Android.Content.Context`), the generated proxy was trying to instantiate the abstract type directly, causing a SIGSEGV crash (null function pointer for abstract constructor).

**Root Cause**: The proxy's `CreateInstance` method was generated to create the target type (`Context`) even when called with an invoker type (`ContextInvoker`). Since invokers share JNI names with their parent, `GetProxyForManagedType(ContextInvoker)` returns `Context_Proxy`, which then tried to construct `Context` (abstract) instead of `ContextInvoker` (concrete).

**Solution**: Modified `GenerateTypeMapAssembly.GenerateProxyType` to detect abstract/interface types and generate code that creates the invoker type instead:
```csharp
// If this is an abstract/interface type with an invoker, CreateInstance should create the invoker
if ((peer.IsInterface || peer.IsAbstract) && !string.IsNullOrEmpty(peer.InvokerTypeName)) {
    activationTypeRef = AddExternalTypeReference(peer.InvokerAssemblyName!, peer.InvokerTypeName!);
}
```

**Key Insight**: Invoker types (e.g., `ContextInvoker`) have `DoNotGenerateAcw=true` because they share the same JNI name as their parent. The parent's proxy must handle creating the invoker when the parent is abstract.

### Challenge 7: Duplicate TypeMap Creation

**Problem**: `TypeMapAttributeTypeMap` was being created twice during startup - once in `JavaInteropRuntime.init()` and again in `JreRuntime.CreateJreVM()`, causing confusion and wasted resources.

**Solution**: Fixed the null check in `JreRuntime.cs` to use `&&` instead of `||`:
```csharp
// Before (wrong): if (options.TypeManager == null || options.ValueManager == null)
// After (correct): if (options.TypeManager == null && options.ValueManager == null)
```

### Challenge 8: Required Runtime JARs for NativeAOT

**Problem**: The `_CreateStrippedRuntimeJarForTypeMapV3` target was stripping `java-interop.jar` which removes essential classes like `ManagedPeer`, `JavaProxyObject`, and `JavaProxyThrowable` that are required for NativeAOT.

**Solution**: 
1. Skip JAR stripping entirely for NativeAOT (`_CreateStrippedRuntimeJarForTypeMapV3` condition)
2. Keep `java-interop.jar` in the APK for NativeAOT builds
3. Remove the `_RemoveLegacyJavaInteropJarsForNativeAot` target that was erroneously deleting needed JARs

### Challenge 9: Crypto Library Integration for HTTPS/SSL

**Problem**: HTTPS requests failed with "No implementation found for boolean net.dot.android.crypto.DotnetProxyTrustManager.verifyRemoteCertificate(long)". The Android crypto library (`libSystem.Security.Cryptography.Native.Android`) has Java components that call back to native code, but these symbols weren't available.

**Root Causes**:
1. **Crypto JAR missing**: `libSystem.Security.Cryptography.Native.Android.jar` wasn't included in the APK for NativeAOT
2. **ProGuard stripping Java classes**: R8/ProGuard removed `net.dot.android.crypto.*` classes
3. **gc-sections removing crypto code**: The `--gc-sections` linker flag eliminated "unreachable" crypto native code
4. **JNI init handler not registered**: `AndroidCryptoNative_InitLibraryOnLoad` wasn't called during JNI_OnLoad
5. **JNI callback not exported**: `Java_net_dot_android_crypto_DotnetProxyTrustManager_verifyRemoteCertificate` wasn't in the exports file

**Solution**:
1. **Add crypto JAR**: Create `_IncludeCryptoJarForNativeAot` target that copies the JAR to `$(IntermediateOutputPath)android-nativeaot/` for DEX compilation
2. **Keep Java classes**: Add ProGuard rules via `_AddCryptoProguardRulesForNativeAot` target: `-keep class net.dot.android.crypto.** { *; }`
3. **Bypass gc-sections**: Set `LinkerFlavor=android` to skip the gc-sections condition (add `-fuse-ld=lld` later to fix the linker selection)
4. **Force crypto symbols**: Use `--whole-archive` to include all symbols from the crypto `.a` file
5. **Register JNI init handler**: Add `<AndroidStaticJniInitFunction Include="AndroidCryptoNative_InitLibraryOnLoad" />` to static ItemGroup
6. **Export JNI callback**: Write crypto symbol to exports file and append via `AppendMarshalMethodExports`

**Key Insight**: NativeAOT's aggressive gc-sections optimization removes symbols that appear "unreachable" from a static analysis perspective, even if they're called dynamically via JNI. The workaround is to bypass gc-sections entirely for NativeAOT Android builds.

### Challenge 10: JCW Type Name Casing and Interface Prefixes

**Problem**: Generated JCW Java files had incorrect type names for constructor parameters and return types:
- Namespace casing: `java.Security.Cert.X509Certificate` instead of `java.security.cert.X509Certificate`
- Interface prefix: `javax.net.ssl.IX509TrustManager` instead of `javax.net.ssl.X509TrustManager`

**Root Cause**: The `TypeNameToJniObject` function in `GenerateTypeMapAssembly.cs` was computing JNI type names from managed type names using simple string replacement, but:
1. Managed namespaces use PascalCase (`Java.Security.Cert`) while Java uses lowercase (`java.security.cert`)
2. .NET interface naming convention prefixes with `I` (`IX509TrustManager`) but Java interfaces don't have this prefix

**Solution**: Multi-pronged fix:
1. **Cache [Register] attribute values**: When scanning types, populate `_managedToJniNameCache` with the JNI name from each type's `[Register]` attribute
2. **Use cache first**: In `TypeNameToJniObject`, check the cache before computing - this gives the authoritative JNI name
3. **Fix namespace casing**: Split type name into parts, lowercase all namespace parts, keep class name as-is
4. **Strip interface prefix**: For classes starting with `I` followed by uppercase, strip the `I` (e.g., `IX509TrustManager` → `X509TrustManager`)

**Key Insight**: The `[Register]` attribute on types like `IX509TrustManager` contains the correct JNI name (`javax/net/ssl/X509TrustManager`). By caching these during the scanning phase, we can use them for accurate constructor signature generation.

### Challenge 11: FakeSSLSession JCW Generation

**Problem**: `ServerCertificateCustomValidator.TrustManager.FakeSSLSession` (a nested class implementing `ISSLSession`) wasn't being generated as a JCW, causing `ClassNotFoundException` at runtime.

**Root Cause**: The class had `DoNotGenerateAcw=true` because it was historically provided by `mono.android.jar`. For NativeAOT, we don't use that JAR so the class needs to be generated.

**Solution**: Remove `DoNotGenerateAcw=true` from the `[Register]` attribute on `FakeSSLSession`.

### Challenge 12: IList<T> Generic Collection Marshalling with DerivedTypeFactory

**Problem**: `AndroidMessageHandler.CopyHeaders` crashed because generic `IList<T>` conversion used `MakeGenericType` which is incompatible with NativeAOT.

**Root Cause**: `JavaConvert.GetJniHandleConverter` threw `NotSupportedException` for `IList<T>` types because it tried to use `MakeGenericType(typeof(JavaList<>), elementType)` which requires runtime type construction.

**Initial Fix (Quick Hack)**: Pre-register converters for common types (`IList<string>`, `IList<int>`, etc.) in a static dictionary. This worked but was limited - any `IList<CustomType>` would fail.

**Proper Solution**: Use the `DerivedTypeFactory` pattern already designed for this purpose:

```csharp
static Func<IntPtr, JniHandleOwnership, object?>? TryCreateGenericListConverter (Type listType)
{
    var elementType = listType.GetGenericArguments()[0];
    
    // Primitives/string have dedicated converters
    if (elementType == typeof(string)) {
        return (h, t) => JavaList<string>.FromJniHandle(h, t);
    }
    // ... similar for int, long, bool, float, double, object
    
    // For Java peer types, use the TypeMap to get the proxy's factory
    var proxy = typeMap.GetProxyForManagedType(elementType);
    if (proxy == null) {
        return (h, t) => JavaList.FromJniHandle(h, t);  // fallback
    }
    
    var factory = proxy.GetDerivedTypeFactory();
    return (h, t) => factory.CreateListFromHandle(h, t);
}
```

**Key Insight**: The `DerivedTypeFactory` abstraction enables AOT-safe generic collection creation:
1. Each `JavaPeerProxy` has `GetDerivedTypeFactory()` returning a `DerivedTypeFactory<T>`
2. `DerivedTypeFactory<T>` has `CreateListFromHandle()` that returns `new JavaList<T>(handle, transfer)`
3. Since the factory is typed at compile time, no `MakeGenericType` is needed
4. For primitive/string types, we still need explicit converters since they don't have proxies

**JavaPeerProxy.TargetType Change**: Also required changing `TargetType` from `abstract Type { get; }` to `virtual Type { get; set; }` because the generated proxy IL calls `set_TargetType` in the constructor.

## Current Status

### Working NativeAOT App (NativeAotComplexApp)

The NativeAOT Trimmable Type Map implementation is now fully functional with:
- ✅ App launches successfully
- ✅ UI renders and is interactive
- ✅ Type mapping works (Java → .NET lookups)
- ✅ Invoker types created correctly (ICharSequenceInvoker, etc.)
- ✅ Abstract/interface peer creation works
- ✅ DNS resolution works
- ✅ TCP socket connections work
- ✅ HTTPS/SSL works with certificate validation
- ✅ **ServerCertificateCustomValidationCallback works!**
- ✅ Response headers correctly parsed (IList<string> marshalling)
- ✅ FakeSSLSession hostname verification works
- ✅ Nested types implementing interfaces (JCW generation)

### Build Artifacts

- **Marshal methods**: 307 LLVM IR files generated
- **Symbol reduction**: 96.5% (from 8,788 to ~300 after filtering)
- **APK structure**: Contains `NativeAotComplexApp.so`, `java-interop.jar`, `mono.android.jar`

---

## Future Considerations

### Conditional Crypto Library Inclusion

Currently, the crypto JAR/DEX and static library are always included. An optimization would be:
- Only include `libSystem.Security.Cryptography.Native.Android.jar` and `.a` if `System.Net.Security.SslStream` survives ILC trimming
- Check ILC `metadata.csv` for `SslStream` presence after `IlcCompile`
- This would reduce APK size for apps that don't use HTTPS/SSL

### ProGuard/R8 Integration

Currently, all Java classes are included. A similar filtering approach could be applied to:
- Only include Java classes for surviving types
- Generate targeted ProGuard keep rules

### Incremental Build Support

Currently, filtering happens on every build. Could potentially:
- Cache ILC metadata between builds
- Only recompile changed marshal methods

### Multi-ABI Support

Currently focused on arm64-v8a. Need to:
- Generate LLVM IR for each ABI
- Apply filtering per-ABI
- Handle ABI-specific optimizations

### Alternative to metadata.csv

`IlcGenerateMetadataLog` is a diagnostic feature. Could explore:
- Custom ILC extension to output surviving types
- Post-processing the native binary symbols
- Integration with ILC's dependency graph directly

---

## Appendices

### Appendix A: LLVM IR Marshal Method Structure

Each marshal method file contains:
1. External declaration of the managed function pointer
2. JNI native function definition with correct naming
3. Call from JNI to managed via function pointer
4. JNI type conversions as needed

Example structure:
```llvm
; External: managed method to call
@managed_InputStreamAdapter_read = external global ptr

; JNI native method implementation
define i32 @Java_mono_android_runtime_InputStreamAdapter_n_1read__(ptr %env, ptr %this) {
  %fn = load ptr, ptr @managed_InputStreamAdapter_read
  %result = call i32 %fn(ptr %env, ptr %this)
  ret i32 %result
}
```

### Appendix B: ILC Metadata.csv Format

The metadata.csv file format:
```
Handle, Kind, Name, Children
7401a8e9, TypeDefinition, "_Microsoft.Android.TypeMaps.Android_Util_Log_Proxy", "..."
```

We parse for `TypeDefinition` entries matching `_Microsoft.Android.TypeMaps.*_Proxy` pattern.

### Appendix C: JNI Symbol Naming Convention

JNI native method names follow a specific encoding:
- Package separators: `.` → `_`
- Nested class separator: `$` → `_1`
- Method name suffix: `_n_1{methodName}`
- Activation constructor: `_nc_1activate_1{index}`

Example:
```
Java class: mono.android.runtime.InputStreamAdapter
Method: read()
Symbol: Java_mono_android_runtime_InputStreamAdapter_n_1read__
```

### Appendix D: Key Files

| File | Purpose |
|------|---------|
| `GenerateTypeMapAssembly.cs` | Main code generation task |
| `FilterMarshalMethodsByIlcMetadata.cs` | Post-ILC filtering task |
| `Microsoft.Android.Sdk.NativeAOT.targets` | Build pipeline integration |
| `TypeMapAttributeTypeMap.cs` | Runtime type lookup implementation |
| `PeerCreationHelper.cs` | Shared peer activation logic |

### Appendix E: Debug Logging

Enable detailed logging with:
```xml
<PropertyGroup>
  <AndroidBuildVerbosity>Detailed</AndroidBuildVerbosity>
</PropertyGroup>
```

Key log prefixes:
- `[GTMA]` - GenerateTypeMapAssembly
- `[FMMIM]` - FilterMarshalMethodsByIlcMetadata
- `[TypeMap]` - General TypeMap build messages
