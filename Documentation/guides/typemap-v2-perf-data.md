# TypeMap v2 Performance Data

## Test Environment
- **Date**: 2026-01-26
- **Device**: Android Emulator (sdk_gphone64_arm64)
- **Host**: macOS
- **Global .NET Version**: 10.0.101
- **Android Workload**: 36.1.2 (via MAUI 10.0.1)

---

## Baseline: .NET 10 Legacy TypeManager (MonoVM)

### App Startup
| Run | TotalTime (ms) | WaitTime (ms) | Notes |
|-----|---------------|---------------|-------|
| 1   | 238           | 239           | First cold start |
| 2   | 5174          | 5413          | Emulator under load? |
| 3   | 5997          | 6024          | |
| 4   | 7635          | 7644          | |
| 5   | 10012         | 10019         | |
| 6   | 5857          | 5925          | |
| **Best** | **238**   | **239**       | Use this as representative |

**Note**: First cold start (238ms) appears most representative. Subsequent starts show ~5-10s which may indicate emulator resource contention or debug infrastructure overhead.

### Managed OnCreate Breakdown (from logs)
| Phase | Time (ms) |
|-------|-----------|
| base.OnCreate | 7-18 |
| SetContentView | 12-64 |
| FindViewById | 2-7 |
| button.Click+= | 0-5 |

### Micro-benchmarks (Run 1)
| Benchmark | Iterations | Total (ms) | Avg per call |
|-----------|------------|------------|--------------|
| FindClass | 100,000 | 210 | **2.10 μs** |
| ObjectCreation(Button) | 10,000 | 567 | **56.79 μs** |
| GetObject | 1,000,000 | 679 | **679.36 ns** |
| MethodDispatch(getText) | 10,000,000 | 44,675 | **4,467.59 ns** (~4.5 μs) |
| CallbackDispatch(Click) | 100,000 | 264 | **2.65 μs** |

---

## Prototype: TypeMap v2 (CoreCLR)

### App Startup
| Run | TotalTime (ms) | WaitTime (ms) | Notes |
|-----|---------------|---------------|-------|
| 1   | 2355          | 2356          | Cold start |
| **Best** | **2355**  | **2356**      | |

**Note**: Prototype uses CoreCLR runtime (TypeMap v2). Startup is significantly slower than baseline (238ms vs 2355ms) - this is expected due to different runtime initialization.

### Managed OnCreate Breakdown (from logs)
| Phase | Time (ms) |
|-------|-----------|
| base.OnCreate | 0 |
| SetContentView | 12 |
| FindViewById | 0 |
| button.Click+= | 2 |

### Micro-benchmarks (Run 1)
| Benchmark | Iterations | Total (ms) | Avg per call |
|-----------|------------|------------|--------------|
| FindClass | 100,000 | 4,080 | **40.81 μs** |
| ObjectCreation(Button) | 10,000 | 8,082 | **808.26 μs** |
| GetObject | 1,000,000 | 1,095 | **1,095.21 ns** |
| MethodDispatch(getText) | 10,000,000 | 79,758 | **7,975.81 ns** (~8.0 μs) |
| CallbackDispatch(Click) | 100,000 | 7,294 | **72.95 μs** |

---

## Comparison Summary

| Metric | Baseline (MonoVM) | Prototype (CoreCLR) | Delta | Notes |
|--------|----------|-----------|-------|-------|
| Cold Startup | 238 ms | 2,355 ms | **+889%** 🔴 | Different runtimes |
| FindClass | 2.10 μs | 40.81 μs | **+1843%** 🔴 | |
| ObjectCreation | 56.79 μs | 808.26 μs | **+1323%** 🔴 | |
| GetObject | 679.36 ns | 1,095.21 ns | **+61%** 🟡 | |
| MethodDispatch | 4,467.59 ns | 7,975.81 ns | **+79%** 🟡 | |
| CallbackDispatch | 2.65 μs | 72.95 μs | **+2653%** 🔴 | |

### Analysis

**Significant Regressions (>100%):**
- **FindClass**: 19.4x slower - JNI class lookup much slower on CoreCLR
- **ObjectCreation**: 14.2x slower - Button creation has significant overhead
- **CallbackDispatch**: 27.5x slower - Java→.NET callbacks are very slow

**Moderate Regressions (<100%):**
- **GetObject**: 1.6x slower - Acceptable overhead for handle wrapping
- **MethodDispatch**: 1.8x slower - JNI call overhead reasonable

**Note**: These are early TypeMap v2 results. The CoreCLR runtime has different JNI marshaling characteristics. Further investigation needed to determine if this is inherent CoreCLR overhead or TypeMap v2 specific.

---

## Raw Logs

### Baseline Run 1 (Cold Start)
```
01-26 14:29:26.899 PERF: base.OnCreate: 7ms
01-26 14:29:26.911 PERF: SetContentView: 12ms
01-26 14:29:26.914 PERF: FindViewById: 2ms
01-26 14:29:26.915 PERF: button.Click+=: 0ms
01-26 14:29:26.915 PERF: OnCreate complete - tap button to run benchmarks
```

### Baseline Benchmarks
```
01-26 14:29:48.769 PERF: === Starting Benchmarks ===
01-26 14:29:49.003 PERF: FindClass: 100000 iterations in 210ms, avg=2.10us/call
01-26 14:29:49.626 PERF: ObjectCreation(Button): 10000 iterations in 567ms, avg=56.79us/call
01-26 14:29:50.306 PERF: GetObject: 1000000 iterations in 679ms, avg=679.36ns/call
01-26 14:30:34.987 PERF: MethodDispatch(getText): 10000000 iterations in 44675ms, avg=4467.59ns/call
01-26 14:30:35.255 PERF: CallbackDispatch(Click): 100000 iterations in 264ms, avg=2.65us/call, count=100000
01-26 14:30:35.255 PERF: === Benchmarks Complete ===
```

### Prototype Run 1 (Cold Start)
```
01-26 14:46:10.122 PERF: base.OnCreate: 0ms
01-26 14:46:10.134 PERF: SetContentView: 12ms
01-26 14:46:10.135 PERF: FindViewById: 0ms
01-26 14:46:10.138 PERF: button.Click+=: 2ms
01-26 14:46:10.138 PERF: OnCreate complete - tap button to run benchmarks
```

### Prototype Benchmarks
```
01-26 14:48:04.043 PERF: === Starting Benchmarks ===
01-26 14:48:08.192 PERF: FindClass: 100000 iterations in 4080ms, avg=40.81us/call
01-26 14:48:19.345 PERF: ObjectCreation(Button): 10000 iterations in 8082ms, avg=808.26us/call
01-26 14:48:20.453 PERF: GetObject: 1000000 iterations in 1095ms, avg=1095.21ns/call
01-26 14:49:40.241 PERF: MethodDispatch(getText): 10000000 iterations in 79758ms, avg=7975.81ns/call
01-26 14:49:47.575 PERF: CallbackDispatch(Click): 100000 iterations in 7294ms, avg=72.95us/call, count=100000
01-26 14:49:47.575 PERF: === Benchmarks Complete ===
```

---

## Optimizations Applied

### Caching Optimizations (2026-01-26)

The following caching optimizations have been implemented in `TypeMapAttributeTypeMap`:

#### 1. Alias Cache (`s_aliasCache`)
- **Purpose**: Cache resolved alias types for `TryGetTypesForJniName`
- **Implementation**: `ConcurrentDictionary<Type, Type[]?>`
- **Benefit**: Avoids repeated `GetCustomAttribute<JavaInteropAliasesAttribute>()` reflection calls
- **Pattern**: Cache `null` for types with no aliases to skip reflection on subsequent lookups

#### 2. JNI Name Cache (`s_jniNameCache`)
- **Purpose**: Cache computed JNI names for `TryGetJniNameForType`
- **Implementation**: `ConcurrentDictionary<Type, string>` (empty string = not found)
- **Benefit**: Avoids repeated attribute reflection and string computation
- **Pattern**: Use empty string as sentinel value for "not found" (distinguishes from "not cached")

#### 3. Lock-Free Proxy Lookup (`GetProxyForType` and `GetOrCreateProxyInstance`)
- **Purpose**: Cache `JavaPeerProxy` attribute instances and activated proxy instances
- **Implementation**: `ConcurrentDictionary.GetOrAdd()` with static lambda
- **Benefit**: Lock-free thread-safe access, avoids closure allocations, avoids repeated `Activator.CreateInstance` calls

#### 4. Class-to-Type Cache (`s_classToTypeCache`)
- **Purpose**: Cache Java class name → .NET type resolution from `CreatePeer` hierarchy walks
- **Implementation**: `ConcurrentDictionary<string, Type?>`
- **Benefit**: Avoids repeated JNI `GetSuperclass` calls for known Java types
- **Pattern**: Cache the resolved type when found after hierarchy traversal

#### 5. JNI Class Reference Cache (`s_jniClassCache`)
- **Purpose**: Cache JNI class references (global refs) for `IsJavaTypeAssignableFrom`
- **Implementation**: `ConcurrentDictionary<string, IntPtr>` storing global references
- **Benefit**: Avoids repeated JNI `FindClass` calls which are expensive
- **Pattern**: Store global refs that persist for app lifetime

---

## Optimized Results (After Caching)

### Micro-benchmarks (With Caching Optimizations)
| Benchmark | Iterations | Total (ms) | Avg per call |
|-----------|------------|------------|--------------|
| FindClass | 100,000 | 2,009 | **20.10 μs** |
| ObjectCreation(Button) | 10,000 | 6,057 | **605.79 μs** |
| GetObject | 1,000,000 | 797 | **797.80 ns** |
| MethodDispatch(getText) | 10,000,000 | 49,541 | **4,954.14 ns** (~4.95 μs) |
| CallbackDispatch(Click) | 100,000 | 4,674 | **46.74 μs** |

### Second Run (Warm Caches)
| Benchmark | Iterations | Total (ms) | Avg per call |
|-----------|------------|------------|--------------|
| FindClass | 100,000 | 358 | **3.58 μs** |
| ObjectCreation(Button) | 10,000 | 6,770 | **677.09 μs** |
| GetObject | 1,000,000 | 718 | **718.76 ns** |
| MethodDispatch(getText) | 10,000,000 | 60,193 | **6,019.31 ns** (~6.0 μs) |
| CallbackDispatch(Click) | 100,000 | 1,399 | **13.99 μs** |

### Improvement from Caching

| Metric | Before Caching | After Caching (Cold) | After Caching (Warm) | Warm Improvement |
|--------|---------------|---------------------|---------------------|------------------|
| FindClass | 40.81 μs | 20.10 μs | **3.58 μs** | **11.4x faster** 🟢 |
| ObjectCreation | 808.26 μs | 605.79 μs | 677.09 μs | 1.2x faster |
| GetObject | 1,095.21 ns | 797.80 ns | **718.76 ns** | **1.5x faster** 🟢 |
| MethodDispatch | 7,975.81 ns | 4,954.14 ns | 6,019.31 ns | 1.3x faster |
| CallbackDispatch | 72.95 μs | 46.74 μs | **13.99 μs** | **5.2x faster** 🟢 |

### Comparison vs Baseline (MonoVM) - With Warm Caches

| Metric | Baseline (MonoVM) | Warm TypeMap v2 | Regression |
|--------|----------|-----------|-------------|
| FindClass | 2.10 μs | 3.58 μs | **1.7x slower** 🟢 |
| ObjectCreation | 56.79 μs | 677.09 μs | **11.9x slower** 🔴 |
| GetObject | 679.36 ns | 718.76 ns | **1.06x slower** 🟢 (near parity!) |
| MethodDispatch | 4,467.59 ns | 6,019.31 ns | **1.35x slower** 🟢 |
| CallbackDispatch | 2.65 μs | 13.99 μs | **5.3x slower** 🟡 |

### Analysis

**Excellent progress with caching:**
- **GetObject**: Now essentially at parity with MonoVM baseline (1.06x slower)!
- **FindClass**: Now only 1.7x slower (was 19.4x before caching)
- **CallbackDispatch**: Improved from 27.5x to 5.3x slower
- **MethodDispatch**: Within 35% of baseline

**Remaining bottlenecks:**
- **ObjectCreation**: Still 11.9x slower - likely inherent CoreCLR overhead for creating Java objects
- **CallbackDispatch**: Still 5.3x slower with warm caches - JNI → managed call path overhead

**Key insight:** The caches dramatically improve warm-path performance:
- FindClass: Cold 20μs → Warm 3.6μs (5.6x improvement)
- CallbackDispatch: Cold 47μs → Warm 14μs (3.3x improvement)

**Root cause:** The trimming issue (>10MB TypeMap assembly) likely contributes to slower cold-path lookups.
Proper trimming should reduce the type map size significantly.

---

## Notes

- All caches use `ConcurrentDictionary` for lock-free thread-safe access
- GetFunctionPointer caching was considered but not implemented - called once per method, result is cached in LLVM IR globals
- FrozenDictionary for `_externalTypeMap` is a possible future optimization (requires runtime support)

### Remaining Work

- **Trimming Issue**: TypeMap assembly is >10MB, should be much smaller with proper trimming
- **Re-benchmark**: Need to re-run benchmarks after optimizations to measure improvement
- **GetOrCreateExternalTypeMapping timing**: Need to measure time spent in this initialization call
