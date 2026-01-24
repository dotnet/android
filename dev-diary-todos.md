# Type Mapping API PoC - Development Diary TODOs

This file tracks the analysis progress for each commit in the Trimmable Type Mapping PoC implementation.

## Commits to Analyze (oldest → newest)

| # | Status | SHA | Timestamp | Commit Message |
|---|--------|-----|-----------|----------------|
| 1 | [x] | `4743aa548` | 2026-01-21 11:18:11 | WIP: type mapping API experiment work in progress |
| 2 | [x] | `ac3da6ff7` | 2026-01-21 13:20:40 | Fix: rename _JavaTypeManager back to JavaTypeManager |
| 3 | [x] | `a0e7c7120` | 2026-01-21 14:33:38 | PoC: keep Register attrs and log typemap lookups |
| 4 | [x] | `9918a2f5c` | 2026-01-21 14:33:45 | Style: align LogCategories formatting |
| 5 | [x] | `d82fa5564` | 2026-01-21 17:26:09 | WIP: Step 4 - Marshal method generation with UCO wrappers, JCW Java, and LLVM IR |
| 6 | [x] | `e23ecdc74` | 2026-01-21 18:35:08 | WIP: Fix LLVM IR generation and remove duplicate JCW Java generation |
| 7 | [x] | `e6598ae1f` | 2026-01-21 19:04:42 | WIP: Integrate get_function_pointer callback for Type Mapping API |
| 8 | [x] | `dc56a3e98` | 2026-01-21 19:34:44 | WIP: Fix LLVM IR type parsing and MSBuild target ordering for marshal methods |
| 9 | [x] | `713aec20e` | 2026-01-21 19:53:21 | WIP: Fix LLVM IR for PIC and correct ABI format for linking |
| 10 | [x] | `0dd28e444` | 2026-01-21 21:09:48 | Fix: Generate marshal_methods_init.ll to define get_function_pointer global |
| 11 | [x] | `0d0a9e821` | 2026-01-21 21:26:59 | WIP: fix duplicate xamarin_app_init symbol by respecting GenerateEmptyCode in legacy generator |
| 12 | [x] | `33a2deca4` | 2026-01-21 22:06:23 | Fix IL1012 crash in GenerateTypeMapAttributesStep and update MainActivity verification code |
| 13 | [x] | `df4fa731d` | 2026-01-21 23:10:28 | WIP: Call xamarin_app_init to initialize get_function_pointer in app DSO |
| 14 | [x] | `1a0763beb` | 2026-01-22 00:31:50 | WIP: Fully functional TypeMaps PoC with reflection fallback |
| 15 | [x] | `cf36dcc4e` | 2026-01-22 00:56:30 | WIP: Hybrid TypeMaps mode - Disable dynamic registration for Mono.Android on CoreCLR |
| 16 | [x] | `c972521ce` | 2026-01-22 01:16:11 | Fix JNI symbol visibility and signature mangling in LLVM IR generation |
| 17 | [x] | `9337647e0` | 2026-01-22 01:27:33 | Register xamarin_typemap_init in p/invoke override tables |
| 18 | [x] | `67c76f91c` | 2026-01-22 01:34:20 | Cleanup GenerateTypeMapAttributesStep: remove verbose logging and improve comments |
| 19 | [x] | `d64491841` | 2026-01-22 07:23:06 | Review feedback: refactor GetFunctionPointer, rename typemap_init |
| 20 | [x] | `e9b2f00e8` | 2026-01-22 07:25:04 | Review feedback: completely disable RegisterNativeMembers on CoreCLR |
| 21 | [x] | `e5b34ded4` | 2026-01-22 09:16:43 | Merge WriteLine calls into raw string literals in GenerateTypeMapAttributesStep |
| 22 | [x] | `fed933f8e` | 2026-01-22 09:41:26 | Enable GenerateJcwJavaFile with proper base class, wrapper methods, and constructor |
| 23 | [x] | `cee9cbf5e` | 2026-01-22 09:44:31 | Replace reflection with generated CreateInstance factory method |
| 24 | [x] | `dc5a3c111` | 2026-01-22 09:46:43 | Remove typemap_init - set get_function_pointer directly from JNIEnvInit.Initialize |
| 25 | [x] | `16a03ec9f` | 2026-01-22 09:47:19 | Mark all review items as complete |
| 26 | [x] | `a3dc2784b` | 2026-01-22 09:55:17 | Implement proper JI constructor support in CreateInstance codegen |
| 27 | [x] | `80076cbb3` | 2026-01-22 10:00:41 | Throw NotSupportedException when no constructor found in CreateInstance |
| 28 | [x] | `319469ca6` | 2026-01-22 10:04:26 | Remove unused TargetType property from JavaPeerProxy |
| 29 | [x] | `73b8f7c61` | 2026-01-22 10:17:33 | Drop unnecessary DAM attributes |
| 30 | [x] | `f279bf519` | 2026-01-22 10:28:15 | Add path normalization and debug logging for JCW generation |
| 31 | [x] | `57de27aab` | 2026-01-22 10:42:37 | Add JCW copy target to overwrite old-style JCW after GenerateJavaStubs |
| 32 | [x] | `d0ec41a9a` | 2026-01-22 12:45:11 | WIP: Disable old JCW generation, enable new linker-based JCW generation |
| 33 | [x] | `1bf92212b` | 2026-01-22 13:21:02 | Fix JCW generation to skip framework assemblies and add xamarin_app_init |
| 34 | [x] | `534f1b108` | 2026-01-22 13:26:46 | Remove debug file logging from GenerateTypeMapAttributesStep |
| 35 | [x] | `e4de42142` | 2026-01-22 15:24:20 | Fix get_function_pointer symbol visibility and definition |
| 36 | [x] | `ad41ffa71` | 2026-01-22 21:41:58 | WIP: Fix TypeMap attribute to return proxy type for runtime lookup |
| 37 | [x] | `7e68e037e` | 2026-01-23 10:16:57 | WIP: Add LLVM stubs for Implementor types (e.g., IOnClickListenerImplementor) |
| 38 | [x] | `6a79636a5` | 2026-01-23 15:08:24 | Use JavaInterop1 codegen target for CoreCLR runtime |
| 39 | [x] | `f83de12d7` | 2026-01-23 15:12:00 | Fix TypeMap activation for CoreCLR runtime |

## Progress Summary

- **Total commits:** 39
- **Analyzed:** 39 ✅
- **Remaining:** 0

## Notes

- Commits span 3 days: Jan 21-23, 2026
- Day 1 (Jan 21): 14 commits - Initial implementation and LLVM IR fixes
- Day 2 (Jan 22): 21 commits - Major refactoring, review feedback, and JCW generation
- Day 3 (Jan 23): 4 commits - Final fixes for activation and Implementor types

## Key Findings

### Major Evolutions Discovered

1. **`JavaPeerProxy.TargetType` → `CreateInstance()` Factory Method**
   - Commit 1: Initial design with `TargetType` property
   - Commit 23: `CreateInstance()` added
   - Commit 28: `TargetType` removed entirely

2. **Hybrid Mode → Full TypeMaps**
   - Commit 15: Only Mono.Android types used TypeMaps
   - Commit 20: Completely disabled dynamic registration for CoreCLR

3. **`typemap_init` → Direct Pointer Pass**
   - Commits 10-17: Used native `xamarin_typemap_init` function
   - Commit 24: Simplified to direct pointer via `JNIEnvInit`

4. **TypeMap Return Value**
   - Initially: TypeMap returned target type
   - Commit 36: Changed to return proxy type (target as trimTarget)

5. **String Encoding**
   - Initially: UTF-8 strings
   - Commit 39: UTF-16 `ReadOnlySpan<char>` for zero-copy

### Temporary Code Patterns

| Pattern | Added | Removed |
|---------|-------|---------|
| Debug file logging | Commits 3, 12, 30 | Commits 18, 34 |
| `TimingLog` profiling | ~Commit 36 | Commit 39 |
| Reflection fallback | Commit 14 | Superseded |
| `TargetType` property | Commit 1 | Commit 28 |

## Full Report

See `dev-diary-report.md` for the complete chronological analysis.
