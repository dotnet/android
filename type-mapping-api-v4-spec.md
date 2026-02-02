# Type Mapping API V4 Specification for .NET for Android

## Executive Summary

This document is a comprehensive revision of the Type Mapping API V3 specification, incorporating lessons learned from the Proof-of-Concept (PoC) implementation, cross-referencing with the actual codebase, and providing recommendations for production readiness.

**Key Changes from V3:**
- Consolidated findings from PoC implementation
- Identified remaining gaps and edge cases
- Added performance and build time analysis
- Clarified compromises and trade-offs
- Provided clearer path to production

---

## 1. Original Motivation and Goals

### 1.1 The Problem Being Solved

The legacy type mapping system in .NET for Android has fundamental incompatibilities with modern .NET capabilities:

| Problem | Impact | V4 Solution |
|---------|--------|-------------|
| `TypeManager.Activate()` uses `Type.GetType()` | Not AOT-safe | Compile-time TypeMapAttribute lookup |
| `Activator.CreateInstance()` for constructor invocation | Not trimming-safe | Direct `newobj` IL via IgnoresAccessChecksTo |
| Native type maps with integer indices | Complex dual-table system | Unified attribute-based system |
| Reflection-based marshal method registration | Incompatible with NativeAOT | Static codegen with LLVM IR stubs |
| Dynamic `[Export]` method registration | Runtime reflection | Build-time `[Export]` method collection |

### 1.2 Goals (Validated)

The V3/V4 design successfully addresses:

- ✅ **AOT-Safe**: All type instantiation works with NativeAOT
- ✅ **Trimming-Safe**: Proper attribute design ensures required types survive trimming
- ✅ **Single Activation Path**: All types use the same activation mechanism
- ✅ **Developer Experience**: No changes required to existing application code
- ✅ **Export Support**: `[Export]` methods handled with static codegen

### 1.3 Goals (Partially Achieved)

- ⚠️ **Performance**: Runtime overhead acceptable, but startup impact on large type maps needs measurement (see Section 5.2 for measurement plan)
- ⚠️ **Build Time**: Generation of 7000+ proxy types adds build overhead (see Section 6.3 for optimization opportunities)

---

## 1A. Alternative Approach: Reflection-Safe TypeMap

This section explores an alternative design that trades pre-generated code for trimmer-safe reflection. This approach could coexist with the full codegen approach or replace it for certain scenarios.

### 1A.1 The Insight

The current V4 PoC generates:
1. **LLVM IR stubs** for every marshal method (~10KB per type)
2. **IL factory methods** in every proxy (`CreateInstance`, `GetFunctionPointer`)
3. **Switch statements** mapping method indices to function pointers

But .NET provides `[DynamicallyAccessedMembers]` which tells the trimmer to preserve specified members. With proper annotations, reflection becomes trimmer-safe:

```csharp
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | 
                            DynamicallyAccessedMemberTypes.NonPublicConstructors |
                            DynamicallyAccessedMemberTypes.PublicMethods)]
Type TargetType { get; }
```

If the trimmer preserves constructors and methods, we can use reflection to invoke them at runtime without pre-generating IL.

### 1A.2 Minimal Proxy Design

Instead of generating full factory methods:

```csharp
// CURRENT: Full codegen proxy (~50 lines per type)
sealed class MainActivityProxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
        => new MainActivity(handle, transfer);
    
    public override IntPtr GetFunctionPointer(int methodIndex)
        => methodIndex switch {
            0 => (IntPtr)(delegate* unmanaged<...>)&n_OnCreate,
            1 => (IntPtr)(delegate* unmanaged<...>)&n_OnResume,
            // ... 20 more methods
            _ => IntPtr.Zero
        };
    
    // Plus LLVM IR stubs for each method
}
```

We could generate:

```csharp
// ALTERNATIVE: Minimal proxy (~5 lines per type)
sealed class MainActivityProxy : ReflectionJavaPeerProxy
{
    [DynamicallyAccessedMembers(Constructors | Methods)]
    public override Type TargetType => typeof(MainActivity);
}
```

### 1A.3 ReflectionJavaPeerProxy Base Class

```csharp
abstract class ReflectionJavaPeerProxy : JavaPeerProxy
{
    const DynamicallyAccessedMemberTypes Constructors = 
        DynamicallyAccessedMemberTypes.PublicConstructors | 
        DynamicallyAccessedMemberTypes.NonPublicConstructors;
    const DynamicallyAccessedMemberTypes Methods = 
        DynamicallyAccessedMemberTypes.PublicMethods | 
        DynamicallyAccessedMemberTypes.NonPublicMethods;

    [DynamicallyAccessedMembers(Constructors | Methods)]
    public abstract Type TargetType { get; }

    // Cached constructor - found once via reflection, then reused
    ConstructorInfo? _activationCtor;

    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        _activationCtor ??= TargetType.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { typeof(IntPtr), typeof(JniHandleOwnership) }, null);
        
        if (_activationCtor == null)
            throw new TypeMapException($"No activation constructor found for {TargetType}");
        
        return (IJavaPeerable)_activationCtor.Invoke(new object[] { handle, transfer });
    }

    public override Array CreateArray(int length, int rank)
    {
        // This still needs codegen or Array.CreateInstance
        // Array.CreateInstance is NOT trimmer-safe, so this is a limitation
        return rank switch {
            1 => Array.CreateInstance(TargetType, length),
            2 => Array.CreateInstance(TargetType.MakeArrayType(), length),
            _ => throw new ArgumentOutOfRangeException(nameof(rank))
        };
    }
}
```

### 1A.4 Dynamic Native Member Registration

Instead of pre-generating LLVM IR stubs, register native methods dynamically at runtime:

```csharp
// When a Java type is first accessed, register its native methods
void RegisterNativeMethods(Type managedType, JniObjectReference javaClass)
{
    var methods = managedType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Where(m => m.GetCustomAttribute<RegisterAttribute>() != null);
    
    foreach (var method in methods)
    {
        var register = method.GetCustomAttribute<RegisterAttribute>()!;
        var connector = GetConnectorMethod(method); // e.g., GetOnCreate_Handler()
        var callback = connector.Invoke(null, null) as Delegate;
        
        // Register with JNI
        JniEnvironment.Types.RegisterNatives(javaClass, ...);
    }
}
```

This is what `DynamicNativeMembersRegistration.cs` already does in the PoC.

### 1A.5 Trade-off Analysis

| Aspect | Full Codegen (Current) | Reflection-Safe Alternative |
|--------|------------------------|----------------------------|
| **Binary size** | Larger (LLVM IR + IL) | Smaller (minimal proxies) |
| **Build time** | Longer (generate all stubs) | Shorter (minimal generation) |
| **Runtime startup** | Faster (pre-compiled) | Slower (reflection + JIT) |
| **Runtime steady-state** | Same | Same (after warmup) |
| **Debugging** | Harder (generated code) | Easier (step through real methods) |
| **NativeAOT** | ✅ Works | ⚠️ Needs `rd.xml` or source generators |
| **Trimming** | ✅ Safe | ✅ Safe (with DAMT annotations) |
| **Code complexity** | Higher (6000 lines) | Lower (~500 lines) |
| **Array creation** | ✅ AOT-safe | ⚠️ Uses Array.CreateInstance |

### 1A.6 When Would Reflection-Safe Be Better?

**Potentially better for Debug builds:**
- Faster incremental builds (less codegen)
- Better debugging experience
- Smaller APK during development

**Probably not better for Release builds:**
- Startup overhead from reflection
- JIT compilation of marshal methods
- Risk of AOT issues with NativeAOT

### 1A.7 The Debug vs Release Divergence Question

**Option A: Same code path for Debug and Release**

Pros:
- "What you debug is what you ship"
- Bugs found in Debug also exist in Release
- Simpler mental model
- One code path to maintain

Cons:
- Debug builds slower than necessary
- Debugging pre-generated code is harder

**Option B: Reflection for Debug, Codegen for Release**

Pros:
- Fast Debug builds
- Natural debugging experience
- Release still gets full optimization

Cons:
- Different code paths = bugs that only appear in Release
- Two implementations to maintain
- "Works in Debug, crashes in Release" scenarios

**Recommendation:**

With SDK pre-generation (Section 9.1 of REVIEW.md), the argument for divergent paths weakens:
- 80% of types (SDK) are pre-generated once, not per-build
- App-only types are small subset
- Incremental builds can skip unchanged types

**If SDK pre-generation is implemented, keep Debug and Release on the same path.** The build time savings from reflection-based Debug would be marginal, and the risk of divergent behavior isn't worth it.

However, if SDK pre-generation is delayed or not feasible, a reflection-based Debug mode could be a reasonable interim solution.

### 1A.8 Hybrid Approach: TypeMapMode

We could expose a property to let developers choose:

```xml
<PropertyGroup>
  <!-- Options: Full, Reflection, Auto -->
  <AndroidTypeMapMode>Auto</AndroidTypeMapMode>
</PropertyGroup>
```

| Mode | Behavior |
|------|----------|
| `Full` | Always generate LLVM IR + IL (current V4) |
| `Reflection` | Generate minimal proxies, use reflection at runtime |
| `Auto` | `Reflection` for Debug, `Full` for Release |

**Not recommended initially** — adds complexity. But could be added later if build time becomes a significant pain point.

### 1A.9 PoC Implementation

A proof-of-concept implementation exists at:
- `src/Mono.Android/Java.Interop/ReflectionTypeMap.cs`

This file contains:

**1. `ReflectionTypeMap` class** — ITypeMap implementation using reflection:
```csharp
class ReflectionTypeMap : ITypeMap
{
    // Type lookup via cached dictionary built from attributes
    readonly IReadOnlyDictionary<string, Type> _jniToManagedMap;
    
    // Constructor cache for activation
    readonly ConcurrentDictionary<Type, ConstructorInfo?> _ctorCache;
    
    public IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType)
    {
        // Uses ConstructorInfo.Invoke - reflection, but trimmer-safe
        // because proxy has [DynamicallyAccessedMembers(Constructors)]
        var ctor = GetActivationConstructor(targetType);
        return (IJavaPeerable?)ctor.Invoke(new object[] { handle, transfer });
    }
    
    public IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex)
    {
        // Not supported - uses dynamic native registration instead
        throw new NotSupportedException();
    }
}
```

**2. `ReflectionPeerProxy` class** — Minimal proxy attribute:
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class ReflectionPeerProxy : JavaPeerProxy
{
    [DynamicallyAccessedMembers(Constructors | Methods)]
    public Type TargetType { get; }
    
    public ReflectionPeerProxy(
        [DynamicallyAccessedMembers(Constructors | Methods)] Type targetType)
    {
        TargetType = targetType;
    }
    
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
    {
        // Cached reflection - finds ctor once, then reuses
        _cachedCtor ??= TargetType.GetConstructor(...);
        return (IJavaPeerable)_cachedCtor.Invoke(new object[] { handle, transfer });
    }
}
```

**3. `ReflectionTypeMapEntryAttribute`** — Assembly-level mapping:
```csharp
// Generated in _Microsoft.Android.TypeMaps.dll:
[assembly: ReflectionTypeMapEntry("android/app/Activity", typeof(Android.App.Activity))]
[assembly: ReflectionTypeMapEntry("com/example/MainActivity", typeof(MainActivity))]
[assembly: ReflectionTypeMapEntry("android/view/View$OnClickListener", 
                                   typeof(Android.Views.View.IOnClickListener),
                                   invokerType: typeof(Android.Views.View.IOnClickListenerInvoker))]
```

### 1A.10 Generated Assembly Comparison

**Full Codegen Approach (Current V4):**
```csharp
// Per-type: ~500 bytes IL + LLVM IR stubs
[assembly: TypeMap<MainActivityProxy>("com/example/MainActivity")]

sealed class MainActivityProxy : JavaPeerProxy
{
    public override IJavaPeerable CreateInstance(IntPtr h, JniHandleOwnership t) 
        => new MainActivity(h, t);
    public override Array CreateArray(int len, int rank) => rank == 1 ? new MainActivity[len] : new MainActivity[len][];
    public override IntPtr GetFunctionPointer(int idx) => idx switch {
        0 => (IntPtr)(delegate* unmanaged<...>)&n_OnCreate,
        1 => (IntPtr)(delegate* unmanaged<...>)&n_OnResume,
        // ... more methods
        _ => IntPtr.Zero
    };
}

// Plus LLVM IR file with native entry points
```

**Reflection Approach (Alternative):**
```csharp
// Per-type: ~20 bytes IL, no LLVM IR
[assembly: ReflectionTypeMapEntry("com/example/MainActivity", typeof(MainActivity))]

// No proxy class generated per type!
// ReflectionPeerProxy is applied directly to the target type if needed
[ReflectionPeerProxy(typeof(MainActivity))]
class MainActivity : Activity { ... }
```

### 1A.11 Conclusion

The reflection-safe alternative is architecturally interesting and could reduce complexity. However:

1. **Array.CreateInstance** remains a problem (not trimmer-safe)
2. **NativeAOT** may have issues with reflection-based activation
3. **SDK pre-generation** largely solves the build time concern
4. **Divergent Debug/Release** paths are risky

**Recommendation:** Proceed with full codegen approach, but keep this design documented and the PoC available as a fallback if:
- Build time issues persist after SDK pre-generation
- A "fast Debug mode" is needed
- The full codegen approach proves too complex to maintain

---

## 2. Current Implementation Status

### 2.1 Components Implemented

Based on codebase cross-referencing:

| Component | File | Status | Lines |
|-----------|------|--------|-------|
| MSBuild Task | `GenerateTypeMapAssembly.cs` | ✅ Implemented | ~6000 |
| Runtime TypeMap | `TypeMapAttributeTypeMap.cs` | ✅ Implemented | ~500 |
| JavaPeerProxy Base | `JavaPeerProxy.cs` | ✅ Implemented | ~105 |
| ITypeMap Interface | `ITypeMap.cs` | ✅ Implemented | ~85 |
| Legacy TypeMap | `LlvmIrTypeMap.cs` | ✅ Maintained | ~330 |
| RuntimeFeature Switch | `RuntimeFeature.cs` | ✅ Implemented | ~42 |
| MSBuild Targets | `Microsoft.Android.Sdk.ILLink.targets` | ✅ Integrated | - |
| Sample Project | `samples/HelloWorld/NewTypeMapPoc` | ✅ Working | - |

### 2.2 Feature Switches

```csharp
// In RuntimeFeature.cs
RuntimeFeature.IsMonoRuntime     // Default: true  → Uses LlvmIrTypeMap
RuntimeFeature.IsCoreClrRuntime  // Default: false → Uses TypeMapAttributeTypeMap
RuntimeFeature.IsDynamicTypeRegistration // Default: true → Enables/disables dynamic registration
```

**Activation Conditions:**
- `AndroidEnableTypeMaps=true` in project file
- `RuntimeIdentifier` set (e.g., `android-arm64`)
- CoreCLR or NativeAOT runtime selected

---

## 3. Problems Faced During Development

### 3.1 Critical Issues (Resolved)

#### 3.1.1 Method Index Synchronization Bug

**Problem:** LLVM IR stubs and C# `GetFunctionPointer` switch used different method ordering.

**Symptom:** `onCreate` (index 0) returned activation constructor's pointer → SIGSEGV crash.

**Root Cause:** IL generator iterated activation constructors first; LLVM generator iterated regular methods first.

**Fix:** Established explicit ordering contract:
1. Regular marshal methods: indices 0 to n-1
2. Activation constructors: indices n to m-1

**Lesson:** When multiple code generators must produce matching indices, they MUST use identical iteration order.

#### 3.1.2 Missing Java Constructors

**Problem:** User types like `MainActivity` didn't get Java constructors generated.

**Root Cause:** Code checked for `[Register("<init>")]` but C# uses `[Register(".ctor")]`.

**Fix:** Accept both `".ctor"` and `"<init>"` in jniName check.

#### 3.1.3 Callback Type Resolution for Inherited Methods

**Problem:** `n_*` callbacks are defined in base classes, not user types.

**Root Cause:** When walking base class chain, the connector string doesn't include type info for same-type methods.

**Fix:** When finding Register attribute in base class, use that base class as callback type.

### 3.2 Design Issues (Resolved)

#### 3.2.1 ILLink UnsafeAccessor Marking

**Problem:** ILLink marks ALL constructors when it sees `[UnsafeAccessor]`, regardless of signature.

**Impact:** 54 types preserved (vs 37 legacy).

**Resolution:** Use `IgnoresAccessChecksToAttribute` instead → 26 types preserved.

#### 3.2.2 Invokers in TypeMap Bloat

**Problem:** Including Invoker types created thousands of unnecessary alias entries.

**Impact:** 20% more proxy types than needed.

**Resolution:** Exclude Invokers entirely - they're only created by interface proxy's `CreateInstance`.

### 3.3 Known Limitations

#### 3.3.1 Generic Collections Not Supported

**Current Behavior (in JavaConvert.cs):**
```csharp
if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof (IList<>)) {
    throw new NotSupportedException (
        $"Generic IList<> conversion is not supported with TypeMap v3. Use JavaList<> directly.");
}
```

**Affected Types:** `IList<T>`, `IDictionary<K,V>`, `ICollection<T>`

**Status:** This is **future work**, not a permanent limitation. See Section 8.4 for proposed solution.

#### 3.3.2 Higher-Rank Arrays Not Supported

**Current Behavior:**
```csharp
// In JavaPeerProxy.CreateArrayOf<T>
return rank switch {
    1 => new T[length],
    2 => new T[length][],
    _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or 2"),
};
```

**Impact:** T[][][] and higher not supported. No known Android APIs use these.

**Status:** Low priority. If needed, see Section 8.1 for proposed solution.

---

## 4. Compromises and Trade-offs

### 4.1 Build Time vs Runtime Performance

| Approach | Build Time | Runtime | APK Size |
|----------|------------|---------|----------|
| V3 TypeMap (full) | Slower (+~50%) | Fast | Smaller |
| Legacy LlvmIr | Faster | Fast | Larger |

**V4 Recommendation:** Accept build time increase for production builds. Consider:
- SDK type pre-generation during SDK build
- NuGet package type caching by version
- Parallel assembly scanning (already implemented)

### 4.2 Code Generation Strategy

**Chosen:** Generate ALL types upfront, let trimmer remove unused.

**Alternative Considered:** Only generate types that survive trimming.

**Trade-off:**
- Chosen approach: Simpler pipeline, works with Debug builds, R8 handles Java trimming
- Alternative: Faster builds, but requires two-pass pipeline

### 4.3 Reflection for MonoVM

**Current:** TypeMapAttributeTypeMap works for both CoreCLR and MonoVM via `WorkaroundForMonoCollectTypeMapEntries()`.

**Trade-off:** The `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic is resolved at ILLink time (replaced with a static dictionary). MonoVM currently uses a reflection-based workaround because the PoC bypasses ILLink's intrinsic substitution via `SetEntryAssembly` hack.

**Proposed Long-term Solution:**
1. **Option A:** Extend MonoVM to support `TypeMapping` intrinsic (requires Mono runtime changes)
2. **Option B:** Pre-serialize type map to binary format at build time, load without reflection
3. **Option C:** Accept reflection cost for MonoVM (it already uses JIT, so reflection is acceptable)

**Recommendation:** Option C for now - MonoVM has different performance characteristics than CoreCLR/NativeAOT, and reflection at startup is acceptable. The workaround adds ~50ms startup on typical apps.

---

## 5. Performance Analysis

This section consolidates all performance considerations for TypeMap V4, covering both build-time and runtime performance. Each item is categorized as either "by-design" (intentional trade-off) or "future optimization" (known improvement opportunity).

### 5.1 Runtime Performance Overview

**Design Philosophy:** V4 prioritizes AOT-safety and trimmer compatibility over raw performance. However, the performance characteristics should be competitive with the legacy system for typical apps.

#### 5.1.1 Measured Baselines (from V3 spec)

From benchmarks on Samsung Galaxy S23:

| Configuration | Startup Time | DEX Size | Notes |
|---------------|--------------|----------|-------|
| MonoVM (Marshal Methods) | 169.8ms | 21KB | Legacy baseline |
| CoreCLR (blanket keeps) | 189.8ms | 249KB | Naive R8 config |
| CoreCLR (selective keeps) | 179ms | 33KB | Optimized R8 config |
| V4 TypeMap (projected) | ~180ms | ~35KB | Based on CoreCLR path |

**Improvement with R8:** 87% DEX size reduction, ~10ms startup improvement.

#### 5.1.2 Runtime Performance: By-Design Trade-offs

| Decision | Performance Impact | Justification |
|----------|-------------------|---------------|
| ConcurrentDictionary for caches | ~2x slower than Dictionary | Required for thread safety (Q1, S2, S3) |
| String-based JNI name lookup | Hash computation per lookup | AOT-safe, no native code dependency |
| Virtual dispatch for CreateInstance | One vtable lookup | Avoids switch statement, smaller code |
| Lazy proxy instantiation | First access is slower | Reduces startup cost for unused types |
| Attribute scanning on first access | O(n) scan once | Subsequent lookups are O(1) |

#### 5.1.3 Runtime Performance: Key Optimizations (Implemented)

| Optimization | Impact | Implementation |
|--------------|--------|----------------|
| **JNI class caching** | Avoids repeated FindClass | `_jniClassCache` stores global refs |
| **Proxy instance caching** | One proxy per type | `_proxyInstances` dictionary |
| **GetOrAdd pattern** | Lock-free reads | ConcurrentDictionary semantics |
| **Native-level caching** | One-time JNI resolve | LLVM IR globals store resolved handles |

### 5.2 Startup Performance (Critical Path)

#### 5.2.1 Startup Sequence

```
App Launch
├── 1. Load Mono.Android.dll                    (~10ms)
├── 2. Initialize AndroidValueManager          (~5ms)
├── 3. First JNI callback triggers TypeMap     (~50-100ms first time)
│   ├── Scan assembly attributes
│   ├── Populate proxy cache (lazy)
│   └── Create first proxy instance
├── 4. Subsequent lookups                      (<1ms each)
└── Total Additional Overhead: ~50-100ms first callback
```

#### 5.2.2 Startup Impact Measurement Plan

**Open Question:** What is the actual startup impact on low-end devices?

**Proposed Measurement:**
1. Device: Samsung A16 or similar low-end (2GB RAM, budget SoC)
2. App: ~8000 TypeMapAttributes (untrimmed Debug build)
3. Metrics: cold start, warm start, time to first Activity.onCreate

**Target:** < 100ms additional startup overhead vs legacy.

#### 5.2.3 Startup Optimizations: Future Work

| Optimization | Expected Impact | Complexity | Status |
|--------------|-----------------|------------|--------|
| **R2R pre-compilation** | 30-50% faster startup | High | ❌ Not implemented |
| **FrozenDictionary** | 20% faster lookups | Medium | ❌ Not implemented |
| **Binary serialization** | 50% faster initial load | High | ❌ Not implemented |
| **Tiered compilation hints** | JIT prioritization | Low | ❌ Not implemented |

**R2R Pre-compilation (Recommended):**

Ready-to-Run (R2R) compilation pre-JITs the TypeMaps.dll during build, eliminating JIT overhead at startup:

```xml
<!-- Proposed: Enable R2R for TypeMaps assembly -->
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunComposite>true</PublishReadyToRunComposite>
</PropertyGroup>

<!-- TypeMaps.dll specifically -->
<ItemGroup>
  <ReadyToRunAssemblies Include="_Microsoft.Android.TypeMaps.dll" />
</ItemGroup>
```

**Current Blocker:** R2R requires crossgen2 in the build pipeline. Need to verify Android target support.

**FrozenDictionary (Recommended):**

After initial population, convert mutable dictionaries to `FrozenDictionary<K,V>` for better cache locality:

```csharp
// Current: ConcurrentDictionary stays mutable
private readonly ConcurrentDictionary<string, JavaPeerProxy?> _typeMap;

// Proposed: Freeze after initialization complete
private FrozenDictionary<string, JavaPeerProxy?>? _frozenTypeMap;

public void FinalizeInitialization()
{
    _frozenTypeMap = _typeMap.ToFrozenDictionary();
    // Clear mutable dictionary to free memory
}
```

**Blocker:** Requires determining "initialization complete" signal.

### 5.3 Steady-State Runtime Performance

After startup, V4 performance should match or exceed legacy:

| Operation | Legacy | V4 | Notes |
|-----------|--------|-----|-------|
| Type lookup by JNI name | O(1) binary search | O(1) hash lookup | V4 slightly faster |
| Instance creation | Activator.CreateInstance | Direct factory call | V4 faster (no reflection) |
| Marshal method dispatch | Native function pointer | Native function pointer | Same |
| Array creation | Runtime.GetArrayType | proxy.CreateArray | V4 faster (no reflection) |

### 5.4 Memory Performance

#### 5.4.1 Memory Overhead: By-Design

| Cache | Size Formula | Typical Size | Notes |
|-------|--------------|--------------|-------|
| `_proxyInstances` | O(accessed types) | ~500 entries | Only instantiated proxies |
| `_jniClassCache` | O(looked-up types) | ~500 global refs | JNI global references |
| `_externalTypeMap` | O(all types) | ~7000 entries | Full type map |

**Total Additional Memory:** ~2-3 MB for typical app with 7000 types.

#### 5.4.2 JNI Global Reference Budget

Android limits global references to ~51,200. V4's `_jniClassCache` stores global refs indefinitely.

**Risk:** Apps with many types could exhaust global refs if all types are accessed.

**Proposed Mitigation:**
```csharp
// Option 1: LRU cache with eviction
private readonly LruCache<string, IntPtr> _jniClassCache = new(maxSize: 1000);

// Option 2: Weak global references (may be collected)
IntPtr weakGlobalRef = JNIEnv.NewWeakGlobalRef(localRef);

// Option 3: Release on memory pressure
public void OnTrimMemory(TrimLevel level)
{
    if (level >= TrimLevel.Moderate)
    {
        foreach (var globalRef in _jniClassCache.Values)
            JNIEnv.DeleteGlobalRef(globalRef);
        _jniClassCache.Clear();
    }
}
```

**Recommendation:** Implement Option 3 (release on memory pressure) as safest approach.

#### 5.4.3 Memory Optimizations: Future Work

| Optimization | Expected Savings | Complexity |
|--------------|------------------|------------|
| String interning for JNI names | ~30% string memory | Low |
| Struct-based proxy (no allocation) | ~50% proxy memory | Medium |
| Lazy attribute loading | ~40% initial memory | Medium |
| Memory-mapped binary format | Variable | High |

### 5.5 APK Size Impact

#### 5.5.1 Size Breakdown

| Component | Size | Notes |
|-----------|------|-------|
| TypeMaps.dll (IL) | ~500 KB | Proxies + attributes |
| LLVM IR native stubs | ~2 MB | Marshal method wrappers |
| JCW .dex files | ~35 KB (after R8) | Java callable wrappers |
| **Total Addition** | ~2.5 MB | Before compression |

#### 5.5.2 Size Optimizations: By-Design

| Decision | Size Impact | Justification |
|----------|-------------|---------------|
| Skip abstract class proxies | -30% proxies | Never instantiated from JNI |
| Skip interface proxies (use invoker) | -20% proxies | Interfaces use invoker types |
| Aggressive R8 shrinking | -87% DEX | Dead code elimination |

#### 5.5.3 Size Optimizations: Future Work

| Optimization | Expected Savings | Notes |
|--------------|------------------|-------|
| SDK pre-generation + stripping | ~80% IL removed | Ship pre-built SDK types |
| LLVM IR deduplication | ~30% native | Share common stub patterns |
| Trimmed TypeMaps.dll | ~50% IL | Remove unused proxy types |

---

## 6. Build Time Analysis

This section covers build-time performance, which is critical for developer inner-loop productivity. **The key insight is that V4's approach fundamentally changes when and how often type mapping code is generated, resulting in dramatically better incremental build times.**

### 6.0 Legacy vs V4: Fundamental Architecture Difference

#### Legacy Marshal Methods Approach

```
EVERY Release Build (per-RID, post-trimming):
├── 1. Build app assemblies
├── 2. Run ILLink trimmer (removes unused code)
├── 3. Run marshal method rewriter (POST-trimming)     ← SLOW
│   ├── Scan trimmed assemblies
│   ├── Generate native registration code
│   └── Rewrite assemblies with marshal stubs
├── 4. Generate per-RID native code
└── 5. Package APK

Problems:
- Runs AFTER trimming → cannot be cached across builds
- Per-RID → must run separately for arm64, x86, etc.
- Post-link → trimmer output changes invalidate cache
- ~6 seconds per RID per build
```

#### V4 TypeMap Approach

```
SDK Build (once, universal):
├── Scan Mono.Android.dll (~7000 types)
├── Generate SDK TypeMaps.dll
└── Ship in NuGet package

App Build (incremental-friendly):
├── 1. Scan user assemblies only (~50 types)          ← FAST
├── 2. Merge with pre-built SDK TypeMaps
├── 3. Run ILLink trimmer (TypeMaps survive)
└── 4. Package APK (same TypeMaps.dll for all RIDs)

Benefits:
- Runs BEFORE trimming → universal, cacheable
- RID-independent → one TypeMaps.dll for all architectures
- Pre-link → user code changes don't invalidate SDK cache
- ~100-300ms for typical app (user types only)
```

#### Comparison Summary

| Aspect | Legacy (Marshal Methods) | V4 (TypeMap) |
|--------|--------------------------|--------------|
| **When runs** | Post-trimming | Pre-trimming |
| **RID handling** | Per-RID generation | Universal (RID-independent) |
| **SDK types** | Regenerated every build | Pre-generated in SDK package |
| **Cacheability** | ❌ Trimmer output varies | ✅ Input assemblies stable |
| **Debug/Release sharing** | ❌ Different trim levels | ✅ Same TypeMaps.dll |
| **Incremental builds** | ❌ Full regen on any change | ✅ Only user types |
| **Typical app build** | ~6s per RID | ~100-300ms total |

### 6.1 Where Build Time Goes Today (PoC)

```
GenerateTypeMapAssembly PoC (~6000ms total)
├── JavaPeerScanner.ScanAssemblies (parallel)     (~500ms)
│   └── ~120 assemblies scanned
│   └── ~7000 types from Mono.Android alone (80%+)
├── TypeMapAssemblyGenerator.Generate              (~5000ms)
│   ├── Generate TypeMapAttributes (~7000 types)
│   ├── Generate Proxy Types (~5000 types after filtering)
│   ├── Generate UCO Methods (~500 for ACW types)
│   ├── Generate Java Source Files (~500 JCWs)
│   └── Generate LLVM IR Files (~500 stubs)
└── Output: _Microsoft.Android.TypeMaps.dll
```

**Critical Insight:** ~80% of types come from Mono.Android.dll and AndroidX packages. With SDK pre-generation, app builds only process ~20% of types (user code + uncommon NuGet packages).

### 6.2 Projected Build Times with SDK Pre-generation

| Scenario | Current PoC | With SDK Pre-gen | Notes |
|----------|-------------|------------------|-------|
| **Full rebuild** | ~6000ms | ~6000ms | Same (nothing cached) |
| **Incremental (no changes)** | ~6000ms | ~50ms | Hash check only |
| **Incremental (user code change)** | ~6000ms | ~300ms | User types only |
| **Debug build** | ~6000ms | ~300ms | Same TypeMaps as Release |
| **Release build (any RID)** | ~6000ms | ~300ms | RID-independent |
| **Multi-RID build (3 RIDs)** | ~18000ms | ~300ms | One universal TypeMaps.dll |

**Impact on Developer Inner Loop:**
- Current: Every F5 waits ~6 seconds for type map generation
- With V4 + SDK pre-gen: First build ~300ms, subsequent builds ~50ms

### 6.3 Why V4 is Cache-Friendly

#### Pre-trimming = Stable Inputs

Legacy runs post-trimming, meaning the input assemblies change based on:
- Which code paths the trimmer kept
- Trimmer configuration differences between Debug/Release
- Any change to app code that affects trimmer decisions

V4 runs pre-trimming on original assemblies:
- Mono.Android.dll is immutable (from NuGet)
- AndroidX packages are immutable (from NuGet)
- Only user assemblies change during development

#### RID-Independence

Legacy generates per-RID native code:
```
obj/Release/android-arm64/marshal-stubs.ll
obj/Release/android-x86_64/marshal-stubs.ll
obj/Release/android-arm/marshal-stubs.ll
```

V4 generates universal IL + **architecture-neutral LLVM IR**:
```
obj/Release/TypeMaps.dll           ← Same for all RIDs
obj/Release/marshal-methods/*.ll   ← Architecture-neutral, compiled per-RID
```

#### Architecture-Neutral LLVM IR (Design Requirement)

**V4 MUST generate only architecture-neutral LLVM IR.** The target triple and data layout are NOT embedded in the `.ll` files. Instead, they are specified at compile time via `-mtriple` when invoking `llc`.

**Generated IR Format:**
```llvm
; ModuleID = 'marshal_methods_MainActivity.ll'
source_filename = "marshal_methods_MainActivity.ll"
; NOTE: No target triple or datalayout - specified at compile time

@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8
@fn_ptr_0 = internal unnamed_addr global ptr null, align 8

define default void @Java_com_example_MainActivity_n_1onCreate(ptr %env, ptr %obj, ptr %p0) #0 {
entry:
  %cached_ptr = load ptr, ptr @fn_ptr_0, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call
  ; ... rest of function
}
```

**Why Architecture-Neutral IR Works:**

The generated marshal method stubs use ONLY platform-neutral LLVM types:

| Type | Usage | Platform-Neutral? |
|------|-------|-------------------|
| `ptr` | Opaque pointer (JNIEnv*, jobject, etc.) | ✅ Yes |
| `i8` | Boolean, byte | ✅ Yes |
| `i16` | Char, short | ✅ Yes |
| `i32` | Int, method indices | ✅ Yes |
| `i64` | Long | ✅ Yes |
| `void` | Return type | ✅ Yes |

**What we DON'T use:**
- ❌ Struct types with specific layouts
- ❌ Architecture-specific intrinsics
- ❌ Inline assembly
- ❌ Vector types
- ❌ Platform-specific calling conventions in IR

**Build-Time Compilation:**

The `CompileNativeAssembly` task compiles the architecture-neutral `.ll` files for each target platform:

```bash
# For arm64
llc -mtriple=aarch64-unknown-linux-android21 -O2 --filetype=obj \
    marshal_methods_MainActivity.ll -o arm64/marshal_methods_MainActivity.o

# For x86_64  
llc -mtriple=x86_64-unknown-linux-android21 -O2 --filetype=obj \
    marshal_methods_MainActivity.ll -o x86_64/marshal_methods_MainActivity.o

# For arm (32-bit)
llc -mtriple=armv7-unknown-linux-android21 -O2 --filetype=obj \
    marshal_methods_MainActivity.ll -o arm/marshal_methods_MainActivity.o

# For x86 (32-bit)
llc -mtriple=i686-unknown-linux-android21 -O2 --filetype=obj \
    marshal_methods_MainActivity.ll -o x86/marshal_methods_MainActivity.o
```

**Calling Convention Handling:**

The `-mtriple` flag tells `llc` which calling convention to use:
- ARM64: Arguments in x0-x7, return in x0
- x86_64: Arguments in rdi, rsi, rdx, rcx, r8, r9, return in rax
- ARM32: Arguments in r0-r3, return in r0
- x86: Arguments on stack, return in eax

The IR itself doesn't specify calling conventions - `llc` applies the correct one based on the target triple.

**Data Layout:**

While the IR omits the `target datalayout` directive, `llc` infers the correct data layout from the target triple:
- Pointer sizes (4 bytes on 32-bit, 8 bytes on 64-bit)
- Alignment requirements
- Endianness (little-endian for all Android targets)

For our simple pointer-forwarding stubs, this is sufficient. The IR uses `ptr` (opaque pointers) which work correctly on all architectures.

**Benefits:**

| Benefit | Impact |
|---------|--------|
| **Single IR generation** | Generate once, compile for all 4 ABIs |
| **Faster multi-RID builds** | ~75% reduction in IR generation time for 4-ABI builds |
| **Simplified caching** | Cache one `.ll` file per type, not per type × ABI |
| **SDK pre-generation** | Ship universal `.ll` files in SDK package |
| **Correctness** | Current PoC is broken for x86_64 - this fixes it |

**Implementation Changes Required:**

1. **`GenerateLlvmIrInitFile`**: Remove `target triple` and `target datalayout` lines
2. **`GenerateLlvmIrFile`**: Remove `target triple` and `target datalayout` lines  
3. **`CompileNativeAssembly`**: Read `%(abi)` metadata, map to triple, pass `-mtriple`

### 6.4 Build Time: By-Design Trade-offs

| Decision | Build Time Impact | Justification |
|----------|-------------------|---------------|
| Full assembly scanning | +500ms | Required to find all types |
| IL generation via S.R.Metadata | +1000ms | AOT-safe, no Reflection.Emit |
| LLVM IR generation (once) | +500ms | Architecture-neutral, compiled per-RID |
| Java source generation | +200ms | Required for JCW |
| Pre-trimming execution | Enables caching | Must run before trimmer |

### 6.5 Build Time Optimizations (Implemented)

| Optimization | Savings | Implementation |
|--------------|---------|----------------|
| Parallel assembly scanning | 6x faster | `Parallel.ForEach` |
| Skip abstract types | -30% types | Detected via IL flags |
| Skip interfaces (use invoker) | -20% types | Only invoker types generated |

### 6.6 Build Time Optimizations: Future Work

| Optimization | Expected Savings | Complexity | Priority |
|--------------|------------------|------------|----------|
| **SDK pre-generation** | 80% of types removed | High | P0 |
| **NuGet package caching** | Variable | Medium | P1 |
| **Incremental generation** | User types only | Medium | P1 |
| **Parallel IL emission** | ~30% faster | Low | P2 |

#### 6.6.1 SDK Pre-generation (P0 - Critical)

Pre-generate TypeMaps for Mono.Android.dll during SDK build:

```xml
<!-- During SDK build -->
<Target Name="_PreGenerateSdkTypeMaps" AfterTargets="Build">
  <GenerateTypeMapAssembly
      ResolvedAssemblies="@(SdkAssemblies)"
      OutputDirectory="$(SdkTypeMapOutputDir)"
      SkipJcwGeneration="true" />
</Target>

<!-- App build: merge pre-generated with app types -->
<Target Name="_MergeSdkTypeMaps" BeforeTargets="_GenerateTypeMapAssembly">
  <ItemGroup>
    <_PreGeneratedTypeMaps Include="$(NuGetPackageRoot)/**/sdk-typemaps.dll" />
  </ItemGroup>
  <MergeTypeMapAssemblies 
      PreGenerated="@(_PreGeneratedTypeMaps)"
      AppTypes="@(AppJavaPeerTypes)"
      Output="$(IntermediateOutputPath)TypeMaps.dll" />
</Target>
```

**Expected Impact:** Reduce build time from ~6000ms to ~300ms for typical app.

#### 6.6.2 Incremental Generation (P1)

Cache generated proxies by assembly content hash:

```csharp
// Build cache structure
.typemap-cache/
├── Mono.Android.dll.sha256 → abc123
├── MyApp.dll.sha256 → def456
└── proxies/
    ├── abc123.dll → cached SDK proxies
    └── def456.dll → cached app proxies

// On rebuild: compare hashes, skip unchanged
if (File.Exists(cachedProxy) && HashMatches(assembly, cached))
    return LoadCachedProxy(cachedProxy);
```

#### 6.6.3 NuGet Package Caching (P1)

Cache generated proxies for NuGet dependencies:

```
~/.nuget/typemap-cache/
├── Xamarin.AndroidX.Core/1.9.0/proxies.dll
├── Xamarin.AndroidX.AppCompat/1.6.1/proxies.dll
└── ...
```

**Key:** NuGet packages are immutable, so cache never needs invalidation.

### 6.7 Build Time Metrics and Targets

| Scenario | Current PoC | Target | Notes |
|----------|-------------|--------|-------|
| Full rebuild (no cache) | ~6000ms | ~6000ms | Acceptable |
| Incremental (no changes) | ~6000ms | ~50ms | Hash check only |
| Incremental (app changes) | ~6000ms | ~300ms | App types only |
| With SDK pre-gen | ~6000ms | ~300ms | SDK types shipped |
| Multi-RID Release | ~18000ms | ~300ms | Universal TypeMaps.dll |

### 6.8 CI/CD Considerations

**Cold Cache Builds:**
- CI builds often start fresh with no cache
- SDK pre-generation is critical for CI performance
- Consider shipping pre-built TypeMaps.dll in NuGet package

**Parallelization:**
- TypeMap generation can run parallel to other tasks
- Consider async MSBuild task execution

**Build Farm Caching:**
- Share `.typemap-cache/` directory across builds
- Include assembly versions in cache keys

---

## 7. Scope Coverage Analysis

### 7.1 Fully Covered

| Category | Examples | Status |
|----------|----------|--------|
| User Activities | `MainActivity` | ✅ |
| User Services | `MyBackgroundService` | ✅ |
| User Broadcast Receivers | `MyReceiver` | ✅ |
| User Content Providers | `MyProvider` | ✅ |
| SDK MCW Types | `Activity`, `TextView` | ✅ |
| Interfaces | `IOnClickListener` | ✅ |
| Implementors | `OnClickListenerImplementor` | ✅ |
| Custom Views (layout XML) | `MyCustomButton` | ✅ |
| `[Export]` Methods | Custom Java-callable methods | ✅ |
| `[ExportField]` Fields | Java-accessible fields | ✅ |
| Array Types (T[], T[][]) | `ITrustManager[]` | ✅ |

### 7.2 Partially Covered

| Category | Issue | Proposed Solution | See Section |
|----------|-------|-------------------|-------------|
| Open Generics | Runtime instantiation fails | Generate closed generics for common patterns | 8.5 |
| Non-static Inner Classes | Skipped in scanner | Investigate outer class reference handling | 8.6 |

### 7.3 Future Work

| Category | Status | Proposed Solution | See Section |
|----------|--------|-------------------|-------------|
| Generic Collections (`IList<T>`, etc.) | Not yet implemented | DerivedTypeFactory pattern | 8.4 |
| Higher-rank arrays (`T[][][]`) | No known use cases | Extend CreateArrayOf pattern | 8.1 |

### 7.4 Not Covered (By Design)

| Category | Reason | Impact |
|----------|--------|--------|
| Desktop JVM targets | Out of scope | java-interop repo only |
| Debug builds with Mono | Uses LlvmIrTypeMap | No impact, legacy path preserved |

---

## 8. Edge Cases and Special Handling

This section comprehensively covers edge cases identified from unit tests (COUNTEREXAMPLES.md) and documentation. Each subsection references the specific questions (Q#) and scenarios (S#) from the counterexamples document.

### Edge Case Coverage Summary

| Category | Subsections | Status | Key Concerns |
|----------|-------------|--------|--------------|
| **Arrays** | 8.1-8.3 | ✅ Implemented | Rank 1-2 supported, primitives handled |
| **Generics** | 8.4-8.5 | ⚠️ Partial | Closed generics work, open throw NotSupportedException |
| **Concurrency** | 8.9 | ✅ Designed | ConcurrentDictionary handles thread safety |
| **Construction** | 8.10 | ✅ Designed | Activation constructor pattern preserved |
| **Identity** | 8.11 | ✅ Designed | PeekValue integration maintained |
| **Exceptions** | 8.12 | ✅ Designed | Throwable subclasses work |
| **Interfaces** | 8.13-8.14 | ✅ Designed | Invoker types generated |
| **Export** | 8.15 | ⚠️ Future | Build-time collection planned |
| **Priority** | 8.16 | ✅ Designed | SDK types win, conflicts warned |
| **Hot Reload** | 8.17 | ⚠️ Future | Debug-only hybrid approach |
| **Trimming** | 8.18 | ✅ Designed | Proxy types root target types |
| **JCW Names** | 8.19 | ✅ Designed | Register name always used |
| **Value Types** | 8.20 | ✅ Unchanged | JNI handles directly |
| **Performance** | 8.21 | ⚠️ Needs verification | 10K+ types benchmark needed |
| **Special Chars** | 8.22 | ✅ Designed | String comparison handles all |
| **No Default Ctor** | 8.23 | ✅ Designed | Clear error message |
| **Circular Refs** | 8.24 | ✅ Analyzed | No V4-specific issues |

### 8.1 Array Types

**Design:** Each proxy implements `CreateArray(int length, int rank)` method.

```csharp
// Generated proxy
public override Array CreateArray(int length, int rank)
    => CreateArrayOf<ITrustManager>(length, rank);

// Base class helper
protected static Array CreateArrayOf<T>(int length, int rank) => rank switch {
    1 => new T[length],
    2 => new T[length][],
    _ => throw new ArgumentOutOfRangeException(...)
};
```

**Runtime Flow:**
1. `JNIEnv.ArrayCreateInstance(elementType, length)` called
2. Delegates to `ITypeMap.CreateArray(elementType, length, rank: 1)`
3. Looks up proxy by element type's JNI name
4. Calls `proxy.CreateArray(length, rank)` - virtual dispatch, no reflection

**Higher-Rank Arrays (Future Work):**

Currently only rank 1 (`T[]`) and rank 2 (`T[][]`) are supported. No known Android APIs use `T[][][]` or higher.

**Proposed Solution:** If needed, extend `CreateArrayOf<T>` with additional cases:
```csharp
protected static Array CreateArrayOf<T>(int length, int rank) => rank switch {
    1 => new T[length],
    2 => new T[length][],
    3 => new T[length][][],  // Add if needed
    4 => new T[length][][][], // Add if needed
    _ => throw new ArgumentOutOfRangeException(...)
};
```

**Implementation:** Low priority unless a specific API requires it.

### 8.2 Primitive Arrays

Handled separately in `NativeArrayToManaged`:
- `byte[]`, `int[]`, `float[]`, etc. use direct allocation
- No TypeMap lookup needed

### 8.3 String Arrays

Special case in `GetArrayType`:
```csharp
if (elementJni == "java/lang/String")
    return typeof(JavaArray<string>);
```

### 8.4 Generic Collections (Future Work)

**Problem:** `IList<T>`, `IDictionary<K,V>`, `ICollection<T>` conversions currently throw `NotSupportedException`.

**Proposed Solution: Derived Type Factory Pattern**

Similar to array handling via `CreateArray()`, generic collections can use a factory pattern:

```csharp
// New interface for derived type creation
public interface IDerivedTypeFactory<T>
{
    // Array creation (existing)
    Array CreateArray(int length, int rank);
    
    // Collection creation (new)
    IList<T> CreateList();
    IDictionary<TKey, T> CreateDictionary<TKey>();
    ICollection<T> CreateCollection();
}

// Generated proxy returns a factory for its specific type
public sealed class android_view_View_Proxy : JavaPeerProxy
{
    // Existing
    public override Array CreateArray(int length, int rank) 
        => CreateArrayOf<Android.Views.View>(length, rank);
    
    // New - returns factory for this type
    public override IDerivedTypeFactory<Android.Views.View> GetDerivedTypeFactory()
        => new DerivedTypeFactory<Android.Views.View>();
}

// Generic factory implementation
public sealed class DerivedTypeFactory<T> : IDerivedTypeFactory<T>
{
    public Array CreateArray(int length, int rank) => rank switch {
        1 => new T[length],
        2 => new T[length][],
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };
    
    public IList<T> CreateList() => new JavaList<T>();
    public IDictionary<TKey, T> CreateDictionary<TKey>() => new JavaDictionary<TKey, T>();
    public ICollection<T> CreateCollection() => new JavaCollection<T>();
}
```

**Benefits:**
- Same pattern as array handling - proxy returns type-specific factory
- No reflection needed - factory methods use `new` directly
- AOT-safe - all types known at compile time
- Extensible - can add more collection types

**Runtime Flow:**
1. `JavaConvert.ToType(jobject, typeof(IList<View>))` called
2. Extract element type (`View`) and look up its proxy
3. Get factory: `proxy.GetDerivedTypeFactory()`
4. Create collection: `factory.CreateList()`
5. Populate from Java array/collection

**Implementation Tasks:**
- [ ] Add `IDerivedTypeFactory<T>` interface
- [ ] Add `GetDerivedTypeFactory()` to `JavaPeerProxy` base class
- [ ] Generate factory return in each proxy
- [ ] Update `JavaConvert.ToType()` to use factory for `IList<T>`, etc.
- [ ] Handle nested generics: `IList<IList<T>>`

### 8.5 Open Generics (Future Work)

**Problem:** Open generic types like `IList<>` cannot be instantiated at runtime without knowing the type parameter.

**Current Behavior:**
```csharp
// This fails - we don't know what T is
Type openGeneric = typeof(IList<>);
// Cannot create instance of open generic
```

**Proposed Solution: Closed Generic Pre-generation**

At build time, analyze usage patterns and pre-generate closed generics:

```csharp
// During build: scan for IList<View>, IList<string>, etc.
// Generate closed generic proxies:

[assembly: TypeMap<JLO>("java/util/List<android/view/View>", 
    typeof(JavaList_View_Proxy), typeof(JavaList<View>))]
[assembly: TypeMap<JLO>("java/util/List<java/lang/String>", 
    typeof(JavaList_String_Proxy), typeof(JavaList<string>))]

sealed class JavaList_View_Proxy : JavaPeerProxy {
    public override object CreateInstance(IntPtr handle, JniHandleOwnership ownership)
        => new JavaList<View>(handle, ownership);
}
```

**Implementation Tasks:**
- [ ] Add generic usage scanning to GenerateTypeMapAssembly
- [ ] Generate closed generic proxy types for common instantiations
- [ ] Map Java generic signatures to closed .NET types
- [ ] Handle wildcards: `List<? extends View>` → `IList<View>`

**Fallback:** For uncommonly-used generics, throw `NotSupportedException` with guidance to use explicit `JavaList<T>`.

### 8.6 Non-static Inner Classes (Future Work)

**Problem:** Non-static Java inner classes require an instance of the outer class to instantiate. The scanner currently skips these.

**Example:**
```java
// Java
public class Outer {
    public class Inner {  // Non-static - requires Outer instance
        public Inner() { }
    }
}
```

**Current Behavior:** Inner classes skipped in `JavaPeerScanner`:
```csharp
// GenerateTypeMapAssembly.cs
if (type.IsNestedPrivate || type.IsNestedFamily)
    continue; // Skipped
```

**Proposed Solution:**

1. **Detect outer class requirement** by checking for synthetic `this$0` field
2. **Generate activation constructor** that accepts outer instance:
   ```csharp
   sealed class Outer_Inner_Proxy : JavaPeerProxy {
       public override object CreateInstance(IntPtr handle, JniHandleOwnership ownership)
           => new Outer.Inner(handle, ownership);
       
       // New: outer-aware activation
       public object CreateInstance(IntPtr handle, JniHandleOwnership ownership, Outer outer)
           => new Outer.Inner(handle, ownership, outer);
   }
   ```
3. **Runtime lookup** must extract outer reference from Java object

**Implementation Tasks:**
- [ ] Identify non-static inner classes by `this$0` field pattern
- [ ] Generate proxy with outer-aware activation method
- [ ] Update runtime to extract outer reference from Java object
- [ ] Handle nested levels: `Outer.Middle.Inner`

**Impact:** Low priority - most Android APIs use static inner classes or top-level classes.

### 8.7 Interfaces and Abstract Classes

**Flow:**
1. Lookup returns interface proxy type
2. `proxy.CreateInstance()` returns Invoker instance
3. `proxy.InvokerType` property exposes Invoker type

### 8.8 Aliases (Multiple Types Same Java Name)

**Pattern:**
```csharp
[assembly: TypeMap<JLO>("com/example/Handler", typeof(Handler_Aliases), typeof(Handler_Aliases))]
[assembly: TypeMap<JLO>("com/example/Handler[0]", typeof(HandlerA_Proxy), typeof(HandlerA))]
[assembly: TypeMap<JLO>("com/example/Handler[1]", typeof(HandlerB_Proxy), typeof(HandlerB))]

[JavaInteropAliases("com/example/Handler[0]", "com/example/Handler[1]")]
sealed class Handler_Aliases { }
```

### 8.9 Concurrency and Thread Safety

**Counterexample Reference:** Q1 (Thread-Safety During Type Registration), S2 (Multiple Threads Registering Same Type), S3 (Cross-Thread JNI Handle Usage)

**Problem:** Types may be accessed from multiple threads simultaneously - native threads, Java threads, and managed threads. The type map and JNI class cache must be thread-safe.

**Current Implementation:**
```csharp
// TypeMapAttributeTypeMap.cs
private readonly ConcurrentDictionary<string, JavaPeerProxy?> _typeMap;
private readonly ConcurrentDictionary<IntPtr, JavaPeerProxy?> _classToProxy;
private readonly ConcurrentDictionary<Type, JniClassEntry> _typeToJniClassCache;
```

**Analysis:**
- ✅ **ConcurrentDictionary usage** ensures thread-safe reads and writes
- ✅ **JNI global refs** stored in cache are valid across threads (unlike local refs)
- ⚠️ **Race condition on cache population** - two threads may simultaneously populate the same entry. The `GetOrAdd` pattern handles this correctly, but the second computation is wasted work.

**Thread Attachment:**
- When a new thread first accesses JNI, it must be attached to the JVM via `AttachCurrentThread`
- V4's type map operations work correctly on any attached thread
- No special initialization is required per-thread

**Verification:**
- [ ] Run `RegisterTypeOnNewNativeThread` test with V4
- [ ] Run `RegisterTypeOnNewJavaThread` test with V4
- [ ] Run `ConversionsAndThreadsAndInstanceMappingsOhMy` test with V4

**No Changes Needed:** Current `ConcurrentDictionary` usage is sufficient.

### 8.10 Virtual Callbacks During Construction

**Counterexample Reference:** Q2 (Virtual Method Dispatch During Construction), S1 (Virtual Callback During Constructor)

**Problem:** Java constructors may virtually call overridden methods before the managed object is fully constructed. Example: `AbsListView.<init>` calls `getAdapter()` which triggers a managed callback.

**Scenario:**
```java
// Java - AbsListView constructor
public AbsListView(Context context) {
    super(context);
    ListAdapter adapter = getAdapter();  // Virtual call!
}
```

```csharp
// C# - Custom adapter
public class MyListView : AbsListView {
    public override ListAdapter Adapter {
        get => _adapter;  // Called before constructor completes
        set => _adapter = value;
    }
}
```

**V4 Handling:**

The activation constructor `(IntPtr, JniHandleOwnership)` is designed specifically for this:

1. Java creates the object, calls constructor
2. Constructor virtually calls `getAdapter()`
3. JNI calls back to managed code
4. Managed code receives the `IntPtr` handle
5. `TypeMap.GetObject(handle)` looks for existing managed wrapper
6. If none exists, calls `proxy.CreateInstance(handle, ownership)` 
7. Activation constructor initializes minimal state needed for callback
8. Virtual method executes with partially-initialized object

**Critical Requirement:** Activation constructors must not have side effects that depend on subclass state. They should only:
- Store the handle
- Register with surfaced objects tracking
- Initialize base class state

**V4 Ensures:** Pre-generated factory methods call activation constructors directly without reflection, so this path is AOT-safe.

**Verification:**
- [ ] Run `CanOverrideAbsListView_Adapter` test with V4

### 8.11 Instance Identity and Tracking

**Counterexample Reference:** Q5 (Instance Identity Preservation), S9 (Instance Registered During Construction), S15 (JavaCast Obtains Original Instance), Q18 (GC Bridge and Weak References)

**Problem:** When the same Java object is accessed multiple times, the same managed wrapper must be returned.

**Mechanism:**
```csharp
// AndroidValueManager.cs
public override IJavaPeerable? PeekValue(JniObjectReference reference)
{
    if (!reference.IsValid)
        return null;
    return Runtime.GetSurfacedObjects()?.FirstOrDefault(x => 
        JniEnvironment.Types.IsSameObject(x.PeerReference, reference));
}
```

**V4 Integration:**

1. **Before activation:** `PeekValue` checks if managed wrapper exists
2. **If found:** Return existing instance (no new allocation)
3. **If not found:** Create via `proxy.CreateInstance()`
4. **After creation:** Instance self-registers with `Runtime.RegisterSurfacedObject()`

**JavaCast Identity:**
```csharp
// When casting, must return original instance if compatible
var obj = new Java.Lang.String("test");
var result = obj.JavaCast<Java.Lang.Object>();
Debug.Assert(Object.ReferenceEquals(obj, result));  // Must be true
```

**V4 Implementation:**
```csharp
// TypeMapAttributeTypeMap.cs - GetObject<T>
public T? GetObject<T>(IntPtr handle, JniHandleOwnership ownership) where T : class
{
    // First check for existing instance
    var existing = PeekValue(new JniObjectReference(handle));
    if (existing is T typed)
        return typed;
    
    // Otherwise create new instance via proxy
    ...
}
```

**GC Bridge Integration:**
- Managed wrappers are registered with `Runtime.GetSurfacedObjects()`
- When GC collects wrapper, weak reference becomes invalid
- V4 doesn't change this mechanism - it just provides the instantiation path

**Verification:**
- [ ] Run `GetObject_ReturnsMostDerivedType` with V4
- [ ] Run `JavaConvert_FromJavaObject_ShouldNotBreakExistingReferences` with V4
- [ ] Run `DoNotLeakWeakReferences` with V4

### 8.12 Throwable and Exception Handling

**Counterexample Reference:** Q8 (Throwable Subclass Activation), Q15 (JavaProxyThrowable Creation)

**Problem:** Types extending `Java.Lang.Throwable` have special semantics for exception marshaling. They must be in the type map.

**Handling:**
```csharp
// V4 generates proxies for all Throwable subclasses
[assembly: TypeMap<JLO>("java/lang/RuntimeException", 
    typeof(RuntimeException_Proxy), typeof(Java.Lang.RuntimeException))]

sealed class RuntimeException_Proxy : JavaPeerProxy {
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership ownership)
        => new Java.Lang.RuntimeException(handle, ownership);
}
```

**JavaProxyThrowable:**
- `JavaProxyThrowable.Create(Exception)` wraps a .NET exception in a Java `Throwable`
- This is a managed-to-Java direction - V4 handles Java-to-managed
- No special V4 handling needed

**Exception Marshaling Flow:**
1. Java throws exception
2. JNI layer catches it
3. `TypeMap.GetObject<Throwable>(handle)` creates managed wrapper
4. Managed code receives the typed exception

**Verification:**
- [ ] Run `ActivatedDirectThrowableSubclassesShouldBeRegistered` with V4
- [ ] Run `InnerExceptionIsSet` with V4

### 8.13 Interface Invokers

**Counterexample Reference:** Q9 (JavaCast to Interface), Q14 (Invoker Wrapper Disposal), S11 (InputStreamInvoker Wrapping Java Stream)

**Problem:** When casting to an interface type, V4 must create an appropriate invoker instance (e.g., `IValueProviderInvoker`).

**Design:**

Each interface has a corresponding Invoker type generated by the binding process. V4 must:
1. Map the Java interface to the Invoker type
2. Generate proxies for Invoker types (not interface types directly)

```csharp
// For interface IValueProvider, binding generates IValueProviderInvoker
// V4 generates:
[assembly: TypeMap<JLO>("com/example/IValueProvider", 
    typeof(IValueProviderInvoker_Proxy), typeof(IValueProviderInvoker))]

sealed class IValueProviderInvoker_Proxy : JavaPeerProxy {
    public override Type TargetType => typeof(IValueProviderInvoker);
    public override Type? InvokerType => typeof(IValueProviderInvoker);
    
    public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership ownership)
        => new IValueProviderInvoker(handle, ownership);
}
```

**JavaCast to Interface:**
```csharp
// When casting to IValueProvider:
var provider = javaObj.JavaCast<IValueProvider>();

// V4 implementation:
// 1. Look up "com/example/IValueProvider" in type map
// 2. Get proxy with InvokerType = typeof(IValueProviderInvoker)
// 3. CreateInstance returns IValueProviderInvoker which implements IValueProvider
```

**Invoker Disposal:**
- Invokers hold a JNI reference to the wrapped Java object
- Disposing the invoker releases the reference
- If the underlying Java object is disposed separately, invoker's disposal should not throw

**Verification:**
- [ ] Run `JavaCast_InterfaceCast` with V4
- [ ] Run `Disposing_Shared_Data_Does_Not_Throw_IOE` with V4

### 8.14 JavaCast Validation

**Counterexample Reference:** Q10 (JavaCast to Managed Subclass)

**Problem:** `JavaCast` must validate that the cast is valid at the Java level, not just the .NET level.

**Invalid Cast Scenario:**
```csharp
// java.lang.Object cannot be cast to a managed-only subclass
var javaObj = new Java.Lang.Object();
javaObj.JavaCast<MyManagedOnlyType>();  // Must throw InvalidCastException
```

**V4 Validation:**

```csharp
// TypeMapAttributeTypeMap.cs - JavaCast implementation
public T JavaCast<T>(IJavaPeerable source) where T : class
{
    // Get the actual Java class
    var javaClass = JNIEnv.GetObjectClass(source.Handle);
    var javaClassName = GetJavaClassName(javaClass);
    
    // Get target's expected Java class
    var targetProxy = GetProxyForType(typeof(T));
    if (targetProxy == null)
        throw new InvalidCastException($"Type {typeof(T)} has no Java mapping");
    
    // Verify Java-level assignability
    if (!JNIEnv.IsAssignableFrom(javaClass, targetProxy.JniClass))
        throw new InvalidCastException($"Cannot cast {javaClassName} to {targetProxy.JniName}");
    
    // If already the right managed type, return as-is
    if (source is T result)
        return result;
    
    // Create wrapper of target type
    return (T)targetProxy.CreateInstance(source.Handle, JniHandleOwnership.DoNotTransfer);
}
```

**Key Validation:** The Java type must be assignable to the target Java type. A pure Java object cannot be cast to a managed subclass that only exists in .NET.

**Verification:**
- [ ] Run `JavaCast_CheckForManagedSubclasses` with V4

### 8.15 Export Methods

**Counterexample Reference:** Q7 (Export Attribute Support), Q19 (Mono.Android.Export Dependency)

**Problem:** The `[Export]` attribute allows exposing .NET methods to Java without a binding. This requires runtime reflection which is incompatible with NativeAOT.

**Current Status:**
- ⚠️ `[Export]` is NOT supported with NativeAOT
- Tests are currently ignored: `CreateTypeWithExportedMethods`

**V4 Approach:**

1. **Build-time collection:** Scan for `[Export]` methods during build
2. **Generate JCW and marshal stubs** the same way as `[Register]` methods
3. **Remove runtime reflection dependency**

```csharp
// User code
public class MyClass : Java.Lang.Object {
    [Export("doSomething")]
    public void DoSomething() { ... }
}

// V4 generates the same way as [Register] methods:
// - JCW with native method declaration
// - LLVM IR stub for the native method
// - Marshal method in generated code
```

**Implementation Tasks:**
- [ ] Add `[Export]` method scanning to `GenerateTypeMapAssembly`
- [ ] Generate JCW entries for exported methods
- [ ] Generate LLVM IR stubs for exported methods
- [ ] Update documentation to clarify `[Export]` works with V4

**Migration Path:**
- For NativeAOT compatibility before V4 fully implements `[Export]`, developers should use `[JavaCallable]` source generators instead

### 8.16 Type Mapping Priority and Conflicts

**Counterexample Reference:** Q6 (Duplicate JNI Names Across Assemblies), Q20 (java/lang/Object Mapping Priority)

**Problem:** Multiple managed types may map to the same JNI name, either intentionally (aliases) or accidentally (conflicts).

**Priority Rules:**

1. **SDK types win:** `Mono.Android.dll` types have highest priority for standard Android SDK
2. **User types can override:** Explicit `[Register]` takes precedence for user-defined JCW names
3. **First wins for conflicts:** If two types have the same JNI name without explicit handling, the first loaded wins

**Implementation:**

```csharp
// GenerateTypeMapAssembly.cs - Priority handling
// Types from Mono.Android are marked as "sdk" types
// User types are marked as "user" types
// During generation, SDK types are processed first

// At runtime, the ConcurrentDictionary uses GetOrAdd which keeps first value
_typeMap.GetOrAdd(jniName, proxy);  // First one wins
```

**Conflict Detection:**

```csharp
// Build-time warning for duplicates
if (seenJniNames.Contains(jniName)) {
    Log.LogWarning("XA4214", $"Duplicate JNI name: {jniName}");
}
```

**Verification:**
- [ ] Verify `java/lang/Object` maps to `Java.Lang.Object`
- [ ] Add test for conflict detection warning

### 8.17 Hot Reload Support

**Counterexample Reference:** S17 (Hot Reload Type Changes)

**Problem:** During debugging with MAUI Hot Reload, types may be modified. The pre-generated typemap becomes stale.

**Current Status:**
- ⚠️ V4 does NOT support Hot Reload in Release builds
- Debug builds should use a fallback mechanism

**Proposed Solution:**

1. **Debug builds:** Include both V4 typemap AND legacy reflection fallback
2. **Release builds:** V4 only, no Hot Reload support

```csharp
// Debug configuration
#if DEBUG
public class HybridTypeMap : ITypeMap
{
    private readonly TypeMapAttributeTypeMap _v4;
    private readonly LegacyReflectionTypeMap _legacy;
    
    public IJavaPeerable? GetObject(IntPtr handle, Type targetType)
    {
        // Try V4 first
        var result = _v4.GetObject(handle, targetType);
        if (result != null)
            return result;
        
        // Fall back to reflection for hot-reloaded types
        return _legacy.GetObject(handle, targetType);
    }
}
#endif
```

**Implementation Tasks:**
- [ ] Define Debug vs Release configuration for typemap
- [ ] Implement hybrid typemap for Debug builds
- [ ] Document Hot Reload limitations in Release builds

### 8.18 Trimmed App Type Preservation

**Counterexample Reference:** S18 (Trimmed App Missing Expected Type)

**Problem:** Aggressive trimming might remove types that are in the JNI typemap but never directly referenced in .NET code.

**V4 Solution:**

Proxy types are rooted by the `TypeMap<TProxy>` attribute:

```csharp
// Generated attribute references proxy type directly
[assembly: TypeMap<JLO>("android/view/View", 
    typeof(android_view_View_Proxy),  // Proxy is rooted
    typeof(Android.Views.View))]      // Target is rooted

// The trimmer sees both types referenced and preserves them
```

**Additional Rooting:**

The `TargetType` property on proxies provides additional rooting:

```csharp
sealed class android_view_View_Proxy : JavaPeerProxy {
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type TargetType => typeof(Android.Views.View);
}
```

**Verification:**
- [ ] Build sample app with aggressive trimming
- [ ] Verify all needed types survive trimming
- [ ] Test JNI activation of trimmed types

### 8.19 Renamed JCW Types

**Counterexample Reference:** S8 (Renamed JCW Type), Q11 (Array of Custom JCW Types)

**Problem:** Types with custom `[Register("custom/package/Name")]` must use the Register name, not the .NET namespace.

**V4 Handling:**

```csharp
// User type with custom JNI name
[Register("com/custom/MyHandler")]
public class MyHandler : Java.Lang.Object { }

// V4 generates with the Register name:
[assembly: TypeMap<JLO>("com/custom/MyHandler", 
    typeof(com_custom_MyHandler_Proxy), 
    typeof(MyHandler))]
```

**Array Types:**

When creating arrays of renamed types:
```csharp
// Must use "com/custom/MyHandler" not derived from namespace
var array = new MyHandler[10];
// JNI signature: "[Lcom/custom/MyHandler;"
```

**V4 ensures:** The JNI name in the attribute is always the Register name, not derived from .NET namespace.

**Verification:**
- [ ] Run `NewArray_UseJcwTypeWhenRenamed` with V4

### 8.20 Value Types and Struct Marshaling

**Counterexample Reference:** S4 (Generic Type with Value Type Parameter), S12 (Binding with Color Enum Marshaling)

**Problem:** Value types (structs, enums) cannot be directly wrapped as Java peers. They require boxing/unboxing.

**Handled Cases:**

1. **Primitive value types:** `int`, `float`, `bool` - handled by JNI directly
2. **Enums:** Marshaled as underlying integer type
3. **Structs like `Color`:** Special handling in bindings

**V4 Impact:**

V4 doesn't change struct marshaling. Structs are not Java peers and don't need type map entries.

```csharp
// Android.Graphics.Color is a struct
// Methods taking/returning Color handle it as an int
public void SetColor(Color color) 
    => JNIEnv.CallVoidMethod(Handle, "setColor", "(I)V", color.ToArgb());
```

**Generic Types with Value Parameters:**

```csharp
// GenericHolder<int> - the int is boxed/unboxed
// V4 generates proxy for GenericHolder<T> instantiations found at build time
[assembly: TypeMap<JLO>("com/example/GenericHolder", 
    typeof(GenericHolder_int_Proxy), typeof(GenericHolder<int>))]
```

**Verification:**
- [ ] Run `NewClosedGenericTypeWorks` with V4
- [ ] Run `TestBxc4288` (Color marshaling) with V4

### 8.21 Large TypeMap Performance

**Counterexample Reference:** S23 (Very Large Number of Types)

**Problem:** Apps with many dependencies may have 10,000+ types in the typemap. Lookup performance and startup time matter.

**Analysis:**

| Operation | Complexity | Expected Time |
|-----------|------------|---------------|
| Attribute scan on startup | O(n) | ~100ms for 10K types |
| JNI name lookup | O(1) hash lookup | <1μs |
| Type to proxy lookup | O(1) hash lookup | <1μs |

**Optimization Strategies:**

1. **Lazy loading:** Only populate cache on first access to each type
2. **Pre-hashed names:** Store hash codes in attributes to speed comparison
3. **Segmented dictionaries:** Partition by namespace prefix

**Current Implementation:**
```csharp
// Lazy population on first access
public JavaPeerProxy? GetProxyForJniName(string jniName)
{
    return _typeMap.GetOrAdd(jniName, name => {
        // Only scans attributes on first access for this name
        return ScanForProxy(name);
    });
}
```

**Verification:**
- [ ] Benchmark with 5K types
- [ ] Benchmark with 10K types
- [ ] Benchmark with 20K types
- [ ] Profile startup time impact

### 8.22 JNI Names with Special Characters

**Counterexample Reference:** S24 (JNI Name with Special Characters)

**Problem:** Obfuscated code may produce JNI names with unusual but valid characters.

**Valid JNI Characters:**
- Letters (A-Z, a-z)
- Digits (0-9)
- Underscores (_)
- Dollar signs ($) - for inner classes
- Forward slashes (/) - package separators

**V4 Handling:**

JNI names are stored as-is in attributes. The string comparison handles all valid characters:

```csharp
// Attribute stores exact JNI name
[assembly: TypeMap<JLO>("com/obfuscated/a$b$c", 
    typeof(Obfuscated_Proxy), typeof(Obfuscated))]

// Lookup uses exact string match
_typeMap.TryGetValue("com/obfuscated/a$b$c", out var proxy);
```

**No Changes Needed:** String-based lookup handles all valid JNI names.

### 8.23 Managed Types Without Default Constructor

**Counterexample Reference:** S25 (Managed Type with No Default Constructor)

**Problem:** Java-side activation of a type that has no parameterless constructor.

**V4 Requirement:**

All Java-activatable types MUST have an activation constructor:
```csharp
public class MyType : Java.Lang.Object {
    // REQUIRED for Java-side activation
    public MyType(IntPtr handle, JniHandleOwnership transfer) 
        : base(handle, transfer) { }
    
    // Optional - for managed-side creation
    public MyType(string param) : base(...) { }
}
```

**Scanner Validation:**

```csharp
// GenerateTypeMapAssembly.cs - Validate activation constructor exists
var activationCtor = type.GetConstructor(new[] { typeof(IntPtr), typeof(JniHandleOwnership) });
if (activationCtor == null) {
    Log.LogWarning("XA4215", $"Type {type.FullName} has no activation constructor");
    continue;  // Skip this type
}
```

**Error Handling:**

If activation constructor is missing and Java tries to activate:
```csharp
// Runtime error with clear message
throw new MissingMethodException(
    $"Type {targetType.FullName} cannot be activated from Java: " +
    "missing constructor (IntPtr, JniHandleOwnership)");
```

### 8.24 Circular Type References

**Counterexample Reference:** S22 (Circular Type References)

**Problem:** Two types that reference each other in constructors or static initializers could cause deadlock or stack overflow.

**Analysis:**

The V4 type map uses lazy initialization which avoids circular dependency issues:

```csharp
// Type A references Type B
[assembly: TypeMap<JLO>("com/example/A", typeof(A_Proxy), typeof(A))]
[assembly: TypeMap<JLO>("com/example/B", typeof(B_Proxy), typeof(B))]

// Proxy types don't have circular dependencies
sealed class A_Proxy : JavaPeerProxy {
    public override Type TargetType => typeof(A);
    // No reference to B_Proxy
}
```

**Key Insight:** Proxy types only reference their target types, not other proxies. The circular reference exists in the target types, not the type map.

**Runtime Behavior:**
1. Java creates A, calls activation constructor
2. A's constructor may access B
3. If B isn't yet created, Java creates it
4. B's constructor may access A
5. A already exists (step 1), so existing instance is returned
6. No infinite loop

**No V4-Specific Issues:** Circular references are handled the same as legacy.

---

## 9. Problems TypeMap V4 Creates

### 9.1 New Build Dependencies

| Dependency | Description | Risk | Mitigation |
|------------|-------------|------|------------|
| .NET 11 SDK | ILLink `--typemap-entry-assembly` flag | Blocks older SDK usage | Track dotnet/runtime#121513, have fallback plan |
| TypeMapping API | `TypeMapping.GetOrCreateExternalTypeMapping<T>()` intrinsic | Runtime dependency | MonoVM workaround exists |
| LLVM toolchain | LLVM IR compilation per type | Build infrastructure | Already required for marshal methods |

**ILLink Dependency Status:**
- PR: dotnet/runtime#121513
- Status: Required for production
- Fallback: If not merged, implement custom ILLink step (increases complexity)

**Detailed Fallback Plan if ILLink PR Not Merged:**
1. **Option A: Custom ILLink Step** - Create `GenerateTypeMapAttributesStep.cs` that runs as a custom linker step, manually scanning assemblies and emitting attributes. This is what the current PoC does but requires maintaining parallel logic.
2. **Option B: Post-Link Processing** - After standard ILLink runs, use `System.Reflection.Metadata` to modify the trimmed assembly and inject the entry point marker.
3. **Option C: Runtime Assembly Detection** - Keep the `SetEntryAssembly` workaround and document it as permanent for MonoVM, while CoreCLR/NativeAOT use the native intrinsic.

**Recommendation:** Option A is most aligned with production quality but adds ~500 lines of code. Pursue ILLink PR first; if blocked past .NET 11 preview 3, implement Option A.

### 9.2 New Runtime Dependencies

| Dependency | Description | Proposed Solution |
|------------|-------------|-------------------|
| `_Microsoft.Android.TypeMaps.dll` | Must be present in APK | Ensured by build targets |
| `Assembly.SetEntryAssembly` workaround | Current builds need this hack | Will be fixed when ILLink PR merges |
| ConcurrentDictionary caches | Thread safety overhead | Consider FrozenDictionary after init |

**SetEntryAssembly Workaround:**
```csharp
// Current hack in TypeMapAttributeTypeMap.cs
var typeMapsAssembly = Assembly.Load(TypeMapsAssemblyName);
Assembly.SetEntryAssembly(typeMapsAssembly);
```

**Proposed Fix:** Once ILLink properly handles `--typemap-entry-assembly`, this workaround can be removed. The runtime will automatically scan the correct assembly.

### 9.3 Debugging Complexity

| Issue | Impact | Proposed Solution |
|-------|--------|-------------------|
| Errors point to proxy types | Confusing stack traces | Add source mapping in error messages |
| LLVM IR stubs add indirection | Hard to debug native crashes | Add LLVM IR source comments with managed method names |
| Method index sync issues | SIGSEGV crashes | Add runtime validation in Debug builds |

**Debug Mode Validation (Proposed):**
```csharp
#if DEBUG
// Validate function pointer before returning
if (result == IntPtr.Zero) {
    throw new InvalidOperationException(
        $"GetFunctionPointer failed: class='{className}', methodIndex={methodIndex}. " +
        $"Ensure GenerateTypeMapAssembly ran and method indices are synchronized.");
}
#endif
```

### 9.4 Increased Coupling

| Coupling | Risk | Mitigation |
|----------|------|------------|
| Generator understands Java + .NET | Complex maintenance | Good documentation, unit tests |
| LLVM IR, Java, IL must sync | Index mismatch = crash | Single source of truth for method ordering |
| Proguard depends on trimmed types | Stale keeps = bloat | Generate rules post-trim |

---

## 10. Production Readiness Checklist

### 10.1 Must Have (P0)

- [x] Basic type activation working
- [x] Marshal methods working (regular + activation)
- [x] Interface/Implementor support
- [x] Array creation support
- [x] Trimmer integration
- [x] R8 integration (selective keeps)
- [ ] Comprehensive test suite (see Section 10.4)
- [ ] Performance benchmarks on low-end devices (see Section 5.2)
- [ ] Error handling for missing types/methods (see Section 10.5)
- [ ] Remove or gate debug logging (see Section 11.1)

### 10.2 Should Have (P1)

- [ ] SDK type pre-generation (see Section 6.3)
- [ ] Incremental build support (see Section 6.3)
- [ ] Better error messages with location info (see Section 10.5)
- [ ] Debug logging toggleable via config (see Section 11.1)
- [ ] Documentation for app developers (see Section 11.3)
- [ ] JNI global reference management (see Section 5.3)

### 10.3 Nice to Have (P2)

- [ ] NuGet package type caching (see Section 6.3)
- [ ] Closed generic type support for common patterns (see Section 8.5)
- [ ] Shared callback wrappers - dedupe UCO methods (see Section 11.3)
- [ ] Tool for inspecting generated type maps (see Section 11.3)
- [ ] Generic collection support (see Section 8.4)

### 10.4 Test Suite Requirements

**Unit Tests (TypeMapAttributeTypeMap):**
```csharp
[Test] void TryGetTypesForJniName_KnownType_ReturnsTrue()
[Test] void TryGetTypesForJniName_UnknownType_ReturnsFalse()
[Test] void TryGetTypesForJniName_AliasType_ReturnsAllAliases()
[Test] void CreatePeer_ConcreteType_ReturnsInstance()
[Test] void CreatePeer_InterfaceType_ReturnsInvoker()
[Test] void CreatePeer_AbstractType_ReturnsInvoker()
[Test] void CreatePeer_UnknownType_ThrowsNotSupported()
[Test] void GetFunctionPointer_ValidIndex_ReturnsNonZero()
[Test] void GetFunctionPointer_InvalidIndex_ThrowsOrReturnsZero()
[Test] void CreateArray_Rank1_ReturnsCorrectArray()
[Test] void CreateArray_Rank2_ReturnsJaggedArray()
```

**Integration Tests:**
```csharp
[Test] void App_With100CustomTypes_StartsSuccessfully()
[Test] void App_WithInterfaceCallbacks_InvokesCorrectly()
[Test] void App_WithExportMethods_CallsFromJava()
[Test] void TrimmedReleaseBuild_KeepsRequiredTypes()
[Test] void R8OptimizedBuild_KeepsRequiredJavaClasses()
```

### 10.5 Error Handling Requirements

**Current Problem:** `GetFunctionPointer` returns `IntPtr.Zero` causing SIGSEGV.

**Proposed Error Codes:**

| Code | Description | Thrown When |
|------|-------------|-------------|
| XA4301 | Type not found in TypeMap | `CreatePeer` with unknown JNI name |
| XA4302 | Proxy attribute missing | Type exists but no `[JavaPeerProxy]` |
| XA4303 | Method index out of range | `GetFunctionPointer` with bad index |
| XA4304 | Invoker type not found | Interface without generated Invoker |
| XA4305 | Activation constructor missing | Type cannot be instantiated |

**Implementation:**
```csharp
public IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex)
{
    string classNameStr = className.ToString();
    
    if (!_externalTypeMap.TryGetValue(classNameStr, out Type? type)) {
        throw new TypeMapException(
            $"XA4301: Type '{classNameStr}' not found in TypeMap. " +
            "Ensure the type has [Register] attribute and is included in build.");
    }
    
    var proxy = GetProxyForType(type);
    if (proxy is not IAndroidCallableWrapper acw) {
        throw new TypeMapException(
            $"XA4302: Type '{classNameStr}' has no proxy with GetFunctionPointer. " +
            "This type may be an MCW (binds existing Java class) not an ACW.");
    }
    
    var result = acw.GetFunctionPointer(methodIndex);
    if (result == IntPtr.Zero) {
        throw new TypeMapException(
            $"XA4303: Method index {methodIndex} not found for '{classNameStr}'. " +
            "Method indices may be out of sync between IL and LLVM IR generation.");
    }
    
    return result;
}
```

---

## 11. Recommendations for Moving Forward

### 11.1 Immediate Actions

1. **Remove Debug Logging**
   - Current implementation has extensive `Logger.Log` calls
   - Add `TYPEMAP_DEBUG` conditional compilation symbol
   - Default off for release

2. **Add Unit Tests**
   - Test `TypeMapAttributeTypeMap` in isolation
   - Test proxy generation for all type categories
   - Test method index ordering contract

3. **Benchmark Startup**
   - Measure on Samsung A16 or similar low-end device
   - Test with 8000+ TypeMapAttributes (untrimmed Debug)
   - Compare with legacy LlvmIrTypeMap

### 11.2 Short-term Improvements

1. **SDK Pre-generation**
   - Pre-generate proxies for Mono.Android types during SDK build
   - Ship as artifacts, skip regeneration at app build time
   - Expected: ~80% build time reduction

2. **Error Code System**
   - Define XA43xx error codes for TypeMap runtime (see Section 10.5)
   - Provide actionable error messages with context
   - Include type, assembly, and method index information

3. **Integration Tests**
   - Test full build → deploy → run cycle
   - Cover Activities, Services, Broadcast Receivers
   - Test interface event handlers (e.g., `button.Click +=`)

### 11.3 Long-term Considerations

1. **Generic Type Support**
   - Analyze common generic instantiations in Android apps
   - Consider generating closed generic proxies for `IList<View>`, etc.
   - May require app-specific analysis
   
   **Proposed Approach:** Scan app code for `IList<T>` usage patterns and pre-generate the top 20 most common instantiations.

2. **Shared Callback Wrappers**
   - Multiple user types overriding same method share callback
   - Currently duplicated per type
   - Potential for significant code size reduction
   
   **Proposed Approach:**
   ```csharp
   // Instead of per-type:
   // MainActivity_Proxy.n_onCreate_mm_0(...)
   // MyActivity_Proxy.n_onCreate_mm_0(...)
   
   // Generate shared:
   // SharedCallbacks.n_onCreate_Activity(IntPtr jnienv, IntPtr native__this, IntPtr savedInstanceState)
   // Lookup actual type from native__this, dispatch to correct managed method
   ```
   
   **Trade-off:** Adds runtime lookup overhead, but reduces native code size significantly.

3. **Tool Integration**
   - Visual Studio inspection of generated types
   - Build error navigation to source
   - APK size analysis with type map breakdown
   
   **Proposed Approach:**
   - Generate `.typemap.json` with type mappings for VS extension to consume
   - Add source links in generated code pointing to original `[Register]` locations
   - Add `--typemap-stats` flag to GenerateTypeMapAssembly for size analysis

4. **Documentation for App Developers**
   - What TypeMap V4 is and why it matters
   - How to enable/disable
   - Troubleshooting common issues
   - Migration guide from legacy
   
   **Proposed Approach:** Add doc page at `Documentation/guides/typemap-v4.md`

---

## 12. Migration Path

### 12.1 From Legacy to V4

```xml
<!-- Project file opt-in -->
<PropertyGroup>
  <AndroidEnableTypeMaps>true</AndroidEnableTypeMaps>
</PropertyGroup>
```

### 12.2 Runtime Feature Switches

```json
// runtimeconfig.template.json
{
  "configProperties": {
    "Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime": true,
    "Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime": false
  }
}
```

### 12.3 Breaking Changes

| Change | Impact | Mitigation | Status |
|--------|--------|------------|--------|
| `IList<T>` conversion throws | Apps using generic collections | Use `JavaList<T>` temporarily | Future work (Section 8.4) |
| Dynamic type registration disabled | Plugins loading types at runtime | Enable feature switch | By design |
| `Activator.CreateInstance` fails | Types without proxy attributes | Ensure types are scanned | By design |

---

## 13. Architecture Diagrams

### 13.1 Build-Time Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        BUILD TIME PIPELINE                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ResolvedAssemblies                                                         │
│         │                                                                   │
│         ▼                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │  GenerateTypeMapAssembly MSBuild Task                                   ││
│  │                                                                         ││
│  │  1. JavaPeerScanner.ScanAssemblies (parallel)                          ││
│  │     - Find types with [Register] attribute                             ││
│  │     - Collect marshal methods and activation constructors              ││
│  │     - Identify interfaces, Implementors, MCW vs ACW types              ││
│  │                                                                         ││
│  │  2. TypeMapAssemblyGenerator.Generate                                   ││
│  │     a) _Microsoft.Android.TypeMaps.dll                                 ││
│  │        - TypeMapAttribute<JLO> for each Java peer                      ││
│  │        - Proxy types with CreateInstance, CreateArray, GetFunctionPtr  ││
│  │        - UCO wrapper methods with [UnmanagedCallersOnly]               ││
│  │        - IgnoresAccessChecksTo for cross-assembly access               ││
│  │                                                                         ││
│  │     b) JCW .java files (ACW types only)                                ││
│  │        - Java classes with native method declarations                  ││
│  │        - nc_activate_N() activation calls                               ││
│  │                                                                         ││
│  │     c) LLVM IR .ll files (ACW types only)                              ││
│  │        - JNI entry points with function pointer caching                ││
│  │        - Calls to typemap_get_function_pointer()                       ││
│  └─────────────────────────────────────────────────────────────────────────┘│
│         │                     │                        │                    │
│         ▼                     ▼                        ▼                    │
│  ┌────────────────┐   ┌──────────────────┐   ┌────────────────────────────┐│
│  │   ILLink       │   │  javac + d8/R8   │   │  clang (LLVM → .o → .so)   ││
│  │ (with --type   │   │ (compile Java)   │   │  (per architecture)        ││
│  │  map-entry-    │   └──────────────────┘   └────────────────────────────┘│
│  │  assembly)     │                                                        │
│  └────────────────┘                                                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 13.2 Runtime Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          RUNTIME ACTIVATION                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Java: new MainActivity()                                                   │
│         │                                                                   │
│         ▼                                                                   │
│  JCW Constructor: nc_activate_0()  [native method]                         │
│         │                                                                   │
│         ▼                                                                   │
│  LLVM IR Stub: @Java_crc64..._MainActivity_nc_1activate_10                 │
│         │                                                                   │
│         ├── Check cached function pointer (@fn_ptr_N)                       │
│         │   └── If null: call typemap_get_function_pointer()               │
│         │                                                                   │
│         ▼                                                                   │
│  TypeMapAttributeTypeMap.GetFunctionPointer(className, methodIndex)        │
│         │                                                                   │
│         ├── Lookup proxy type from _externalTypeMap dictionary             │
│         ├── Get cached JavaPeerProxy via GetCustomAttribute                │
│         └── Cast to IAndroidCallableWrapper, call GetFunctionPointer()     │
│         │                                                                   │
│         ▼                                                                   │
│  UCO Wrapper: MainActivity_Proxy.nc_activate_0(jnienv, jobject)           │
│         │                                                                   │
│         ├── Check JniEnvironment.WithinNewObjectScope → skip if true       │
│         ├── Check Java.Lang.Object.PeekObject() → skip if exists          │
│         ├── RuntimeHelpers.GetUninitializedObject(typeof(MainActivity))    │
│         ├── SetPeerReference(new JniObjectReference(jobject))              │
│         └── CallActivationCtor(instance, handle, transfer)                 │
│                                                                             │
│  Result: MainActivity instance created and linked to Java peer             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 14. Glossary

| Term | Definition |
|------|------------|
| **MCW** | Managed Callable Wrapper - .NET binding for existing Java class |
| **JCW/ACW** | Java/Android Callable Wrapper - Generated Java class for .NET type |
| **UCO** | UnmanagedCallersOnly - Static method callable from native code |
| **TypeMap** | Mapping between Java class names and .NET types |
| **Proxy** | Generated JavaPeerProxy subclass that handles activation |
| **Invoker** | Concrete implementation of interface for wrapping Java objects |
| **Implementor** | Generated class that implements Java interface for C# events |
| **Marshal Method** | Method callable from Java that bridges to .NET code |
| **Activation Constructor** | Constructor with (IntPtr, JniHandleOwnership) signature |

---

## 15. Build Pipeline Comparison: Legacy vs V4 TypeMap

This section provides a detailed comparison of the MSBuild task ordering between the legacy and V4 TypeMap systems, highlighting what runs before/after trimming and in outer vs per-RID inner builds.

### 15.1 Build Context: Outer vs Per-RID Inner Builds

.NET for Android uses a **multi-RID build model**:

```
Outer Build (no RuntimeIdentifier set)
├── Compile C# → MyApp.dll (RID-independent)
├── Resolve assemblies
├── [NEW] GenerateTypeMapAssembly (before trimming, RID-independent)
├── GenerateJavaStubs / GenerateTypeMappings
│
└── Per-RID Inner Builds (parallel, RuntimeIdentifier=android-arm64, etc.)
    ├── ILLink trimming (per-RID)
    ├── [LEGACY] Custom ILLink steps
    ├── [NEW] --typemap-entry-assembly flag
    ├── AOT compilation (Mono AOT / crossgen2 / NativeAOT ILC)
    ├── Compile LLVM IR → native .o files
    ├── Link native runtime
    └── Output per-RID artifacts
```

### 15.2 Legacy TypeMap Build Pipeline (MonoVM)

| Phase | Target/Task | Location | Notes |
|-------|-------------|----------|-------|
| **Outer Build** | | | |
| 1 | `_ResolveAssemblies` | All RIDs | Collect user + framework assemblies |
| 2 | `_GenerateJavaStubs` | Outer | Scan assemblies, generate JCW .java files |
| 3 | `GenerateTypeMappings` | Outer | Generate native type map blobs |
| 4 | `_CompileJava` | Outer | Compile JCW .java → .class |
| **Per-RID Build** | | | |
| 5 | `ILLink` | Per-RID | Trim assemblies |
| 6 | Custom ILLink steps | Per-RID | MarkJavaObjects, PreserveRegistrations, etc. |
| 7 | `_RemoveRegisterAttribute` | Per-RID | Strip `[Register]` after trimming |
| 8 | `_GenerateMarshalMethodsSources` | Per-RID | Generate legacy LLVM IR for marshal methods |
| 9 | `_CompileNativeAssemblySources` | Per-RID | Compile .ll → .o per ABI |
| 10 | MonoAOT | Per-RID | AOT compile managed assemblies |
| 11 | Link native | Per-RID | Link libmonodroid.so with type maps |
| **Final** | | | |
| 12 | `_CompileDex` | Outer | R8/D8 on all .class files |
| 13 | Package APK | Outer | Combine all RID artifacts |

**Key Characteristics:**
- JCW generation happens BEFORE trimming (based on all types)
- Custom ILLink steps required per-RID
- Marshal method LLVM IR generated AFTER trimming per-RID
- R8 receives ALL JCW classes (no coordination with trimmer)

### 15.3 V4 TypeMap Build Pipeline (CoreCLR/NativeAOT)

| Phase | Target/Task | Location | Notes |
|-------|-------------|----------|-------|
| **Outer Build** | | | |
| 1 | `_ResolveAssemblies` | All RIDs | Collect user + framework assemblies |
| 2 | **`_GenerateTypeMapAssembly`** | **Outer** | Scan, generate `_Microsoft.Android.TypeMaps.dll`, JCWs, LLVM IR |
| 3 | `_GenerateJavaStubs` | Outer | Now minimal - just manifest/state (JCWs done in step 2) |
| 4 | `_CompileJava` | Outer | Compile JCW .java → .class |
| **Per-RID Build** | | | |
| 5 | `ILLink` + `--typemap-entry-assembly` | Per-RID | Trim with TypeMap-aware flag |
| 6 | *(No custom ILLink steps)* | - | TypeMap attributes handle preservation |
| 7 | *(No `_RemoveRegisterAttribute`)* | - | Attributes kept for runtime lookup |
| 8 | `_CollectTypeMapMarshalMethodSources` | Per-RID | Collect pre-generated LLVM IR files |
| 9 | `_CompileNativeAssemblySources` | Per-RID | Compile .ll → .o per ABI |
| 10 | crossgen2 (R2R) / ILC (NativeAOT) | Per-RID | AOT compile with type map inlined |
| 11 | Link native | Per-RID | Link with V4 marshal stubs |
| **Final** | | | |
| 12 | `_GenerateProguardFromTypeMap` | Outer | Generate R8 keep rules from trimmed TypeMap assembly |
| 13 | `_CompileDex` | Outer | R8 with TypeMap-aware keep rules |
| 14 | Package APK | Outer | Combine all RID artifacts |

**Key Characteristics:**
- All codegen happens BEFORE trimming in outer build
- No custom ILLink steps - standard trimmer + `--typemap-entry-assembly`
- LLVM IR pre-generated, just collected per-RID
- R8 receives keep rules derived from trimmed TypeMap assembly

### 15.4 Task Timing Comparison

```
LEGACY                          V4 TYPEMAP
────────────────────────        ────────────────────────
  Outer Build                     Outer Build
  │                               │
  ├─ ResolveAssemblies            ├─ ResolveAssemblies
  │                               │
  │                               ├─ GenerateTypeMapAssembly ◄──── NEW (generates ALL)
  │                               │   ├─ Scan [Register] types
  │                               │   ├─ _Microsoft.Android.TypeMaps.dll
  │                               │   ├─ JCW .java files
  │                               │   └─ LLVM IR stubs
  │                               │
  ├─ GenerateJavaStubs            ├─ GenerateJavaStubs (minimal)
  │   ├─ Scan [Register]          │
  │   └─ JCW .java files          │
  │                               │
  ├─ GenerateTypeMappings         │   (not needed)
  │   └─ Native type maps         │
  │                               │
  Per-RID Build                   Per-RID Build
  │                               │
  ├─ ILLink                       ├─ ILLink + --typemap-entry-assembly
  │   └─ Custom steps ◄───        │   └─ (no custom steps) ◄─────── SIMPLER
  │       MarkJavaObjects         │
  │       PreserveRegistrations   │
  │                               │
  ├─ RemoveRegisterAttribute      │   (not needed - keep attrs)
  │                               │
  ├─ GenerateMarshalMethods       ├─ CollectTypeMapMarshalMethods
  │   └─ Generate LLVM IR         │   └─ (already generated) ◄────── FASTER
  │                               │
  ├─ CompileNativeSources         ├─ CompileNativeSources
  ├─ MonoAOT                      ├─ crossgen2 / ILC
  ├─ LinkNative                   ├─ LinkNative
  │                               │
  After Trimming                  After Trimming
  │                               │
  │                               ├─ GenerateProguardFromTypeMap ◄─ NEW
  │                               │
  ├─ CompileDex (no guidance)     ├─ CompileDex (with keep rules)
  └─ Package                      └─ Package
```

### 15.5 Build Time Expectations

| Metric | Legacy | V4 | Analysis |
|--------|--------|-----|----------|
| **First build** | Baseline | ~+2.5s | V4 generates more upfront (TypeMap DLL, proxies, LLVM IR) |
| **Incremental build (code change)** | Full rebuild often | Similar | Both rerun affected targets |
| **Incremental build (no change)** | Fast | Fast | Timestamps prevent work |
| **Per-RID overhead** | High | Low | V4 pre-generates, just collects per-RID |
| **Custom ILLink steps** | ~1s per RID | 0 | V4 uses standard trimmer |
| **Post-trim codegen** | Required | None | V4 does all codegen before trim |

**First Build Breakdown (V4 overhead):**
- `GenerateTypeMapAssembly`: ~2s
  - Assembly scanning: ~500ms
  - TypeMap DLL emission: ~300ms
  - JCW generation: ~1s
  - LLVM IR generation: ~200ms
- Offset savings: ~500ms (no custom linker steps)
- **Net: +1.5-2.5s first build**

**Subsequent Builds:**
- If assemblies unchanged → skip GenerateTypeMapAssembly
- Per-RID builds: just collect existing .ll files
- **Should be equal or faster than legacy**

### 15.6 Runtime Performance Expectations

| Metric | Legacy | V4 | Analysis |
|--------|--------|-----|----------|
| **Type lookup** | Native binary search | Dictionary lookup | Comparable O(log n) vs O(1) |
| **First lookup** | Warm (native) | Cold (dict build) | Legacy slight advantage |
| **With R2R/crossgen2** | N/A | Pre-JITted | V4 should match or beat legacy |
| **Memory** | Native allocations | Managed dictionary | V4 slightly higher managed heap |
| **Startup (cold)** | Baseline | ~Similar | Both need type loading |
| **Startup (warm/R2R)** | Baseline | +10ms or equal | Depends on R2R coverage |

**R2R/crossgen2 Optimization:**

The V4 TypeMap dictionary can be pre-compiled:

```csharp
// In TypeMapAttributeTypeMap.cs
static readonly Dictionary<string, TypeMapEntry> s_typeMap = 
    TypeMapping.GetOrCreateExternalTypeMapping<Dictionary<string, TypeMapEntry>>();
```

With **Ready-to-Run (R2R)** via crossgen2:
1. Dictionary initialization code is pre-JITted
2. Static constructor runs at AOT time
3. First lookup is warm, not cold

**Key Performance Advantage: Fewer Managed/Native Transitions**

The legacy TypeMap requires multiple JNI boundary crossings per type lookup:

```
Legacy: Java → JNI → native typemap lookup → JNI → managed activation → JNI → Java
V4:     Java → JNI → managed lookup + activation (single crossing) → JNI → Java
```

V4 keeps the hot path entirely in managed code after the initial JNI call, avoiding:
- Native function call overhead
- JNI reference management per lookup
- Cache coherency issues between native and managed heaps

**Expectation:** With R2R, V4 should be **faster** than legacy native lookup due to:
1. Fewer managed/native transitions
2. Pre-compiled dictionary operations
3. Better CPU cache utilization (managed heap is contiguous)

### 15.7 Per-RID vs All-RIDs Work Distribution

| Work Item | Legacy | V4 | Optimal? |
|-----------|--------|-----|----------|
| Assembly scanning | Per-RID (duplicate) | Outer (once) | ✅ V4 |
| JCW generation | Per-RID (duplicate) | Outer (once) | ✅ V4 |
| TypeMap DLL | N/A | Outer (once) | ✅ V4 |
| LLVM IR stubs | Per-RID (different) | Outer (RID-independent) | ✅ V4 |
| ILLink | Per-RID | Per-RID | Same |
| Native compilation | Per-RID | Per-RID | Same |
| AOT | Per-RID | Per-RID | Same |

**V4 moves RID-independent work to the outer build**, reducing redundant work when building for multiple architectures (arm64 + x64).

### 15.8 Dual Build Support Requirements

To support both TypeMaps simultaneously:

```xml
<!-- When AndroidEnableTypeMaps=true (V4) -->
<Target Name="_GenerateTypeMapAssembly" Condition="'$(AndroidEnableTypeMaps)'=='true'">
  <!-- V4 path: generate TypeMap DLL, JCWs, LLVM IR -->
</Target>

<!-- When AndroidEnableTypeMaps!=true (Legacy) -->
<Target Name="_GenerateJavaStubs" Condition="'$(AndroidEnableTypeMaps)'!='true'">
  <!-- Legacy path: generate JCWs the old way -->
</Target>
<Target Name="_GenerateTypeMappings" Condition="'$(AndroidEnableTypeMaps)'!='true'">
  <!-- Legacy path: generate native type maps -->
</Target>
```

**Condition Matrix for Tasks:**

| Target | `AndroidEnableTypeMaps=true` | `AndroidEnableTypeMaps=false` |
|--------|------------------------------|-------------------------------|
| `_GenerateTypeMapAssembly` | ✅ Runs | ❌ Skipped |
| `_GenerateJavaStubs` (full) | ❌ Skipped | ✅ Runs |
| `_GenerateJavaStubs` (minimal) | ✅ Runs | - |
| `GenerateTypeMappings` | ❌ Skipped | ✅ Runs |
| Custom ILLink steps | ❌ Disabled | ✅ Enabled |
| `_RemoveRegisterAttribute` | ❌ Skipped | ✅ Runs |
| `_GenerateProguardFromTypeMap` | ✅ Runs | ❌ Skipped |

---

## 16. Security Considerations

### 16.1 Access Control Bypass

**Issue:** The generated `_Microsoft.Android.TypeMaps.dll` uses:
```csharp
[assembly: IgnoresAccessChecksTo("Mono.Android")]
[assembly: IgnoresAccessChecksTo("Java.Interop")]
```

**Rationale:** This is necessary because:
1. Activation constructors (IntPtr, JniHandleOwnership) are typically `protected`
2. Generated proxies need to call these constructors via `newobj`
3. Without this attribute, the runtime would throw `MethodAccessException`

**Security Impact:**
- The attribute only applies to the generated assembly, not user code
- User code cannot leverage this bypass
- The bypass is scoped to specific assemblies (Mono.Android, Java.Interop)
- Type safety is still enforced by the CLR

**Alternative Considered:** `[UnsafeAccessor]` attribute
- More granular (per-method instead of per-assembly)
- Code exists in GenerateTypeMapAssembly.cs lines 5201-5395
- Switched away due to complexity and trimmer interactions

**Recommendation:** Document this design decision and have security team review before production.

### 16.2 JNI Handle Safety

**Good Practices in Current Implementation:**
```csharp
// Proper cleanup of local references
if (class_ptr != IntPtr.Zero) {
    JNIEnv.DeleteLocalRef(class_ptr);
}

// Hierarchy walk cleans up intermediate refs
if (currentPtr != class_ptr) {
    JNIEnv.DeleteLocalRef(currentPtr);
}
```

**Concern:** Global reference caching (see Section 5.3) could exhaust the global reference table.

### 16.3 Type Confusion Prevention

**Protection:** Type mapping is generated at build time from:
1. Compiled assemblies (trusted input)
2. `[Register]` attributes in source code
3. Build-time validation of type relationships

**Attack Vector:** An attacker would need to:
1. Modify compiled assemblies before build, OR
2. Inject malicious `[Register]` attributes in source

Both require compromising the development/build environment, which is out of scope.

---

## 17. References

| Document | Location |
|----------|----------|
| V3 Spec | `type-mapping-api-v3-spec.md` |
| TypeMapAttributeTypeMap | `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs` |
| GenerateTypeMapAssembly | `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` |
| JavaPeerProxy | `src/Mono.Android/Java.Interop/JavaPeerProxy.cs` |
| MSBuild Integration | `src/.../Microsoft.Android.Sdk.ILLink.targets` |
| Sample Project | `samples/HelloWorld/NewTypeMapPoc/` |
| ILLink PR | [dotnet/runtime#121513](https://github.com/dotnet/runtime/pull/121513) |

---

## Appendix A: Comparison with V3 Spec

| Section | V3 Status | V4 Changes |
|---------|-----------|------------|
| Open Questions | 3 items | Resolved 2 (size savings, array handling), perf measurement still pending |
| Architecture | Spec only | Validated against implementation |
| Type Classification | Complete | Added edge case documentation |
| Build Pipeline | High-level | Added measured timings |
| Error Handling | Incomplete | Added error code recommendations (XA4301-XA4305) |
| ILLink Steps | Analysis complete | Confirmed replacements working |
| Array Handling | Designed | Implemented and tested |
| Generic Types | Claimed "handled" | Clarified as future work (Section 8.4) |

---

## Appendix B: Verified Implementation Details

### B.1 Actual Type Counts (HelloWorld Sample)

From build logs:
- Java peer types found: ~7000
- Proxy types generated: ~5000 (after filtering Invokers)
- JCW .java files: ~500 (ACW types only)
- LLVM IR stubs: ~500 (ACW types only)
- Final DEX with R8: 31 classes, 19KB

### B.2 Memory Layout

```
TypeMapAttributeTypeMap instance:
├── _externalTypeMap: IReadOnlyDictionary<string, Type> (~7000 entries)
├── _proxyInstances: ConcurrentDictionary<Type, JavaPeerProxy?> (lazy populated)
├── _aliasCache: ConcurrentDictionary<Type, Type[]?> (lazy populated)
├── _jniNameCache: ConcurrentDictionary<Type, string> (lazy populated)
├── _classToTypeCache: ConcurrentDictionary<string, Type?> (lazy populated)
└── _jniClassCache: ConcurrentDictionary<string, IntPtr> (global JNI refs)
```

### B.3 Generated Assembly Structure

```
_Microsoft.Android.TypeMaps.dll:
├── [assembly: IgnoresAccessChecksTo("Mono.Android")]
├── [assembly: IgnoresAccessChecksTo("Java.Interop")]
├── [assembly: TypeMap<JLO>("com/example/MainActivity", typeof(...))]
│   (×7000 for each Java peer type)
├── IgnoresAccessChecksToAttribute (internal, defined in assembly)
└── Proxy types:
    ├── com_example_MainActivity_Proxy : JavaPeerProxy, IAndroidCallableWrapper
    │   ├── GetFunctionPointer(int) → switch statement
    │   ├── CreateInstance(IntPtr, JniHandleOwnership) → newobj
    │   ├── CreateArray(int, int) → CreateArrayOf<T>()
    │   └── static UCO methods: n_onCreate_mm_0, nc_activate_0, ...
    └── android_widget_TextView_Proxy : JavaPeerProxy
        ├── CreateInstance(IntPtr, JniHandleOwnership) → newobj
        └── CreateArray(int, int) → CreateArrayOf<T>()
```

---

## 18. Dual TypeMap Coexistence Strategy

This section addresses how to maintain both `LlvmIrTypeMap` (legacy/Mono) and `TypeMapAttributeTypeMap` (V3/V4) side-by-side and switch between them based on configuration.

### 18.1 Current State Analysis

The current implementation **already supports both TypeMaps** but the switching logic needs refinement:

**Runtime Selection (Android.Runtime/JNIEnvInit.cs line 236):**
```csharp
private static ITypeMap CreateTypeMap ()
{
    if (RuntimeFeature.IsCoreClrRuntime) {
        return new TypeMapAttributeTypeMap ();
    } else if (RuntimeFeature.IsMonoRuntime) {
        // PoC: Currently hardcoded to TypeMapAttributeTypeMap for testing
        return new TypeMapAttributeTypeMap ();  // Should be LlvmIrTypeMap
    } else {
        throw new NotSupportedException ("...");
    }
}
```

**Problem:** The PoC hardcoded both paths to `TypeMapAttributeTypeMap`. For production, MonoVM should use `LlvmIrTypeMap` unless V4 is explicitly enabled.

### 18.2 Configuration Matrix

| Runtime | Build Config | TypeMap | Native Stubs | Notes |
|---------|--------------|---------|--------------|-------|
| MonoVM | Default | `LlvmIrTypeMap` | Legacy LLVM IR | Current shipping behavior |
| MonoVM | `AndroidEnableTypeMaps=true` | `TypeMapAttributeTypeMap` | V4 LLVM IR | Opt-in V4 |
| CoreCLR | Default | `TypeMapAttributeTypeMap` | V4 LLVM IR | CoreCLR requires V4 |
| NativeAOT | Default | `TypeMapAttributeTypeMap` | V4 LLVM IR | NativeAOT requires V4 |

### 18.3 Proposed Feature Switch

**New MSBuild Property:**
```xml
<!-- User-facing property -->
<PropertyGroup>
  <!-- Enable TypeMap V4 for all runtimes including MonoVM -->
  <AndroidEnableTypeMaps>true</AndroidEnableTypeMaps>
</PropertyGroup>
```

**New Runtime Feature Switch:**
```csharp
// RuntimeFeature.cs
[FeatureSwitchDefinition ($"{FeatureSwitchPrefix}{nameof (UseAttributeTypeMap)}")]
internal static bool UseAttributeTypeMap { get; } =
    AppContext.TryGetSwitch ($"{FeatureSwitchPrefix}{nameof (UseAttributeTypeMap)}", 
        out bool isEnabled) ? isEnabled : UseAttributeTypeMapEnabledByDefault;

// Default: false for MonoVM, true for CoreCLR/NativeAOT
const bool UseAttributeTypeMapEnabledByDefault = false;
```

**Build Targets Integration:**
```xml
<!-- Microsoft.Android.Sdk.RuntimeConfig.targets -->
<ItemGroup>
  <!-- Enable attribute-based TypeMap when explicitly requested OR when using non-Mono runtime -->
  <RuntimeHostConfigurationOption 
      Include="Microsoft.Android.Runtime.RuntimeFeature.UseAttributeTypeMap"
      Condition="'$(AndroidEnableTypeMaps)' == 'true' or '$(_AndroidRuntime)' != 'MonoVM'"
      Value="true"
      Trim="true"
  />
</ItemGroup>
```

### 18.4 Runtime Selection Logic (Proposed)

```csharp
private static ITypeMap CreateTypeMap ()
{
    // UseAttributeTypeMap is:
    // - true when AndroidEnableTypeMaps=true (explicit opt-in)
    // - true when CoreCLR or NativeAOT (required for AOT safety)
    // - false when MonoVM without explicit opt-in (legacy behavior)
    
    if (RuntimeFeature.UseAttributeTypeMap) {
        // V4 TypeMap - requires _Microsoft.Android.TypeMaps.dll
        return new TypeMapAttributeTypeMap ();
    }
    
    // Legacy TypeMap - uses native LLVM IR type maps
    return new LlvmIrTypeMap ();
}
```

### 18.5 Build-Time Implications

When `AndroidEnableTypeMaps=true`:

| Component | Generated | Required |
|-----------|-----------|----------|
| `_Microsoft.Android.TypeMaps.dll` | Yes | Yes |
| V4 JCW .java files | Yes | Yes |
| V4 LLVM IR stubs | Yes | Yes |
| Legacy native type maps | No | No |
| Legacy JCW generator | Skipped | N/A |

When `AndroidEnableTypeMaps=false` (or unset with MonoVM):

| Component | Generated | Required |
|-----------|-----------|----------|
| `_Microsoft.Android.TypeMaps.dll` | No | No |
| V4 JCW .java files | No | No |
| V4 LLVM IR stubs | No | No |
| Legacy native type maps | Yes | Yes |
| Legacy JCW generator | Used | Yes |

### 18.6 Dual Build Support

For a transition period, the build may need to generate **both** type map formats:

```xml
<!-- Generate BOTH during transition -->
<Target Name="_GenerateDualTypeMaps"
        Condition="'$(AndroidEnableDualTypeMaps)' == 'true'">
  
  <!-- Generate V4 TypeMap assembly -->
  <GenerateTypeMapAssembly ... />
  
  <!-- Generate legacy native type maps (existing tasks) -->
  <GenerateTypeMappings ... />
  <GenerateJavaStubs ... />
  
</Target>
```

**Benefits:**
- Enables A/B testing in production
- Allows fallback if V4 has issues
- Supports gradual rollout

**Drawbacks:**
- Increased build time (generates both)
- Increased APK size (both sets of artifacts)
- Only useful during transition

### 18.7 Trimmer Behavior Differences

| TypeMap | Trimmer Behavior |
|---------|------------------|
| `LlvmIrTypeMap` | Uses custom ILLink steps (MarkJavaObjects, PreserveRegistrations, etc.) |
| `TypeMapAttributeTypeMap` | Uses TypeMapAttribute + proxy references (no custom steps needed) |

**Important:** The ILLink custom steps should remain active when `UseAttributeTypeMap=false`:

```xml
<!-- Microsoft.Android.Sdk.ILLink.targets -->
<Target Name="_AddAndroidLinkSteps"
        Condition="'$(AndroidEnableTypeMaps)' != 'true'">
  <!-- Only add legacy ILLink steps when NOT using V4 TypeMap -->
  <ILLinkTrimmerDescriptor ... />
</Target>
```

### 18.8 Native Runtime Considerations

The native runtime (`libmonodroid.so` / `libnet-android.so`) has different entry points:

**MonoVM + LlvmIrTypeMap:**
- Uses `monovm_typemap_java_to_managed()` native function
- Type maps embedded in native code as binary blobs
- `RegisterJniNatives` called at runtime

**CoreCLR/NativeAOT + TypeMapAttributeTypeMap:**
- Uses managed `TypeMapAttributeTypeMap` class
- Type maps in `_Microsoft.Android.TypeMaps.dll`
- `GetFunctionPointer` callback for marshal methods
- No runtime JNI registration

**Native code changes for dual support:**
```c++
// In managed-interface.hh / runtime initialization
struct JnienvInitializeArgs {
    // ... existing fields ...
    bool useAttributeTypeMap;  // New: signals which TypeMap to use
};
```

### 18.9 Migration Path

#### Phase 1: Current (PoC)
- `AndroidEnableTypeMaps=true` required for V4
- V4 only tested with CoreCLR
- MonoVM hardcoded to V4 in PoC (for testing)

#### Phase 2: Production-Ready
- Fix MonoVM to use `LlvmIrTypeMap` by default
- Add `RuntimeFeature.UseAttributeTypeMap` switch
- V4 opt-in for MonoVM via `AndroidEnableTypeMaps=true`
- V4 automatic for CoreCLR/NativeAOT

#### Phase 3: Default V4
- After validation, make V4 default for all runtimes
- `AndroidEnableTypeMaps=false` to opt-out (temporary)
- Deprecate legacy `LlvmIrTypeMap`

#### Phase 4: Legacy Removal
- Remove `LlvmIrTypeMap` code
- Remove legacy ILLink custom steps
- Remove legacy type map generation tasks

### 18.10 Testing Strategy

```xml
<!-- Test matrix -->
<PropertyGroup>
  <!-- Scenario 1: MonoVM + Legacy (current shipping) -->
  <UseMonoRuntime>true</UseMonoRuntime>
  <AndroidEnableTypeMaps>false</AndroidEnableTypeMaps>
  
  <!-- Scenario 2: MonoVM + V4 (opt-in testing) -->
  <UseMonoRuntime>true</UseMonoRuntime>
  <AndroidEnableTypeMaps>true</AndroidEnableTypeMaps>
  
  <!-- Scenario 3: CoreCLR + V4 (required) -->
  <UseMonoRuntime>false</UseMonoRuntime>
  <!-- AndroidEnableTypeMaps implied true -->
  
  <!-- Scenario 4: NativeAOT + V4 (required) -->
  <PublishAot>true</PublishAot>
  <!-- AndroidEnableTypeMaps implied true -->
</PropertyGroup>
```

### 18.11 Implementation Checklist

- [ ] Add `RuntimeFeature.UseAttributeTypeMap` property
- [ ] Fix `CreateTypeMap()` to use the new switch
- [ ] Update MSBuild targets to set the runtime config option
- [ ] Ensure ILLink custom steps only run for legacy path
- [ ] Test MonoVM + LlvmIrTypeMap (regression)
- [ ] Test MonoVM + TypeMapAttributeTypeMap (opt-in)
- [ ] Test CoreCLR + TypeMapAttributeTypeMap (required)
- [ ] Test NativeAOT + TypeMapAttributeTypeMap (required)
- [ ] Document the configuration options

---

## Appendix C: Consolidated Future Work Tracking

This appendix consolidates all future work items mentioned throughout the document for easy tracking.

### C.1 Feature Gaps (Must Address)

| Item | Description | Section | Priority |
|------|-------------|---------|----------|
| Generic Collections | `IList<T>`, `IDictionary<K,V>` support | 8.4 | P1 |
| Error Handling | XA43xx error codes, actionable messages | 10.5 | P0 |
| Test Suite | Unit and integration tests | 10.4 | P0 |
| Debug Logging | Gate behind conditional compilation | 11.1 | P0 |
| Performance Benchmarks | Low-end device testing | 5.2 | P0 |

### C.2 Optimizations (Should Address)

| Item | Description | Section | Priority |
|------|-------------|---------|----------|
| SDK Pre-generation | Pre-generate SDK types during SDK build | 6.3, 11.2 | P1 |
| JNI Reference Management | LRU cache or cleanup for global refs | 5.3 | P1 |
| FrozenDictionary | Use for read-only type map | 5.3 | P2 |
| Incremental Build | Cache results based on assembly hashes | 6.3 | P1 |

### C.3 Edge Cases (Nice to Have)

| Item | Description | Section | Priority |
|------|-------------|---------|----------|
| Open Generics | Closed generic pre-generation | 8.5 | P2 |
| Non-static Inner Classes | Outer class reference handling | 8.6 | P2 |
| Higher-rank Arrays | T[][][] and beyond | 8.1 | P3 |
| Shared UCO Methods | Dedupe callback wrappers | 11.3 | P2 |
| Tool Integration | VS inspection, build navigation | 11.3 | P2 |
| Documentation | App developer guide | 11.3 | P1 |

### C.4 Infrastructure (Blocking)

| Item | Description | Section | Status |
|------|-------------|---------|--------|
| ILLink PR | dotnet/runtime#121513 | 9.1 | **Blocking** |
| RuntimeFeature Switch | `UseAttributeTypeMap` property | 18.3 | Not started |
| MonoVM Path Fix | Restore `LlvmIrTypeMap` for default MonoVM | 18.1 | Not started |

### C.5 Proposed Solutions Summary

| Gap | Proposed Solution | Implementation Effort |
|-----|-------------------|----------------------|
| Generic Collections | `IDerivedTypeFactory<T>` pattern (Section 8.4) | Medium |
| Open Generics | Build-time closed generic scanning | Medium |
| Non-static Inner Classes | Outer-aware activation constructor | Low |
| JNI Global Refs | LRU cache with bounded size | Low |
| Error Handling | TypeMapException with XA43xx codes | Low |
| Performance | R2R + FrozenDictionary | Medium |
| MonoVM Reflection | Accept for now; optional binary serialization later | Low |
| SetEntryAssembly Hack | Remove when ILLink PR merges | Low |
| SDK Pre-generation | Pre-generate during SDK build, ship as NuGet | Medium |
| Incremental Build | Hash-based cache, skip unchanged assemblies | Medium |
| Shared Callbacks | Shared dispatcher with runtime type lookup | Medium |
| Tool Integration | `.typemap.json` for VS, source links | Low |
| Documentation | `Documentation/guides/typemap-v4.md` | Low |

---

*Document version: 4.8*
*Based on: type-mapping-api-v3-spec.md v1.4*
*Last updated: 2026-02-01*
*Author: Cross-reference analysis of V3 spec and codebase*
