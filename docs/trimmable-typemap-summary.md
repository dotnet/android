# Trimmable Type Map for .NET Android

## Motivation

The legacy type mapping system in .NET Android relies on reflection (`Type.GetType()`, `Activator.CreateInstance()`) and runtime code generation. This is incompatible with:

- **NativeAOT**: Reflection-based type instantiation fails at runtime
- **Aggressive trimming**: Required types get removed because the trimmer cannot see dynamic usage

The Trimmable Type Map replaces the legacy system with a compile-time code generation approach that is **AOT-safe and trimming-safe by design**.

## Comparison: Legacy vs Trimmable Type Map

| Aspect | Legacy Type Map | Trimmable Type Map |
|--------|-----------------|-------------------|
| **Type lookup** | Native typemap tables + `Type.GetType()` | `TypeMapping.Get<T>()` intrinsic |
| **Instance creation** | `Activator.CreateInstance()` | Pre-generated `CreateInstance()` factory |
| **Method dispatch** | Reflection to find methods | Pre-generated `GetFunctionPointer()` |
| **Trimmer compatibility** | Requires custom ILLink steps | Works with standard trimmer |
| **NativeAOT compatibility** | ❌ Not supported | ✅ Fully supported |
| **Type preservation** | `MarkJavaObjects` ILLink step | `[TypeMap<T>]` attributes |
| **Build output** | Native `.o` files with typemap data | `_Microsoft.Android.TypeMaps.dll` + LLVM IR |
| **Runtime** | MonoVM only | CoreCLR + NativeAOT |

## Key Aspects

### Trimming-Safe by Design
- All type mappings are generated as `[TypeMap<T>]` attributes at build time
- The .NET trimmer's `TypeMapping` intrinsic preserves types that are registered
- No `Type.GetType()` string-based lookups at runtime

### AOT-Safe by Design
- Type instantiation uses pre-generated factory methods (`CreateInstance`)
- Method dispatch uses pre-generated function pointer accessors (`GetFunctionPointer`)
- No `Activator.CreateInstance()` or `MakeGenericType()` at runtime

## How It Works

### Build Time (GenerateTypeMapAssembly Task)

| Step | Legacy | Trimmable |
|------|--------|-----------|
| 1. Scan assemblies | `GenerateJavaStubs` task | `GenerateTypeMapAssembly` task |
| 2. Generate type maps | Native typemap tables (`.o` files) | `[TypeMap<T>]` attributes in IL |
| 3. Generate activation | Relies on `Activator.CreateInstance` | `JavaPeerProxy.CreateInstance()` methods |
| 4. Generate JCWs | `GenerateJavaStubs` task | Integrated in `GenerateTypeMapAssembly` |
| 5. Generate callbacks | Marshal methods in native code | LLVM IR with managed `GetFunctionPointer` |

### Runtime

| Step | Legacy | Trimmable |
|------|--------|-----------|
| Java calls native | Same | Same |
| Lookup type | Native typemap → `Type.GetType()` | `TypeMapping.Get<T>(jniName)` |
| Create instance | `Activator.CreateInstance()` | `JavaPeerProxy.CreateInstance()` |
| Get callback | Reflection-based lookup | `JavaPeerProxy.GetFunctionPointer()` |

## Performance Considerations

Performance characteristics have not been benchmarked yet. Key areas to measure:

- **Startup time**: The trimmable type map generates more managed code that needs JIT compilation. R2R (ReadyToRun) / crossgen2 precompilation may be required to avoid startup regression.
- **First method call**: Managed proxy lookup adds overhead compared to native typemap lookup.
- **Memory**: Proxy objects are cached in memory.
- **APK size**: R8 can trim unused JCW classes, potentially reducing DEX size.

## Open Questions

1. **Startup performance**: What is the impact of loading ~8000 `TypeMap` attributes? Is R2R precompilation sufficient?
2. **NativeAOT integration**: MSBuild targets need work to support NativeAOT builds (RID metadata, ILLink integration)
3. **Interface implementations**: The current JCW generator only emits overridden methods, not all interface methods - this needs fixing for full compatibility
4. **Generic types**: `MakeGenericType()` is not AOT-safe; generic invoker types must be pre-registered
5. **Array types**: Currently limited to rank 1-3; higher-rank arrays throw at runtime

## Current Status

- ✅ Core runtime implementation complete
- ✅ Build task generates type map assembly
- ✅ Sample app runs successfully on device (CoreCLR)
- ⚠️ NativeAOT builds not yet supported
- ⚠️ JCW generation has bugs with interface implementations
- ⚠️ Not all Mono.Android-Tests pass yet
- ❌ No performance benchmarks yet

## Files

- `src/Mono.Android/Java.Interop/TrimmableTypeMap.cs` - Runtime type map implementation
- `src/Mono.Android/Java.Interop/LlvmIrTypeMap.cs` - Legacy type map (for comparison)
- `src/Mono.Android/Java.Interop/ITypeMap.cs` - Abstraction interface (shared by both)
- `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` - Build-time generator
- `docs/trimmable-typemap-spec.md` - Full specification
