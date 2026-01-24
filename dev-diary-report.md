# Type Mapping API PoC - Development Diary

> A chronological analysis of the Trimmable Type Mapping PoC implementation, tracking how ideas evolved, what was tried and abandoned, and the key learnings along the way.

---

## Overview

This document analyzes 39 commits spanning January 21-23, 2026, implementing the Type Mapping API for .NET Android. The goal is to understand:

1. **Evolution of ideas** - How the architecture changed over time
2. **Dead ends** - Approaches that were tried and abandoned
3. **Key insights** - Moments where understanding shifted
4. **Technical challenges** - Problems encountered and how they were solved

---

## Key Evolutions Discovered

Before diving into the commit-by-commit analysis, here are the most significant patterns of change:

### 1. `JavaPeerProxy.TargetType` → `CreateInstance()` Factory Method

**Initial Design (Commit 1):**
```csharp
abstract class JavaPeerProxy : Attribute
{
    public abstract Type TargetType { get; }
}
```
The proxy returned a `Type`, and `Activator.CreateInstance()` was still used.

**Final Design (Commit 23 → 28):**
```csharp
abstract class JavaPeerProxy : Attribute
{
    public abstract IntPtr GetFunctionPointer(int methodIndex);
    public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
}
```
The `TargetType` property was **removed entirely** in commit 28 after `CreateInstance()` was added in commit 23. The factory method enables true AOT-safe instantiation without reflection.

### 2. Hybrid Mode → Full TypeMaps

**Commit 15 (Hybrid):** Only `Mono.Android` types used TypeMaps, user code still used dynamic registration:
```csharp
if (RuntimeFeature.IsCoreClrRuntime &&
    type.Assembly.GetName().Name == "Mono.Android" &&
    type.Namespace != "Java.Interop")
    return;  // Skip only Mono.Android types
```

**Commit 20 (Full):** Dynamic registration completely disabled for CoreCLR:
```csharp
if (RuntimeFeature.IsCoreClrRuntime) {
    // All native methods resolved via TypeMaps
    return;
}
```

### 3. Reflection Fallback → Generated UCO Wrappers

**Commit 14:** Added `ResolveUserTypeMethod()` using reflection as fallback:
```csharp
// Reflection-based resolution when proxy not available
var callbackMethod = type.GetMethod(callbackName, ...);
return callbackMethod.MethodHandle.GetFunctionPointer();
```

**Later Commits:** This fallback became unnecessary as all types got generated proxies with UCO wrappers.

### 4. `typemap_init` Native Function → Direct Pointer Pass

**Commits 10-17:** Used a `xamarin_typemap_init` native function called from `xamarin_app_init`.

**Commit 24:** Simplified to pass `get_function_pointer` directly via `JNIEnvInit.Initialize()` out parameter, removing redundant initialization code.

### 5. Debug Logging → Cleanup

**Commits 3, 12, 18, 34:** Debug logging and file writes (`/tmp/linker-jcw.txt`, `/tmp/typemap-debug.log`) were added throughout development, then removed in cleanup commits (18, 34, 39).

**Commit 39 final cleanup:** Removed the entire `TimingLog` static class (~76 lines of profiling code).

---

## Day 1: January 21, 2026 - Initial Implementation

### Commit 1: `4743aa548` - 11:18:11
**WIP: type mapping API experiment work in progress**

This massive initial commit (~5,000 lines changed) establishes the foundational architecture.

#### Files Added
- `ITypeMap.cs` - Core abstraction interface
- `TypeMapAttributeTypeMap.cs` - CoreCLR/NativeAOT implementation
- `LlvmIrTypeMap.cs` - Mono fallback implementation
- `JavaPeerProxy.cs` - Proxy base class (with `TargetType` property)
- `AndroidTypeManager.cs`, `AndroidValueManager.cs` - New managers
- `DynamicNativeMembersRegistration.cs` - Isolated reflection code
- `GenerateTypeMapAttributesStep.cs` - New ILLink step
- `type-mapping-api-codegen-spec.md` - Design specification

#### Files Deleted
- `TypeMappingStep.cs` - Old linker step
- `ManagedMarshalMethodsLookupTable.cs`
- `ManagedMarshalMethodsLookupGenerator.cs`
- `ManagedTypeManager.cs`, `ManagedTypeMapping.cs`

#### Key Design Insight
The proxy applies itself as an attribute (`[A_Proxy]` on `class A_Proxy`), enabling AOT-safe `GetCustomAttribute<JavaPeerProxy>()` to retrieve the proxy instance.

---

### Commit 2: `ac3da6ff7` - 13:20:40
**Fix: rename _JavaTypeManager back to JavaTypeManager**

Quick cosmetic fix - debugging marker accidentally committed.

---

### Commit 3: `a0e7c7120` - 14:33:38
**PoC: keep Register attrs and log typemap lookups**

> 🔄 **TEMPORARY:** Debug logging added here, removed in commit 18.

---

### Commit 4: `9918a2f5c` - 14:33:45
**Style: align LogCategories formatting**

Pure formatting change.

---

### Commit 5: `d82fa5564` - 17:26:09
**WIP: Step 4 - Marshal method generation with UCO wrappers, JCW Java, and LLVM IR**

**Major milestone** - 1,026 lines added to `GenerateTypeMapAttributesStep.cs`:
- Added `GetFunctionPointer(int methodIndex)` to `JavaPeerProxy`
- Collect marshal methods from `[Register]` attributes
- Generate `[UnmanagedCallersOnly]` wrapper methods
- Generate JCW `.java` files with native method declarations
- Generate LLVM IR `.ll` files with cached function pointer stubs

---

### Commit 6: `e23ecdc74` - 18:35:08
**WIP: Fix LLVM IR generation and remove duplicate JCW Java generation**

> 🔄 **APPROACH CHANGED:** JCW Java file generation removed - existing JCW generator already handled this. Later re-added in different form.

- Fixed JNI symbol names: `<init>` → `_ctor` for valid identifiers
- Fixed LLVM IR call instruction type annotations

---

### Commit 7: `e6598ae1f` - 19:04:42
**WIP: Integrate get_function_pointer callback for Type Mapping API**

Connected the LLVM IR stubs to managed code:
- Added `GetFunctionPointer` `[UnmanagedCallersOnly]` callback
- Updated native headers with callback signature
- LLVM IR passes class name string + length to resolve function pointers

---

### Commit 8: `dc56a3e98` - 19:34:44
**WIP: Fix LLVM IR type parsing and MSBuild target ordering**

Fixed JNI signature parsing for arrays and object types. **1,730 marshal method .ll files now compile to .o files.**

---

### Commit 9: `713aec20e` - 19:53:21
**WIP: Fix LLVM IR for PIC and correct ABI format**

- Removed `dso_local` for PIC compatibility
- Map RuntimeIdentifier to Android ABI format
- **1,730 .o files now link into libxamarin-app.so**

---

### Commit 10: `0dd28e444` - 21:09:48
**Fix: Generate marshal_methods_init.ll to define get_function_pointer global**

Added the initialization LLVM IR file that declares the global callback pointer.

---

### Commit 11: `0d0a9e821` - 21:26:59
**WIP: fix duplicate xamarin_app_init symbol**

Prevented legacy generator from emitting duplicate symbols by respecting `GenerateEmptyCode` flag.

---

### Commit 12: `33a2deca4` - 22:06:23
**Fix IL1012 crash in GenerateTypeMapAttributesStep**

Fixed linker crash, added verification code to sample MainActivity.

---

### Commit 13: `df4fa731d` - 23:10:28
**WIP: Call xamarin_app_init to initialize get_function_pointer in app DSO**

Added native host code to call the initialization function.

---

### Commit 14: `1a0763beb` - 00:31:50 (Day 2)
**WIP: Fully functional TypeMaps PoC with reflection fallback**

🎉 **First working end-to-end PoC!**

> 🔄 **TEMPORARY APPROACH:** Added `ResolveUserTypeMethod()` reflection fallback for types without generated proxies. This became unnecessary later.

```csharp
static IntPtr ResolveUserTypeMethod(Type type, int index)
{
    // Reflection-based resolution when proxy not available
    var callbackMethod = type.GetMethod(callbackName, ...);
    return callbackMethod.MethodHandle.GetFunctionPointer();
}
```

---

## Day 2: January 22, 2026 - Refinement

### Commit 15: `cf36dcc4e` - 00:56:30
**WIP: Hybrid TypeMaps mode - Disable dynamic registration for Mono.Android on CoreCLR**

> 🔄 **INTERMEDIATE STATE:** Only framework types used TypeMaps, user code still used dynamic registration. Changed in commit 20.

---

### Commit 16: `c972521ce` - 01:16:11
**Fix JNI symbol visibility and signature mangling in LLVM IR generation**

Technical fix for JNI native method naming.

---

### Commit 17: `9337647e0` - 01:27:33
**Register xamarin_typemap_init in p/invoke override tables**

> 🔄 **REMOVED LATER:** This was removed in commit 24 when `typemap_init` was eliminated.

---

### Commit 18: `67c76f91c` - 01:34:20
**Cleanup GenerateTypeMapAttributesStep: remove verbose logging and improve comments**

🧹 Removed debug logging added in earlier commits.

---

### Commit 19: `d64491841` - 07:23:06
**Review feedback: refactor GetFunctionPointer, rename typemap_init**

Addressed code review feedback.

---

### Commit 20: `e9b2f00e8` - 07:25:04
**Review feedback: completely disable RegisterNativeMembers on CoreCLR**

> 🎯 **KEY DECISION:** All native methods now resolved via TypeMaps. `[Export]` support noted as TODO.

```csharp
if (RuntimeFeature.IsCoreClrRuntime) {
    Logger.Log(LogLevel.Debug, "monodroid", 
        $"DynamicNativeMembersRegistration: DISABLED for {type.FullName}");
    return;
}
```

---

### Commit 21: `e5b34ded4` - 09:16:43
**Merge WriteLine calls into raw string literals in GenerateTypeMapAttributesStep**

Code style improvement - using C# 11 raw string literals.

---

### Commit 22: `fed933f8e` - 09:41:26
**Enable GenerateJcwJavaFile with proper base class, wrapper methods, and constructor**

Re-enabled JCW Java file generation with correct implementation.

---

### Commit 23: `cee9cbf5e` - 09:44:31
**Replace reflection with generated CreateInstance factory method**

> 🎯 **MAJOR EVOLUTION:** Added `CreateInstance()` to `JavaPeerProxy`, eliminating reflection for instance creation.

```csharp
public abstract IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer);
```

---

### Commit 24: `dc5a3c111` - 09:46:43
**Remove typemap_init - set get_function_pointer directly from JNIEnvInit.Initialize**

> 🧹 **SIMPLIFICATION:** Eliminated redundant `typemap_init` function. Callback pointer now passed directly.

---

### Commit 25: `16a03ec9f` - 09:47:19
**Mark all review items as complete**

Checkpoint commit.

---

### Commit 26: `a3dc2784b` - 09:55:17
**Implement proper JI constructor support in CreateInstance codegen**

Added support for Java.Interop-style constructors (`ref JniObjectReference, JniObjectReferenceOptions`).

---

### Commit 27: `80076cbb3` - 10:00:41
**Throw NotSupportedException when no constructor found in CreateInstance**

Better error handling for missing activation constructors.

---

### Commit 28: `319469ca6` - 10:04:26
**Remove unused TargetType property from JavaPeerProxy**

> 🧹 **CLEANUP:** `TargetType` property removed - no longer needed after `CreateInstance()` added.

---

### Commit 29: `73b8f7c61` - 10:17:33
**Drop unnecessary DAM attributes**

Removed `[DynamicallyAccessedMembers]` attributes that were no longer needed.

---

### Commit 30: `f279bf519` - 10:28:15
**Add path normalization and debug logging for JCW generation**

> 🔄 **TEMPORARY:** Debug logging added, later cleaned up.

---

### Commit 31: `57de27aab` - 10:42:37
**Add JCW copy target to overwrite old-style JCW after GenerateJavaStubs**

MSBuild target to ensure new JCWs replace old ones.

---

### Commit 32: `d0ec41a9a` - 12:45:11
**WIP: Disable old JCW generation, enable new linker-based JCW generation**

Switched to linker-based JCW generation exclusively.

---

### Commit 33: `1bf92212b` - 13:21:02
**Fix JCW generation to skip framework assemblies and add xamarin_app_init**

Framework assemblies already have JCWs; only user types need generation.

---

### Commit 34: `534f1b108` - 13:26:46
**Remove debug file logging from GenerateTypeMapAttributesStep**

🧹 Cleanup of debug logging.

---

### Commit 35: `e4de42142` - 15:24:20
**Fix get_function_pointer symbol visibility and definition**

LLVM IR symbol visibility fix.

---

### Commit 36: `ad41ffa71` - 21:41:58
**WIP: Fix TypeMap attribute to return proxy type for runtime lookup**

> 🎯 **CRITICAL FIX:** TypeMap now returns the **proxy type** (not target type) for runtime lookup. The target type is stored as `trimTarget` for linker preservation.

```csharp
// TypeMap<Universe>(javaName, proxyType, trimTarget)
[assembly: TypeMap<Java.Lang.Object>("android/app/Activity", 
    typeof(Activity_Proxy),  // Returned at runtime
    typeof(Activity))]       // Preserved by linker
```

---

## Day 3: January 23, 2026 - Final Fixes

### Commit 37: `7e68e037e` - 10:16:57
**WIP: Add LLVM stubs for Implementor types (e.g., IOnClickListenerImplementor)**

Fixed null function pointer crashes for Implementor types in `Mono.Android`.

---

### Commit 38: `6a79636a5` - 15:08:24
**Use JavaInterop1 codegen target for CoreCLR runtime**

Configuration change for CoreCLR-specific code generation.

---

### Commit 39: `f83de12d7` - 15:12:00
**Fix TypeMap activation for CoreCLR runtime**

**Final commit** with multiple cleanup items:
- Changed `GetFunctionPointer` to use `ReadOnlySpan<char>` for UTF-16 strings (zero-copy)
- Added `IsDynamicMemberRegistrationEnabled` feature switch
- Fixed JCW return type parsing to use actual class names
- Generate activation wrappers for Implementor types
- **Removed entire `TimingLog` class** (~76 lines of profiling code)

---

## Summary: Ideas That Evolved

| Aspect | Initial | Final | Changed In |
|--------|---------|-------|------------|
| Proxy interface | `TargetType` property | `CreateInstance()` method | Commits 23, 28 |
| Dynamic registration | Hybrid mode | Fully disabled on CoreCLR | Commit 20 |
| Initialization | `typemap_init` function | Direct pointer via `JNIEnvInit` | Commit 24 |
| Fallback mechanism | Reflection fallback | None needed | Commit 14 → later |
| TypeMap return value | Target type | Proxy type | Commit 36 |
| String encoding | UTF-8 | UTF-16 `ReadOnlySpan<char>` | Commit 39 |

## Temporary Code Patterns

| Pattern | Added | Removed |
|---------|-------|---------|
| Debug file logging | Commits 3, 12, 30 | Commits 18, 34 |
| `TimingLog` profiling class | Commit ~36 | Commit 39 |
| Reflection fallback | Commit 14 | No longer used |
| `TargetType` property | Commit 1 | Commit 28 |

---

*Generated from analysis of git history for the Trimmable Type Mapping PoC*
