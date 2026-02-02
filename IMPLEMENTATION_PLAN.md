# TypeMap V4 Implementation Plan

**Version:** 1.1  
**Created:** 2026-02-01  
**Updated:** 2026-02-01  
**Based on:** type-mapping-api-v4-spec.md (v4.8), REVIEW.md

---

## Overview

This document breaks down the TypeMap V4 work into phases with numbered, independent tasks. Each task has clear prerequisites and completion criteria with a strong focus on testing.

**Starting Point:** This plan starts from `origin/main`. The PoC branch (`typed-mapping-api-experiment-wip`) contains foundational refactorings that must be reviewed, tested, and merged first.

**Total Estimated Effort:** 6-8 weeks (excluding external dependencies)

---

## Phase F: Foundation Refactoring (From PoC)

*These tasks represent the architectural changes made in the PoC that must be reviewed and merged before V4-specific work. They establish the abstraction layer that enables both legacy and V4 implementations.*

**PoC Branch:** `typed-mapping-api-experiment-wip`  
**Files Changed:** 13 added, 19 modified, 5 deleted

### Task F.1: Define ITypeMap Interface

**New File:** `src/Mono.Android/Java.Interop/ITypeMap.cs`

**Purpose:** Abstract the type mapping contract so both legacy (`LlvmIrTypeMap`) and V4 (`TypeMapAttributeTypeMap`) can implement it.

**Prerequisites:** None

**Interface Contract:**
```csharp
interface ITypeMap
{
    bool TryGetTypesForJniName(string jniSimpleReference, out IEnumerable<Type>? types);
    bool TryGetJniNameForType(Type type, out string? jniName);
    IEnumerable<string> GetJniNamesForType(Type type);
    bool TryGetInvokerType(Type type, out Type? invokerType);
    JavaPeerProxy? GetProxyForManagedType(Type managedType);
    IJavaPeerable? CreatePeer(IntPtr handle, JniHandleOwnership transfer, Type? targetType);
    Array CreateArray(Type elementType, int length, int rank);
    IntPtr GetFunctionPointer(ReadOnlySpan<char> className, int methodIndex);
}
```

**Completion Criteria:**
- [ ] Interface defined with XML documentation
- [ ] No implementation-specific details leaked into interface
- [ ] Interface is internal (not public API)
- [ ] **TEST:** Verify interface can be implemented by mock for unit testing
- [ ] **REVIEW:** Interface design reviewed by 2+ team members

**Effort:** 2 hours

---

### Task F.2: Create AndroidValueManager

**New File:** `src/Mono.Android/Java.Interop/AndroidValueManager.cs`

**Purpose:** Unified `JniValueManager` that delegates peer creation to `ITypeMap`, replacing the scattered logic in legacy code.

**Prerequisites:** Task F.1

**Key Responsibilities:**
- Instance tracking (`instances` dictionary)
- Peer creation delegation to `ITypeMap.CreatePeer()`
- Handle ownership management
- Array creation via `ITypeMap.CreateArray()`

**Completion Criteria:**
- [ ] Replaces peer creation logic previously in `TypeManager`
- [ ] Works with both `LlvmIrTypeMap` and `TypeMapAttributeTypeMap`
- [ ] No reflection for type instantiation (uses `ITypeMap`)
- [ ] **TEST:** Unit test peer creation with mock `ITypeMap`
- [ ] **TEST:** Unit test handle ownership transfer
- [ ] **TEST:** Integration test with real Android app

**Effort:** 1 day

---

### Task F.3: Create AndroidTypeManager

**New File:** `src/Mono.Android/Java.Interop/AndroidTypeManager.cs`

**Purpose:** `JniTypeManager` implementation that uses `ITypeMap` for type resolution.

**Prerequisites:** Task F.1

**Completion Criteria:**
- [ ] Delegates to `ITypeMap` for all type lookups
- [ ] Handles JNI name ↔ .NET type bidirectional mapping
- [ ] **TEST:** Unit test type resolution with known types
- [ ] **TEST:** Unit test unknown type handling

**Effort:** 4 hours

---

### Task F.4: Extract LlvmIrTypeMap from TypeManager

**New File:** `src/Mono.Android/Java.Interop/LlvmIrTypeMap.cs`  
**Modified:** `src/Mono.Android/Java.Interop/TypeManager.cs`

**Purpose:** Extract existing legacy type mapping logic into `ITypeMap` implementation.

**Prerequisites:** Task F.1

**Implementation:**
- Move `TypeManager.GetJavaToManagedType()` logic to `LlvmIrTypeMap`
- Move native P/Invoke calls for type lookup
- Keep `[RequiresUnreferencedCode]` annotation (legacy uses reflection)

**Completion Criteria:**
- [ ] `LlvmIrTypeMap` implements `ITypeMap`
- [ ] All existing functionality preserved
- [ ] `TypeManager` uses `ITypeMap` internally
- [ ] **TEST:** Existing Mono.Android tests still pass
- [ ] **TEST:** Legacy apps work without changes

**Effort:** 1 day

---

### Task F.5: Add RuntimeFeature Switches

**Modified:** `src/Mono.Android/Microsoft.Android.Runtime/RuntimeFeature.cs`

**Purpose:** Add feature switches to select between legacy and V4 type maps at runtime.

**Prerequisites:** None

**Feature Switches:**
```csharp
RuntimeFeature.IsMonoRuntime          // → LlvmIrTypeMap
RuntimeFeature.IsCoreClrRuntime       // → TypeMapAttributeTypeMap
RuntimeFeature.IsNativeAotRuntime     // → TypeMapAttributeTypeMap
RuntimeFeature.IsDynamicTypeRegistration // Enable/disable dynamic registration
```

**Completion Criteria:**
- [ ] Feature switches correctly detect runtime
- [ ] Switches are ILLink-substitutable for trimming
- [ ] Default behavior matches legacy (Mono = legacy)
- [ ] **TEST:** Verify switch values on each runtime
- [ ] **TEST:** Verify ILLink substitution works

**Effort:** 4 hours

---

### Task F.6: Create JavaPeerProxy Base Class

**New File:** `src/Mono.Android/Java.Interop/JavaPeerProxy.cs`

**Purpose:** Base class for generated proxy types that holds activation constructor and marshal method function pointers.

**Prerequisites:** Task F.1

**Implementation:**
```csharp
abstract class JavaPeerProxy
{
    public abstract IntPtr GetActivationConstructor();
    public abstract IntPtr GetFunctionPointer(int methodIndex);
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}
```

**Completion Criteria:**
- [ ] Abstract base class defined
- [ ] Supports both activation constructor and marshal method scenarios
- [ ] **TEST:** Unit test mock proxy implementation
- [ ] **TEST:** Verify proxy can create instances

**Effort:** 2 hours

---

### Task F.7: Add IAndroidCallableWrapper Interface

**New File:** `src/Mono.Android/Java.Interop/IAndroidCallableWrapper.cs`

**Purpose:** Marker interface for types that have Android Callable Wrappers (JCW).

**Prerequisites:** None

**Completion Criteria:**
- [ ] Interface defined (may be empty marker)
- [ ] Documentation explains purpose
- [ ] **TEST:** Verify interface is applied to generated types

**Effort:** 1 hour

---

### Task F.8: Update JNIEnv and AndroidRuntime Integration

**Modified Files:**
- `src/Mono.Android/Android.Runtime/JNIEnv.cs`
- `src/Mono.Android/Android.Runtime/JNIEnvInit.cs`
- `src/Mono.Android/Android.Runtime/AndroidRuntime.cs`
- `src/Mono.Android/Android.Runtime/AndroidEnvironment.cs`

**Purpose:** Wire up `ITypeMap` selection and initialization during runtime startup.

**Prerequisites:** Tasks F.1-F.5

**Completion Criteria:**
- [ ] Correct `ITypeMap` selected based on runtime
- [ ] Initialization happens early (before first Java call)
- [ ] Error handling for initialization failures
- [ ] **TEST:** Integration test on Mono runtime
- [ ] **TEST:** Integration test on CoreCLR runtime

**Effort:** 1 day

---

### Task F.9: Delete Obsolete Files

**Deleted Files:**
- `src/Mono.Android/Microsoft.Android.Runtime/SimpleValueManager.cs`
- `src/Mono.Android/Microsoft.Android.Runtime/ManagedTypeManager.cs`
- `src/Mono.Android/Microsoft.Android.Runtime/ManagedTypeMapping.cs`
- `src/Mono.Android/Java.Interop/ManagedMarshalMethodsLookupTable.cs`

**Purpose:** Remove code superseded by new abstractions.

**Prerequisites:** Tasks F.2-F.4 (replacements implemented)

**Completion Criteria:**
- [ ] Deleted files not referenced anywhere
- [ ] Build succeeds without deleted files
- [ ] **TEST:** All existing tests pass
- [ ] **TEST:** No runtime errors from missing types

**Effort:** 2 hours

---

### Task F.10: Foundation Integration Tests

**Purpose:** Validate that the foundation refactoring doesn't break existing functionality.

**Prerequisites:** Tasks F.1-F.9

**Test Cases:**
```csharp
[Fact] void LegacyApp_OnMonoRuntime_Works()
[Fact] void LegacyApp_OnCoreClrRuntime_Works()
[Fact] void TypeManager_ResolveActivity_Works()
[Fact] void TypeManager_ResolveInterface_Works()
[Fact] void ValueManager_CreatePeer_Works()
[Fact] void ArrayCreation_AllElementTypes_Works()
```

**Completion Criteria:**
- [ ] All 6 test cases pass
- [ ] No regressions in existing test suites
- [ ] HelloWorld sample runs on device
- [ ] **BENCHMARK:** Startup time not regressed

**Effort:** 2 days

---

### Task F.11: Foundation Code Review and Merge

**Purpose:** Get foundation changes reviewed and merged to main.

**Prerequisites:** Task F.10

**Review Checklist:**
- [ ] API design review (ITypeMap interface)
- [ ] Performance review (no regressions)
- [ ] Trimming review (ILLink substitutions)
- [ ] Security review (no new attack surface)
- [ ] Documentation review

**Completion Criteria:**
- [ ] PR approved by 2+ reviewers
- [ ] CI passes on all platforms
- [ ] Merged to main branch
- [ ] No open issues from review

**Effort:** 2-3 days (review cycle)

---

## Phase 0: Critical Bug Fixes (P0 Blockers)

*Must complete before any other work. These are crash-causing issues in the PoC.*

**Prerequisites:** Phase F complete (foundation merged)

### Task 0.1: Fix IntPtr.Zero Silent Crashes

**File:** `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`

**Problem:** `GetFunctionPointer()` returns `IntPtr.Zero` when method index not found, causing SIGSEGV with no stack trace.

**Prerequisites:** Phase F

**Implementation:**
```csharp
// Line ~454 - BEFORE
return IntPtr.Zero;

// AFTER
throw new TypeMapException($"Method index {methodIndex} not found for type {type.FullName}");
```

**Completion Criteria:**
- [ ] `GetFunctionPointer()` throws `TypeMapException` instead of returning `IntPtr.Zero`
- [ ] `GetActivationConstructor()` throws `TypeMapException` instead of returning `IntPtr.Zero`
- [ ] Exception message includes type name and method index for debugging
- [ ] **TEST:** Unit test `GetFunctionPointer_InvalidIndex_ThrowsTypeMapException`
- [ ] **TEST:** Unit test `GetActivationConstructor_MissingCtor_ThrowsTypeMapException`
- [ ] **TEST:** Integration test verifying stack trace is visible in logcat on crash

**Effort:** 2 hours

---

### Task 0.2: Fix JNI Cache Pollution Bug

**File:** `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`

**Problem:** If `FindClass` fails, `IntPtr.Zero` is cached in `_jniClassCache`, poisoning all future lookups.

**Prerequisites:** None

**Implementation:**
```csharp
// Line ~375 - BEFORE
_jniClassCache[jniClassName] = classHandle;

// AFTER
if (classHandle != IntPtr.Zero)
    _jniClassCache[jniClassName] = classHandle;
```

**Completion Criteria:**
- [ ] `IntPtr.Zero` is never stored in `_jniClassCache`
- [ ] Failed lookups are retried on subsequent calls
- [ ] **TEST:** Unit test `FindClass_Failure_NotCached`
- [ ] **TEST:** Unit test `FindClass_FailureThenSuccess_Works`
- [ ] **TEST:** Verify no memory leak from repeated failed lookups

**Effort:** 1 hour

---

### Task 0.3: Remove/Gate Production Logging

**File:** `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`

**Problem:** 21 `Logger.Log` statements execute in production, causing performance overhead and logcat pollution.

**Prerequisites:** None

**Implementation:**
```csharp
// Create conditional logging method
[Conditional("TYPEMAP_DEBUG")]
static void LogDebug(string message) => Logger.Log(LogLevel.Info, "monodroid-typemap", message);

// Replace all 21 occurrences of:
Logger.Log(LogLevel.Info, "monodroid-typemap", ...);
// With:
LogDebug(...);
```

**Completion Criteria:**
- [ ] All 21 log statements wrapped with `[Conditional("TYPEMAP_DEBUG")]`
- [ ] No logging in Release builds (verified with IL inspection)
- [ ] Logging works when `TYPEMAP_DEBUG` is defined
- [ ] **TEST:** Build Release, verify zero "monodroid-typemap" entries in logcat
- [ ] **TEST:** Build with TYPEMAP_DEBUG, verify logging works
- [ ] **TEST:** Performance benchmark showing no logging overhead in Release

**Effort:** 2 hours

---

### Task 0.4: Create TypeMapException Class

**File:** `src/Mono.Android/Java.Interop/TypeMapException.cs` (new)

**Problem:** No dedicated exception type for TypeMap errors. Need for Task 0.1.

**Prerequisites:** None

**Implementation:**
```csharp
namespace Java.Interop;

/// <summary>
/// Thrown when TypeMap operations fail (missing types, invalid indices, etc.)
/// </summary>
public class TypeMapException : Exception
{
    public TypeMapException(string message) : base(message) { }
    public TypeMapException(string message, Exception innerException) : base(message, innerException) { }
}
```

**Completion Criteria:**
- [ ] `TypeMapException` class exists in `Java.Interop` namespace
- [ ] Exception is public for user code to catch if needed
- [ ] **TEST:** Unit test verifying exception can be thrown and caught
- [ ] **TEST:** Verify exception serializes correctly for remote scenarios

**Effort:** 30 minutes

---

## Phase 1: Test Infrastructure

*Build comprehensive test coverage before further development.*

### Task 1.1: Create TypeMap Unit Test Project

**Location:** `tests/Mono.Android-Tests/Java.Interop/TypeMapTests/`

**Problem:** Zero test coverage for TypeMap V4 runtime.

**Prerequisites:** Task 0.4 (TypeMapException)

**Implementation:**
- Create new test class `TypeMapAttributeTypeMapTests.cs`
- Use xUnit or NUnit (match existing test infrastructure)
- Mock JNI environment for isolated testing

**Completion Criteria:**
- [ ] Test project builds and runs
- [ ] Can instantiate `TypeMapAttributeTypeMap` in test environment
- [ ] **TEST:** Placeholder test that passes (scaffolding)
- [ ] CI pipeline runs TypeMap tests

**Effort:** 4 hours

---

### Task 1.2: Type Lookup Tests

**File:** `tests/Mono.Android-Tests/Java.Interop/TypeMapTests/TypeLookupTests.cs`

**Prerequisites:** Task 1.1

**Test Cases:**
```csharp
[Fact] void TryGetTypesForJniName_KnownType_ReturnsTrue()
[Fact] void TryGetTypesForJniName_UnknownType_ReturnsFalse()
[Fact] void TryGetTypesForJniName_NullName_ReturnsFalse()
[Fact] void TryGetTypesForJniName_EmptyName_ReturnsFalse()
[Fact] void TryGetTypesForJniName_ArrayType_ReturnsTrue()
[Fact] void TryGetTypesForJniName_PrimitiveArray_ReturnsTrue()
[Fact] void TryGetTypesForJniName_CaseSensitive_Correct()
[Fact] void TryGetTypesForJniName_Concurrent_ThreadSafe()
```

**Completion Criteria:**
- [ ] All 8 test cases implemented and passing
- [ ] Tests run in under 1 second total
- [ ] No flaky tests

**Effort:** 4 hours

---

### Task 1.3: JNI Name Resolution Tests

**File:** `tests/Mono.Android-Tests/Java.Interop/TypeMapTests/JniNameTests.cs`

**Prerequisites:** Task 1.1

**Test Cases:**
```csharp
[Fact] void GetJniNameForType_Activity_ReturnsCorrect()
[Fact] void GetJniNameForType_NestedClass_ReturnsCorrect()
[Fact] void GetJniNameForType_GenericType_ThrowsNotSupported()
[Fact] void GetJniNameForType_Interface_ReturnsCorrect()
[Fact] void GetJniNameForType_NullType_Throws()
[Fact] void GetJniNameForType_NonJavaType_Throws()
```

**Completion Criteria:**
- [ ] All 6 test cases implemented and passing
- [ ] Edge cases for nested classes ($) handled
- [ ] Generic types throw clear exception

**Effort:** 3 hours

---

### Task 1.4: Function Pointer Tests

**File:** `tests/Mono.Android-Tests/Java.Interop/TypeMapTests/FunctionPointerTests.cs`

**Prerequisites:** Task 0.1 (IntPtr.Zero fix), Task 1.1

**Test Cases:**
```csharp
[Fact] void GetFunctionPointer_ValidIndex_ReturnsNonZero()
[Fact] void GetFunctionPointer_InvalidIndex_ThrowsTypeMapException()
[Fact] void GetFunctionPointer_NegativeIndex_ThrowsTypeMapException()
[Fact] void GetFunctionPointer_OutOfRange_ThrowsTypeMapException()
[Fact] void GetActivationConstructor_ValidType_ReturnsNonZero()
[Fact] void GetActivationConstructor_AbstractType_ThrowsTypeMapException()
[Fact] void GetActivationConstructor_InterfaceType_ThrowsTypeMapException()
```

**Completion Criteria:**
- [ ] All 7 test cases implemented and passing
- [ ] Exception messages are descriptive
- [ ] Tests verify actual function pointers work (not just non-zero)

**Effort:** 4 hours

---

### Task 1.5: Integration Tests with Device

**File:** `tests/MSBuildDeviceIntegration/Tests/TypeMapIntegrationTests.cs`

**Prerequisites:** Tasks 1.2, 1.3, 1.4

**Test Cases:**
```csharp
[Fact] void App_WithTypeMapV4_StartsSuccessfully()
[Fact] void App_WithTypeMapV4_ActivityLaunches()
[Fact] void App_WithTypeMapV4_JavaCallbacksWork()
[Fact] void App_WithTypeMapV4_ExportMethodsWork()
[Fact] void App_TypeMapV4vsLegacy_SameBehavior()
[Fact] void App_TypeMapV4_TrimmedRelease_Works()
[Fact] void App_TypeMapV4_NativeAOT_Works()
```

**Completion Criteria:**
- [ ] All 7 test cases implemented and passing on emulator
- [ ] Tests run on both arm64 and x64 emulators
- [ ] Tests cover Debug and Release configurations
- [ ] NativeAOT test passes (or is marked skip with tracking issue)

**Effort:** 2 days

---

### Task 1.6: Build Task Unit Tests

**File:** `tests/Xamarin.Android.Build.Tests/Tasks/GenerateTypeMapAssemblyTests.cs`

**Prerequisites:** Task 1.1

**Test Cases:**
```csharp
[Fact] void Execute_ValidInput_GeneratesTypeMapsDll()
[Fact] void Execute_NoJavaTypes_GeneratesEmptyMap()
[Fact] void Execute_AbstractClass_SkipsActivationCtor()
[Fact] void Execute_Interface_SkipsActivationCtor()
[Fact] void Execute_ExportMethod_GeneratesLlvmStub()
[Fact] void Execute_NestedClass_GeneratesCorrectJniName()
[Fact] void Execute_Incremental_DoesNotRegenerate()
[Fact] void Execute_DuplicateJniName_WarnsXA4302()
```

**Completion Criteria:**
- [ ] All 8 test cases implemented and passing
- [ ] Tests verify generated IL is valid
- [ ] Tests verify LLVM IR output is syntactically correct
- [ ] Incremental build test verifies no unnecessary regeneration

**Effort:** 2 days

---

### Task 1.7: Performance Benchmark Suite

**File:** `tests/Xamarin.Android.Build.Tests/Performance/TypeMapBenchmarks.cs`

**Prerequisites:** Tasks 1.5, 1.6

**Benchmarks:**
```csharp
[Benchmark] void TypeLookup_SingleType_Latency()
[Benchmark] void TypeLookup_1000Types_Throughput()
[Benchmark] void BuildTime_SmallApp_TypeMapGeneration()
[Benchmark] void BuildTime_LargeApp_TypeMapGeneration()
[Benchmark] void Startup_ColdStart_TypeMapInitialization()
[Benchmark] void Memory_TypeMapCacheSize()
[Benchmark] void Comparison_V4vsLegacy_TypeLookup()
[Benchmark] void Comparison_V4vsLegacy_BuildTime()
```

**Completion Criteria:**
- [ ] All 8 benchmarks implemented
- [ ] Baseline measurements recorded for legacy
- [ ] V4 measurements within acceptable thresholds:
  - Type lookup: < 2x legacy latency
  - Build time: < 1.5x legacy for same app
  - Memory: < 2x legacy footprint
- [ ] Benchmarks run in CI with regression detection

**Effort:** 2 days

---

## Phase 2: Code Quality & Refactoring

*Improve maintainability before adding features.*

### Task 2.1: Split GenerateTypeMapAssembly.cs

**File:** `src/Xamarin.Android.Build.Tasks/Tasks/GenerateTypeMapAssembly.cs` (6015 lines)

**Problem:** Monolithic file is hard to maintain and test.

**Prerequisites:** Task 1.6 (build task tests provide safety net)

**Target Structure:**
```
Tasks/
├── GenerateTypeMapAssembly.cs          (orchestrator, ~200 lines)
├── TypeMap/
│   ├── JavaPeerScanner.cs              (~500 lines)
│   ├── TypeMapILGenerator.cs           (~800 lines)
│   ├── TypeMapLlvmIrGenerator.cs       (~600 lines)
│   ├── JavaCallableWrapperGenerator.cs (~400 lines)
│   ├── ExportMethodCollector.cs        (~300 lines)
│   └── TypeMapAssemblyWriter.cs        (~400 lines)
```

**Completion Criteria:**
- [ ] Main task file under 300 lines
- [ ] Each extracted class has single responsibility
- [ ] All existing tests still pass
- [ ] No functional changes (pure refactor)
- [ ] **TEST:** All Task 1.6 tests pass after refactor
- [ ] **TEST:** Build sample app, verify identical output

**Effort:** 3 days

---

### Task 2.2: Implement Error Codes XA4301-XA4305

**Files:** Multiple

**Problem:** Spec defines error codes but they're not implemented.

**Prerequisites:** Task 2.1 (for clean code organization)

**Error Codes:**
| Code | Meaning | Implementation |
|------|---------|----------------|
| XA4301 | `Activator.CreateInstance` usage detected | Roslyn analyzer |
| XA4302 | Duplicate JNI name in type map | Build task warning |
| XA4303 | Generic collection in JNI boundary | Build task error |
| XA4304 | Dynamic Assembly.Load with Java types | Roslyn analyzer |
| XA4305 | Missing `[JavaPeerProxy]` attribute | Build task error |

**Completion Criteria:**
- [ ] All 5 error codes implemented
- [ ] Error messages include fix suggestions
- [ ] Errors appear in MSBuild output with correct format
- [ ] **TEST:** Unit test for each error code trigger condition
- [ ] **TEST:** Verify error codes appear in VS Error List
- [ ] Documentation added to error code reference

**Effort:** 3 days

---

### Task 2.3: Implement Global Reference Management

**File:** `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`

**Problem:** `_jniClassCache` accumulates JNI global refs that are never released. Android has ~51,200 limit.

**Prerequisites:** Task 1.4 (tests for safety)

**Implementation Options:**
1. **LRU Cache:** Limit cache to 1000 entries, evict oldest
2. **Weak References:** Use `WeakReference<IntPtr>` for cache values
3. **Explicit Cleanup:** Add `ClearCache()` method for memory pressure

**Completion Criteria:**
- [ ] Cache has bounded size (configurable, default 1000)
- [ ] Old entries are released via `JNIEnv.DeleteGlobalRef`
- [ ] No global ref leaks after sustained operation
- [ ] **TEST:** `GlobalRefCache_AtLimit_EvictsOldest`
- [ ] **TEST:** `GlobalRefCache_AfterEviction_StillWorks`
- [ ] **TEST:** Memory test showing stable memory after 10000 lookups

**Effort:** 1 day

---

## Phase 3: SDK Pre-generation (Critical for Build Performance)

*This phase is required for production - not optional.*

### Task 3.1: Design Dual TypeMap Architecture

**Deliverable:** Design document + API contracts

**Problem:** PoC regenerates Mono.Android types (80%) on every app build.

**Prerequisites:** None

**Design Decisions Needed:**
1. How are SDK types packaged? (separate assembly? embedded resource?)
2. How does runtime combine SDK + app maps?
3. How is SDK typemap updated when SDK updates?
4. What's the versioning strategy?

**Completion Criteria:**
- [ ] Design document approved by team
- [ ] API contracts for `ISdkTypeMap` and `IAppTypeMap` defined
- [ ] Build pipeline integration points identified
- [ ] R2R/crossgen2 integration approach documented
- [ ] **REVIEW:** Design reviewed by 2+ team members

**Effort:** 2 days

---

### Task 3.2: SDK TypeMap Generation Task

**File:** `src/Xamarin.Android.Build.Tasks/Tasks/GenerateSdkTypeMap.cs` (new)

**Problem:** Need separate task for SDK-time generation.

**Prerequisites:** Task 3.1 (design), Task 2.1 (refactored code)

**Implementation:**
- Runs during SDK build, not app build
- Scans `Mono.Android.dll` and satellite assemblies
- Generates pre-compiled typemap assembly
- Outputs to SDK package location

**Completion Criteria:**
- [ ] Task generates SDK typemap during workload build
- [ ] Output assembly is valid and loadable
- [ ] Task integrates with existing SDK build pipeline
- [ ] **TEST:** SDK typemap contains expected types (Activity, View, etc.)
- [ ] **TEST:** SDK typemap loads successfully at runtime
- [ ] **TEST:** Measure generation time (target: < 30 seconds)

**Effort:** 1 week

---

### Task 3.3: App TypeMap Delta Generation

**File:** Modify `GenerateTypeMapAssembly.cs`

**Problem:** App builds should only generate app-specific types.

**Prerequisites:** Task 3.2

**Implementation:**
- Load SDK typemap at build time
- Exclude types already in SDK map
- Generate only app + 3rd-party types

**Completion Criteria:**
- [ ] App build skips types present in SDK typemap
- [ ] Build time reduced by ~80% for typical app
- [ ] **TEST:** Build time benchmark: before vs after
- [ ] **TEST:** App still works with combined maps
- [ ] **TEST:** 3rd-party library types included in app map

**Effort:** 3 days

---

### Task 3.4: Runtime TypeMap Combiner

**File:** `src/Mono.Android/Java.Interop/TypeMapAttributeTypeMap.cs`

**Problem:** Runtime needs to query both SDK and app typemaps.

**Prerequisites:** Task 3.3

**Implementation:**
```csharp
public bool TryGetTypesForJniName(string jniName, out IEnumerable<Type>? types)
{
    // Try SDK first (most types, pre-compiled, fast)
    if (_sdkTypeMap.TryGetValue(jniName, out var type))
    {
        types = new[] { type };
        return true;
    }
    
    // Fall back to app types
    if (_appTypeMap.TryGetValue(jniName, out type))
    {
        types = new[] { type };
        return true;
    }
    
    types = null;
    return false;
}
```

**Completion Criteria:**
- [ ] SDK types resolved first (fast path)
- [ ] App types resolved as fallback
- [ ] No performance regression for SDK type lookup
- [ ] **TEST:** SDK type lookup time < app type lookup time
- [ ] **TEST:** App type not in SDK still resolves
- [ ] **TEST:** Conflicting names resolved correctly (app wins? SDK wins?)

**Effort:** 2 days

---

### Task 3.5: R2R Pre-compilation for SDK TypeMap

**Problem:** SDK typemap should benefit from ReadyToRun.

**Prerequisites:** Task 3.2

**Implementation:**
- Add crossgen2 step to SDK build
- Pre-compile SDK typemap assembly
- Measure cold start improvement

**Completion Criteria:**
- [ ] SDK typemap assembly is R2R compiled
- [ ] Cold start time improved measurably
- [ ] **TEST:** Benchmark cold start with R2R vs without
- [ ] **TEST:** R2R assembly works on all target architectures

**Effort:** 2 days

---

### Task 3.6: Make LLVM IR Architecture-Independent

**Status:** ✅ IMPLEMENTED

**Problem:** The current PoC hardcodes `target triple = "aarch64-unknown-linux-android21"` in generated `.ll` files. This means we generate the same IR multiple times or it only works for arm64.

**Goal:** Generate architecture-neutral LLVM IR once, compile with `llc -mtriple` for each target architecture.

**Prerequisites:** Task F.5 (PoC code merged)

**Why This Works:**
- Generated IR uses only architecture-neutral types: `ptr`, `i8`, `i16`, `i32`, `i64`
- IR contains simple function calls with pointer forwarding - no complex struct layouts
- Calling convention differences are handled by `llc` based on `-mtriple`
- The marshal method stubs don't depend on architecture-specific data layouts

**Implementation (Completed):**
1. ✅ Updated `GenerateLlvmIrInitFile` to omit `target triple` and `target datalayout`
2. ✅ Updated `GenerateLlvmIrFile` to omit `target triple` and `target datalayout`
3. ✅ Updated `CompileNativeAssembly` to read `%(abi)` metadata and pass `-mtriple`:
   ```csharp
   static readonly Dictionary<string, string> AbiToTriple = new Dictionary<string, string> {
       { "arm64-v8a", "aarch64-unknown-linux-android21" },
       { "armeabi-v7a", "armv7-unknown-linux-android21" },
       { "x86_64", "x86_64-unknown-linux-android21" },
       { "x86", "i686-unknown-linux-android21" },
   };
   // ...
   if (!string.IsNullOrEmpty (abi) && AbiToTriple.TryGetValue (abi, out string? triple)) {
       tripleOption = $"-mtriple={triple} ";
   }
   ```

**Completion Criteria:**
- [x] `.ll` files no longer contain hardcoded `target triple`
- [x] Same `.ll` files used for all target architectures
- [x] `CompileNativeAssembly` reads `%(abi)` metadata from source items
- [x] `CompileNativeAssembly` passes `-mtriple` to `llc`
- [ ] **TEST:** Build succeeds for all 4 architectures (arm64, x86_64, arm, x86)
- [ ] **TEST:** Generated native code works correctly on arm64 device
- [ ] **TEST:** Generated native code works correctly on x86_64 emulator
- [ ] **TEST:** Multi-RID build only generates IR once

**Effort:** 0.5 days

**Impact:** 
- Eliminates redundant IR generation for multi-RID builds
- Reduces build time for apps targeting multiple architectures
- Simplifies caching (one IR artifact per type, not per type × RID)
- **Fixes x86_64 support which is currently broken**

---

## Phase 4: External Dependencies

*Track and integrate external work.*

### Task 4.1: Track ILLink PR (dotnet/runtime#121513)

**Problem:** V4 requires `--typemap-entry-assembly` flag in ILLink.

**Prerequisites:** None

**Actions:**
1. Monitor PR status weekly
2. Test with preview builds when available
3. Remove workaround code when merged

**Completion Criteria:**
- [ ] PR merged to dotnet/runtime
- [ ] Feature available in .NET 11 preview
- [ ] `WorkaroundForILLink()` method removed
- [ ] **TEST:** Build works without workaround
- [ ] **TEST:** ILLink correctly preserves typemap entries

**Effort:** External (tracking only)

---

### Task 4.2: Security Review for IgnoresAccessChecksTo

**Problem:** Generated assembly uses `[assembly: IgnoresAccessChecksTo("Mono.Android")]`.

**Prerequisites:** None

**Actions:**
1. Document security implications
2. Submit to security team for review
3. Implement mitigations if required

**Questions for Security Team:**
- Is this pattern acceptable for shipped code?
- What attack vectors does this enable?
- Do we need additional sandboxing?

**Completion Criteria:**
- [ ] Security review completed
- [ ] Findings documented
- [ ] Mitigations implemented if required
- [ ] Sign-off from security team

**Effort:** External (review), 2-3 days (mitigations if needed)

---

## Phase 5: Advanced Features (P2)

*Nice-to-have improvements.*

### Task 5.1: Generic Collection Support

**Problem:** `IList<T>` and similar throw `NotSupportedException`.

**Prerequisites:** Phase 1 complete, Task 2.2 (error codes)

**Design Challenge:** 
- Generics have infinite instantiations
- Cannot pre-generate all possible `IList<T>`
- Need fallback mechanism

**Possible Solutions:**
1. Support common cases (`IList<string>`, `IList<int>`) statically
2. Runtime generic instantiation (breaks AOT goals)
3. Require concrete types in API boundaries

**Completion Criteria:**
- [ ] Design decision documented
- [ ] Common generic types supported if feasible
- [ ] Clear error message for unsupported generics
- [ ] **TEST:** `IList<string>` works (if supported)
- [ ] **TEST:** Unsupported generic gives actionable error

**Effort:** 1 week

---

### Task 5.2: Incremental Build Support

**Problem:** TypeMap regenerated on every build.

**Prerequisites:** Task 2.1 (refactored code)

**Implementation:**
- Track input file timestamps
- Cache generated output
- Only regenerate on changes

**Completion Criteria:**
- [ ] No-op build skips typemap generation
- [ ] Changed file triggers minimal regeneration
- [ ] **TEST:** Build twice, second build skips generation
- [ ] **TEST:** Change one file, only affected types regenerated
- [ ] **TEST:** Build time benchmark: incremental vs full

**Effort:** 1 week

---

## Dependency Graph

```
Phase 0 (Blockers)
├── Task 0.1: IntPtr.Zero fix
├── Task 0.2: Cache bug fix  
├── Task 0.3: Logging fix
└── Task 0.4: TypeMapException ─────────────────────────────────────┐
                                                                    │
Phase 1 (Testing)                                                   │
├── Task 1.1: Test project ◄────────────────────────────────────────┘
│   ├── Task 1.2: Type lookup tests
│   ├── Task 1.3: JNI name tests
│   └── Task 1.4: Function pointer tests ◄── Task 0.1
│       └── Task 1.5: Device integration tests
│           └── Task 1.7: Benchmarks
└── Task 1.6: Build task tests
        │
Phase 2 (Quality)
├── Task 2.1: Split monolithic task ◄── Task 1.6
│   └── Task 2.2: Error codes
└── Task 2.3: Global ref management ◄── Task 1.4
        │
Phase 3 (SDK Pre-gen)
├── Task 3.1: Design ─────────────────┐
│                                     │
├── Task 3.2: SDK task ◄──────────────┤◄── Task 2.1
│   ├── Task 3.3: Delta gen           │
│   │   └── Task 3.4: Runtime combiner│
│   └── Task 3.5: R2R                 │
│                                     │
Phase 4 (External)                    │
├── Task 4.1: ILLink PR (parallel)    │
└── Task 4.2: Security review         │
                                      │
Phase 5 (P2)                          │
├── Task 5.1: Generics ◄──────────────┘
└── Task 5.2: Incremental builds ◄── Task 2.1
```

---

## Summary Table

| Phase | Tasks | Total Effort | Blocking? |
|-------|-------|--------------|-----------|
| **0: Bug Fixes** | 0.1-0.4 | 1 day | Yes |
| **1: Testing** | 1.1-1.7 | 1 week | Yes |
| **2: Quality** | 2.1-2.3 | 1 week | For maintainability |
| **3: SDK Pre-gen** | 3.1-3.5 | 2 weeks | For build perf |
| **4: External** | 4.1-4.2 | External | For GA |
| **5: P2 Features** | 5.1-5.2 | 2 weeks | No |

**Critical Path:** Phase 0 → Phase 1 → Phase 3 → Phase 4 → GA

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Test coverage | >80% | Code coverage tool |
| Build time (app) | <1.5x legacy | Benchmark Task 1.7 |
| Runtime lookup | <2x legacy | Benchmark Task 1.7 |
| Memory usage | <2x legacy | Benchmark Task 1.7 |
| Crash rate | 0 | Device integration tests |
| Security issues | 0 unmitigated | Security review |

---

*Implementation plan created: 2026-02-01*
*Based on: REVIEW.md, type-mapping-api-v4-spec.md v4.8*
