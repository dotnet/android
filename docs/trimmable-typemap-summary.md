# Trimmable Type Map for .NET Android

## Motivation

The legacy type mapping system in .NET Android relies on reflection (`Type.GetType()`, `Activator.CreateInstance()`) and runtime code generation. This is incompatible with:

- **NativeAOT**: Reflection-based type instantiation fails at runtime
- **Aggressive trimming**: Required types get removed because the trimmer cannot see dynamic usage

The Trimmable Type Map replaces the legacy system with a compile-time code generation approach that is **AOT-safe and trimming-safe by design**.

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

1. **Scan assemblies** for Java peer types (`[Register]` attributes, interface implementations)
2. **Generate `_Microsoft.Android.TypeMaps.dll`** containing:
   - `TypeMap<T>` attributes for each Java-to-.NET type mapping
   - `JavaPeerProxy` subclasses with `CreateInstance()` and `GetFunctionPointer()` methods
3. **Generate Java files** (JCWs) for user types and interface implementors
4. **Generate LLVM IR** for native-to-managed callbacks with per-type caching

### Runtime

1. Java calls a native method (e.g., `n_onCreate`)
2. LLVM-generated stub checks cached function pointer, calls `GetFunctionPointer` if needed
3. `TrimmableTypeMap` looks up type via `TypeMapping.Get<T>(jniName)`
4. Returns cached `JavaPeerProxy` which provides the function pointer or creates instances

## Expected Performance Characteristics

| Aspect | Expectation | Status |
|--------|-------------|--------|
| **Startup time** | Similar to legacy (type map loading is fast) | Needs measurement |
| **First method call** | Slightly slower (proxy lookup + cache) | ~1-2ms overhead |
| **Subsequent calls** | Native-level caching, same as legacy | ✓ Verified |
| **APK size** | Smaller with R8 trimming (~87% DEX reduction observed) | ✓ Verified |
| **Memory** | Slightly higher (proxy objects cached) | Needs measurement |

## Open Questions

1. **Startup performance**: What is the impact of loading ~8000 `TypeMap` attributes on low-end devices?
2. **Interface implementations**: The current JCW generator only emits overridden methods, not all interface methods - this needs fixing for full compatibility
3. **Generic types**: `MakeGenericType()` is not AOT-safe; generic invoker types must be pre-registered
4. **Array types**: Currently limited to rank 1-3; higher-rank arrays throw at runtime

## Current Status

- ✅ Core runtime implementation complete
- ✅ Build task generates type map assembly
- ✅ Sample app runs successfully on device
- ⚠️ JCW generation has bugs with interface implementations
- ⚠️ Not all Mono.Android-Tests pass yet
- ❌ No performance benchmarks yet

## Files

- `src/Mono.Android/Java.Interop/TrimmableTypeMap.cs` - Runtime type map implementation
- `src/Mono.Android/Java.Interop/ITypeMap.cs` - Abstraction interface
- `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` - Build-time generator
- `docs/trimmable-typemap-spec.md` - Full specification
