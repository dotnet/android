# TypeMap V3 Build Optimization Plan

## Problem Statement

The TypeMap V3 build process has issues with both CoreCLR Release and NativeAOT builds:
1. **TypeMap generation runs at the wrong time** - different timing issues affect CoreCLR vs NativeAOT
2. **Redundant per-RID execution** - TypeMap is RID-invariant but runs once per architecture (4x for multi-RID)
3. **ILLink runs for NativeAOT when ILC does its own trimming** - redundant and slows builds
4. **Nested MSBuild complexity** - per-RID compilation via `_ComputeFilesToPublishForRuntimeIdentifiers` makes orchestration fragile
5. **Custom ILLink steps block skipping ILLink** - need to migrate to AssemblyModifierPipeline

## Goals

- ✅ TypeMap V3 works correctly with CoreCLR Release builds
- ✅ TypeMap V3 works correctly with NativeAOT builds  
- ✅ **Skip ILLink entirely for NativeAOT** (ILC does its own trimming)
- ✅ Faster build times through reduced redundancy
- ✅ Better caching - don't invalidate already-built artifacts unnecessarily
- ✅ Simpler build orchestration

---

## CURRENT Build Flow (Detailed Analysis)

### Complete Target Execution Order

```
OUTER BUILD (RuntimeIdentifier='', RuntimeIdentifiers='android-arm64;android-x64;...')
================================================================================
Microsoft.Android.Sdk.BuildOrder.targets is IMPORTED (line 15 in After.targets)

_PrepareBuildApkDependsOnTargets executes:
├── _SetLatestTargetFrameworkVersion
├── _GetLibraryImports
├── _RemoveRegisterAttribute  ← RID-agnostic, could share output
├── _ResolveAssemblies        ← ⚡ SPAWNS INNER BUILDS HERE
│   │
│   └──► MSBuild Projects="@(_ProjectToBuild)" Targets="_ComputeFilesToPublishForRuntimeIdentifiers"
│        BuildInParallel="true"
│        │
│        ├── INNER BUILD: android-arm64 ─────────────────────────────────────
│        │   ├── BuildOnlySettings
│        │   ├── _FixupIntermediateAssembly
│        │   ├── ResolveReferences
│        │   ├── ComputeFilesToPublish
│        │   │   ├── PrepareForILLink
│        │   │   ├── _PrepareLinking
│        │   │   │   └── DependsOn: _GenerateTypeMapAssembly  ← ⚠️ RUNS HERE!
│        │   │   ├── ILLink                                   ← ⚠️ RUNS HERE!
│        │   │   └── ... other SDK trimming targets
│        │   ├── [NativeAOT only] IlcCompile
│        │   └── $(_RunAotMaybe) = _AndroidAot               ← ⚠️ RUNS HERE!
│        │
│        ├── INNER BUILD: android-x64 ───────────────────────────────────────
│        │   └── (same as arm64 - TypeMap, ILLink, AOT all run AGAIN)
│        │
│        ├── INNER BUILD: android-arm ───────────────────────────────────────
│        │   └── (same - runs AGAIN)
│        │
│        └── INNER BUILD: android-x86 ───────────────────────────────────────
│            └── (same - runs AGAIN)
│
├── _ResolveSatellitePaths
├── _CreatePackageWorkspace
├── _LinkAssemblies           ← Outer build, but ILLink already ran in inner!
├── _AfterILLinkAdditionalSteps
├── _GenerateJavaStubs        ← Uses TypeMap output from inner builds
├── _ManifestMerger
├── _ConvertCustomView
├── _ReadAndroidManifest
├── _CompileJava              ← ONCE (RID-agnostic) ✅
├── _CreateApplicationSharedLibraries
├── _CompileDex               ← ONCE (RID-agnostic) ✅
├── _CreateBaseApk
├── _PrepareAssemblies
└── ... packaging ...
```

### What's Redundant (4x instead of 1x)

| Step | Currently | Should Be | Savings |
|------|-----------|-----------|---------|
| `_GenerateTypeMapAssembly` | Per-RID (4x) | Once | **75%** |
| `ILLink` | Per-RID (4x) | Once for CoreCLR, Skip for NativeAOT | **75-100%** |
| `_PrepareLinking` | Per-RID (4x) | Once | **75%** |
| Assembly copying/resolution | Per-RID (4x) | Partially shared | ~50% |

### What's Already Optimized ✅

| Step | Execution | Notes |
|------|-----------|-------|
| `_CompileJava` | Once | Java is architecture-neutral |
| `_CompileDex` | Once | DEX is architecture-neutral |
| Resource processing | Once | XML resources are neutral |
| Manifest merging | Once | Single merged manifest |

---

## Key Discoveries

### 1. Mono.Android.dll is IDENTICAL Across All RIDs ✅

**Critical finding**: Mono.Android.dll is built once (not per-RID) and is pure managed IL code. This means:
- ILLink trimmed output CAN be shared across all Android RIDs
- Only native libraries (.so files) differ per architecture
- User assemblies + framework assemblies have identical trimming decisions

### 2. AssemblyModifierPipeline Already Exists ✅

There's already infrastructure to run assembly modifications **outside of ILLink**:
- `AssemblyModifierPipeline` task in `src/Xamarin.Android.Build.Tasks/Tasks/`
- Uses `IAssemblyModifierPipelineStep` interface
- Runs **after** ILLink (or standalone when `PublishTrimmed=false`)
- Steps already migrated: `FindJavaObjectsStep`, `FindTypeMapObjectsStep`

### 3. WIP PRs Are Migrating ILLink Steps

Active work to migrate ILLink custom steps to MSBuild tasks:
- **PR #10694**: Migrate `GenerateProguardConfiguration` → MSBuild task (runs AfterTargets="ILLink")
- **PR #10695**: Migrate `StripEmbeddedLibraries` → `AssemblyModifierPipeline` step

### 4. Custom ILLink Steps Catalog

| Step | Modifies IL? | Can Migrate to AssemblyModifierPipeline? | Needed for NativeAOT? |
|------|-------------|------------------------------------------|----------------------|
| `PreserveSubStepDispatcher` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO |
| `MarkJavaObjects` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO (ILC trims) |
| `PreserveJavaExceptions` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO |
| `PreserveApplications` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO |
| `PreserveRegistrations` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO |
| `PreserveJavaInterfaces` | No | Eliminate (use TrimmerRootDescriptor) | ❌ NO |
| `FixAbstractMethodsStep` | **YES** | ✅ YES (use AssemblyModifierPipeline) | ✅ YES |
| `StripEmbeddedLibraries` | **YES** | ✅ YES (PR #10695) | ✅ YES |
| `GenerateProguardConfiguration` | No | ✅ YES (PR #10694) | ❌ NO |
| `AddKeepAlivesStep` | **YES** | ✅ YES (use AssemblyModifierPipeline) | ✅ YES |
| `RemoveResourceDesignerStep` | **YES** | ✅ YES (use AssemblyModifierPipeline) | ❓ Maybe |
| `FixLegacyResourceDesignerStep` | **YES** | ✅ YES (use AssemblyModifierPipeline) | ❓ Maybe |

---

## Proposed Architecture: "CoreCLR uses ILLink, NativeAOT Skips It"

### New Build Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         OUTER BUILD                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  [1] ResolveReferences                                                  │
│       └─► Gather all assemblies (ReferencePath items)                   │
│                                                                         │
│  [2] _GenerateTypeMapAssembly (ONCE - RID agnostic)                    │
│       ├─► Scan assemblies for [Register] attributes                    │
│       ├─► Generate _Microsoft.Android.TypeMaps.dll                     │
│       ├─► Generate JCW .java files                                     │
│       └─► Generate LLVM IR templates                                   │
│                                                                         │
│  [3] _CompileJava (ONCE - architecture neutral)                        │
│       └─► Compile JCWs to .class → dex                                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
┌─────────────────────────────────┐ ┌─────────────────────────────────┐
│     CORECLR RELEASE PATH        │ │     NATIVEAOT PATH              │
├─────────────────────────────────┤ ├─────────────────────────────────┤
│                                 │ │                                 │
│  [4a] ILLink (ONCE)            │ │  [4b] Skip ILLink entirely!     │
│       ├─► Include TypeMap asm  │ │       └─► ILC does trimming     │
│       ├─► Custom preserve XML  │ │                                 │
│       └─► Trimmed assemblies   │ │                                 │
│                                 │ │                                 │
│  [5a] AssemblyModifierPipeline │ │  [5b] AssemblyModifierPipeline  │
│       ├─► FixAbstractMethods   │ │       ├─► FixAbstractMethods    │
│       ├─► StripEmbeddedLibs    │ │       ├─► StripEmbeddedLibs     │
│       └─► AddKeepAlives        │ │       └─► AddKeepAlives         │
│                                 │ │                                 │
└─────────────────────────────────┘ └─────────────────────────────────┘
                    │                               │
                    └───────────────┬───────────────┘
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    PER-RID INNER BUILDS (parallel)                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐         │
│  │  android-arm64  │  │  android-x64    │  │  android-arm    │  ...    │
│  ├─────────────────┤  ├─────────────────┤  ├─────────────────┤         │
│  │ Copy assemblies │  │ Copy assemblies │  │ Copy assemblies │         │
│  │ from outer      │  │ from outer      │  │ from outer      │         │
│  │                 │  │                 │  │                 │         │
│  │ [CoreCLR only]: │  │ [CoreCLR only]: │  │ [CoreCLR only]: │         │
│  │  MonoAOT        │  │  MonoAOT        │  │  MonoAOT        │         │
│  │                 │  │                 │  │                 │         │
│  │ [NativeAOT]:    │  │ [NativeAOT]:    │  │ [NativeAOT]:    │         │
│  │  ILC compile    │  │  ILC compile    │  │  ILC compile    │         │
│  │                 │  │                 │  │                 │         │
│  │ Link .so        │  │ Link .so        │  │ Link .so        │         │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         PACKAGE APK                                     │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key Changes from Current

1. **TypeMap moves to outer build** - runs ONCE, RID-agnostic
2. **ILLink runs ONCE for CoreCLR** (not per-RID) - output shared across RIDs
3. **ILLink SKIPPED for NativeAOT** - ILC does its own trimming
4. **AssemblyModifierPipeline handles IL modifications** - independent of ILLink
5. **Per-RID inner builds only do AOT/ILC and native linking**

---

## Implementation Phases

### Phase 0: Migrate Remaining ILLink Custom Steps (PREREQUISITE)

Before we can skip ILLink for NativeAOT, ALL IL-modifying steps must move to AssemblyModifierPipeline.

- [ ] **0.1** Complete PR #10694: `GenerateProguardConfiguration` → MSBuild task
- [ ] **0.2** Complete PR #10695: `StripEmbeddedLibraries` → AssemblyModifierPipeline
- [ ] **0.3** Migrate `FixAbstractMethodsStep` → AssemblyModifierPipeline
- [ ] **0.4** Migrate `AddKeepAlivesStep` → AssemblyModifierPipeline  
- [ ] **0.5** Migrate `RemoveResourceDesignerStep` → AssemblyModifierPipeline
- [ ] **0.6** Migrate `FixLegacyResourceDesignerStep` → AssemblyModifierPipeline
- [ ] **0.7** Convert analysis-only steps to TrimmerRootDescriptor XML files:
  - `PreserveSubStepDispatcher` → XML
  - `MarkJavaObjects` → XML
  - `PreserveJavaExceptions` → XML
  - `PreserveApplications` → XML
  - `PreserveRegistrations` → XML  
  - `PreserveJavaInterfaces` → XML

---

## CRITICAL GAP: Post-Trimming Processing

### The Problem

The current PoC is missing **post-trimming filtering** for:
1. **Java classes (JCWs)** - Only include Java classes for .NET types that survived trimming
2. **LLVM IR files** - Only include marshal method stubs for types that survived trimming

---

## Replacing Preservation Steps with TypeMap Attributes

### Current Situation: ILLink Custom Steps for Preservation

The following ILLink custom steps exist to preserve certain types/methods:

| Step | What It Preserves | How |
|------|-------------------|-----|
| `PreserveApplications` | BackupAgent, ManageSpaceActivity types | Marks types referenced in `[Application]` attribute properties |
| `PreserveJavaExceptions` | `ctor(string)` on exception types | Marks string constructor on types inheriting `Java.Lang.Throwable` |
| `PreserveJavaInterfaces` | All methods on Java-interop interfaces | Marks all methods on interfaces implementing `IJavaObject` |
| `MarkJavaObjects` | Activation constructors | Marks `(IntPtr, JniHandleOwnership)` ctor |
| `PreserveRegistrations` | [Register] types | Marks types with `[Register]` attribute |

### The Problem with XML Root Descriptors

The original plan was to convert these to static XML `TrimmerRootDescriptor` files:
```xml
<linker>
  <assembly fullname="*">
    <type fullname="*" inherits="Java.Lang.Throwable">
      <method signature=".ctor(System.String)" />
    </type>
  </assembly>
</linker>
```

**Problems:**
1. **Static** - Can't adapt based on actual assemblies being built
2. **No NativeAOT support** - ILLink XML descriptors don't work with ILC
3. **Still requires ILLink** - Defeats the goal of skipping ILLink

### New Approach: TypeMapAttribute / TypeMapAssociationAttribute

The `TypeMapAttribute<T>` and `TypeMapAssociationAttribute<T>` in .NET 10+ provide **linker/ILC-aware type mappings**:

#### TypeMapAttribute Constructor Variants

**UNCONDITIONAL** (proxy always preserved):
```csharp
// TypeMapAttribute<T>(string externalName, Type proxyType)
// - Proxy is ALWAYS preserved
// - NO [RequiresUnreferencedCode] - trimmer-safe!

[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]
// MainActivity_Proxy is ALWAYS preserved → MainActivity preserved via direct refs
```

**TRIMMABLE** (only if .NET code uses it):
```csharp
// TypeMapAttribute<T>(string externalName, Type proxyType, Type trimTarget)
// - Proxy preserved ONLY IF trimTarget is used (allocation, typeof, etc.)
// - HAS [RequiresUnreferencedCode] - may be trimmed!

[assembly: TypeMap<JavaObjects>("android/widget/TextView", typeof(TextView_Proxy), typeof(TextView))]
// TextView preserved only if user code allocates/uses it
```

#### TypeMapAssociationAttribute Semantics

```csharp
// TypeMapAssociationAttribute<T>(Type source, Type proxy)
// If source is EXPLICITLY ALLOCATED (newobj, box, newarr, etc.):
//   1. The mapping is included in the type map
//   2. The proxy type is preserved
//
// This is NOT triggered by mere "keeping" - requires ALLOCATION observation

[assembly: TypeMapAssociation<JavaObjects>(typeof(MainActivity), typeof(MainActivity_Proxy))]
// When MainActivity is ALLOCATED, the proxy is kept
```

**Key insight:** These attributes work with BOTH ILLink AND ILC (NativeAOT)!

### Preservation Rules: Unconditional vs Trimmable

| Type Category | Preservation | Reason |
|---------------|--------------|--------|
| **JCW types** (user's .NET types with Java wrappers) | **Unconditional** | Java/Android can create them anytime |
| **Manifest-registered types** (Activity, Service, Receiver, Provider) | **Unconditional** | Android system creates them |
| **Application subclasses** | **Unconditional** | Android creates at app startup |
| **Exception types** | **Unconditional** | May be thrown from Java |
| **MCW types** (bindings for existing Java classes) | **Trimmable** | Only if .NET code uses them |

### Key Insight: TypeMapAttribute Points to PROXY Types

The TypeMapAttribute should reference the **proxy type**, not the original type:

```csharp
// CORRECT: Point to the proxy type
[assembly: TypeMap<JavaObjects>("com/example/MainActivity", typeof(MainActivity_Proxy))]

// The proxy has DIRECT REFERENCES to the real type's methods:
class MainActivity_Proxy {
    // Activation ctor directly calls MainActivity's ctor
    public static Java.Lang.Object CreateInstance(IntPtr handle, JniHandleOwnership ownership) {
        return new MainActivity(handle, ownership);  // Direct reference!
    }
    
    // Marshal methods directly call MainActivity's methods
    public static void n_OnCreate(IntPtr jnienv, IntPtr native__this, IntPtr bundle) {
        var __this = (MainActivity)Java.Lang.Object.GetObject<MainActivity>(native__this);
        __this.OnCreate(bundle);  // Direct reference!
    }
}
```

**Why this works for preservation:**
1. Unconditional TypeMapAttribute **always preserves** MainActivity_Proxy
2. MainActivity_Proxy has **direct method references** to MainActivity
3. The trimmer sees these references and **automatically preserves** MainActivity
4. **No `[DynamicDependency]` needed!**

### Replacing Preservation Steps - Simplified

Since proxy types directly reference all needed methods, the TypeMapAttribute does ALL the work:

#### PreserveApplications

```csharp
// The proxy references BackupAgent directly in its code
class MyApp_Proxy {
    static Type GetBackupAgentType() => typeof(MyBackupAgent);  // Direct reference!
}

// Unconditional preservation - no DynamicDependency needed
[assembly: TypeMap<JavaObjects>("com/example/MyApp", typeof(MyApp_Proxy))]
```

#### PreserveJavaExceptions

```csharp
// The exception proxy calls the string ctor directly
class MyException_Proxy {
    public static Java.Lang.Object CreateInstance(IntPtr handle, JniHandleOwnership ownership) {
        // The activation code references the real constructors
        return new MyException(handle, ownership);
    }
    
    // For string ctor - generate a method that calls it
    public static MyException CreateWithMessage(string message) {
        return new MyException(message);  // Direct reference to ctor(string)!
    }
}

// Unconditional preservation
[assembly: TypeMap<JavaObjects>("java/lang/MyException", typeof(MyException_Proxy))]
```

#### PreserveJavaInterfaces

```csharp
// Interface proxy has stubs that reference all methods
class IMyInterface_Proxy {
    public static void n_DoSomething(IntPtr jnienv, IntPtr native__this) {
        var __this = (IMyInterface)Java.Lang.Object.GetObject<IMyInterface>(native__this);
        __this.DoSomething();  // Direct reference to interface method!
    }
}

// Unconditional preservation
[assembly: TypeMap<JavaObjects>("com/example/IMyInterface", typeof(IMyInterface_Proxy))]
```

### Summary: TypeMapAttribute Preservation Rules

| Attribute | Preservation | Triggers On |
|-----------|--------------|-------------|
| `TypeMapAttribute(str, proxyType)` | **Unconditional** | Always preserved |
| `TypeMapAttribute(str, proxyType, trimTarget)` | **Trimmable** | Only if trimTarget used |
| `TypeMapAssociationAttribute(src, proxy)` | **Trimmable** | Only if src allocated |

### Final Migration Checklist

| ILLink Step | Replacement | Status |
|-------------|-------------|--------|
| `PreserveApplications` | Unconditional TypeMapAttribute to proxy (proxy refs BackupAgent) | 🔲 TODO |
| `PreserveJavaExceptions` | Unconditional TypeMapAttribute to proxy (proxy refs string ctor) | 🔲 TODO |
| `PreserveJavaInterfaces` | Unconditional TypeMapAttribute to proxy (proxy refs all methods) | 🔲 TODO |
| `MarkJavaObjects` | Unconditional TypeMapAttribute to proxy (proxy refs activation ctor) | ✅ Already done |
| `PreserveRegistrations` | Unconditional TypeMapAttribute to proxy | ✅ Already done |

### Implementation in GenerateTypeMapAssembly

```csharp
// ALL TypeMapAttribute entries point to PROXY types, not original types

// For JCW types (user types with Java wrappers) - Unconditional
if (type.HasJavaCallableWrapper()) {
    // Generate proxy type with direct method references
    var proxyType = GenerateProxyType(type);
    
    // Unconditional - proxy always preserved, real type preserved via refs
    AddTypeMapAttribute(jniName, proxyType);  
}

// For MCW types (SDK bindings) - Trimmable
if (type.IsManagedCallableWrapper()) {
    var proxyType = GenerateProxyType(type);
    
    // Trimmable - only if user code uses this type
    AddTypeMapAttribute(jniName, proxyType, type);
}

// No DynamicDependency needed!
// The proxy types have direct method calls/references to:
// - Activation constructors (IntPtr, JniHandleOwnership)
// - String constructors for exceptions
// - All interface methods
// - BackupAgent types (via typeof() or direct usage in generated code)
```

---

## CRITICAL GAP: Post-Trimming Processing

### Current Flow (How It Works Today)

```
┌─────────────────────────────────────────────────────────────────────────┐
│ BEFORE TRIMMING                                                         │
├─────────────────────────────────────────────────────────────────────────┤
│ GenerateTypeMapAssembly:                                                │
│ ├─► Scans ALL assemblies for [Register] attributes                     │
│ ├─► Generates JCW .java files for ALL types                            │
│ ├─► Generates LLVM IR marshal_methods_*.ll for ALL types               │
│ └─► Creates _Microsoft.Android.TypeMaps.dll with ALL mappings          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ TRIMMING (ILLink or ILC)                                                │
├─────────────────────────────────────────────────────────────────────────┤
│ Removes unused types → some [Register] types are TRIMMED               │
│ ❌ TypeA, TypeB, TypeC survive                                         │
│ 🗑️  TypeD, TypeE are trimmed away                                       │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ AFTER TRIMMING (⚠️ CURRENTLY MISSING IN POC)                            │
├─────────────────────────────────────────────────────────────────────────┤
│ GenerateProguardConfiguration (ILLink custom step):                     │
│ ├─► Walks SURVIVING assemblies only                                    │
│ ├─► Generates -keep rules ONLY for surviving [Register] types          │
│ └─► Output: proguard_project_references.cfg                            │
│                                                                         │
│ LLVM IR cherry-picking:                                                 │
│ ├─► ??? No current mechanism to filter marshal_methods_*.ll files      │
│ └─► All generated .ll files are compiled (even for trimmed types!)     │
└─────────────────────────────────────────────────────────────────────────┘
```

### What's Needed for TypeMap V3

#### 1. Java Class Filtering (ProGuard)

**Current mechanism** (GenerateProguardConfiguration.cs):
- Runs as ILLink custom step `AfterStep="CleanStep"`
- Iterates over **surviving assemblies only** (post-trimming context)
- Generates `-keep class` rules only for types with `[Register]` still present

**For NativeAOT (no ILLink)**:
- Need equivalent filtering based on what ILC kept
- Options:
  a. Generate ProGuard config from ILC's kept types list
  b. Use ILC's XML root descriptor output
  c. Run a separate MSBuild task post-ILC that scans surviving types

**Proposed solution**:
```csharp
// New MSBuild task: GenerateProguardConfigurationFromTrimmedAssemblies
public class GenerateProguardConfiguration : AndroidTask
{
    public ITaskItem[] TrimmedAssemblies { get; set; }  // Post-ILLink or post-ILC
    public string OutputFile { get; set; }
    
    public override bool Execute()
    {
        foreach (var asm in TrimmedAssemblies)
        {
            var assembly = AssemblyDefinition.ReadAssembly(asm.ItemSpec);
            foreach (var type in assembly.MainModule.Types)
            {
                var register = type.CustomAttributes.FirstOrDefault(
                    a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
                if (register != null)
                {
                    // Type survived trimming → generate -keep rule
                    WriteKeepRule(type, register);
                }
            }
        }
    }
}
```

#### 2. LLVM IR Cherry-Picking

**Current state**: 
- `GenerateTypeMapAssembly` generates `marshal_methods_{TypeHash}.ll` for ALL types
- No post-trimming filtering - all .ll files are compiled to .o and linked

**What's needed**:
- After trimming, determine which types survived
- Only compile/link the .ll files for surviving types
- Delete or skip .ll files for trimmed types

**Options**:

**Option A: Name .ll files by type hash, filter by surviving type list**
```csharp
// In GenerateTypeMapAssembly: Generate with predictable names
File.WriteAllText($"marshal_methods_{ComputeTypeHash(type)}.ll", irContent);

// Post-trimming: Get list of surviving type hashes
var survivingHashes = GetSurvivingTypes(trimmedAssemblies)
    .Select(t => ComputeTypeHash(t))
    .ToHashSet();

// Filter .ll files
var llFilesToCompile = Directory.GetFiles("marshal_methods_*.ll")
    .Where(f => survivingHashes.Contains(ExtractHashFromFilename(f)));
```

**Option B: Generate .ll files AFTER trimming (simpler but slower)**
```
1. TypeMap assembly scanning → BEFORE trimming (type metadata)
2. LLVM IR generation → AFTER trimming (only for surviving types)
```

~~**Option C: Let `--gc-sections` handle it at link time**~~ ❌ **DOES NOT WORK**

**Why `--gc-sections` doesn't help**: Java loads JNI symbols **dynamically at runtime** via `dlopen`/`dlsym`. The native linker has NO visibility into which symbols Java will call. All JNI symbols appear unreferenced from the linker's perspective, so either ALL are kept (via `--export-dynamic`) or ALL are removed.

---

## Correct Strategy: Filter .o Files Based on Surviving .NET Types

### The Approach

```
┌─────────────────────────────────────────────────────────────────────────┐
│ BEFORE TRIMMING (generate everything ONCE)                              │
├─────────────────────────────────────────────────────────────────────────┤
│ GenerateTypeMapAssembly:                                                │
│ ├─► Scan ALL assemblies for [Register] attributes                      │
│ ├─► Generate marshal_methods_{TypeHash}.ll for EACH type (per-type)    │
│ ├─► Generate JCW .java source for EACH type                            │
│ └─► Create _Microsoft.Android.TypeMaps.dll with ALL mappings           │
│                                                                         │
│ LLVM Compilation (per-arch, can cache):                                 │
│ └─► Compile each .ll → .o (marshal_methods_{TypeHash}_{abi}.o)         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ TRIMMING (ILLink or ILC)                                                │
├─────────────────────────────────────────────────────────────────────────┤
│ Removes unused .NET types                                               │
│ ✅ TypeA, TypeB, TypeC survive                                          │
│ 🗑️  TypeD, TypeE are trimmed away                                        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ POST-TRIMMING: Compute Surviving Types (SINGLE PASS)                    │
├─────────────────────────────────────────────────────────────────────────┤
│ GetSurvivingJavaTypes task:                                             │
│ ├─► Input: Trimmed assemblies                                          │
│ ├─► Scan for types with [Register] still present                       │
│ └─► Output: List of surviving type identifiers/hashes                  │
│                                                                         │
│ This list is used for BOTH:                                             │
│ 1. Filtering which .o files to link                                    │
│ 2. Generating ProGuard configuration                                   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
┌─────────────────────────────────┐ ┌─────────────────────────────────┐
│ Filter .o Files                 │ │ Generate ProGuard Config        │
├─────────────────────────────────┤ ├─────────────────────────────────┤
│ For each surviving type:        │ │ For each surviving type:        │
│   Include its .o file           │ │   Write -keep class rule        │
│                                 │ │                                 │
│ Trimmed types' .o files are     │ │ Trimmed types get NO rules      │
│ simply NOT passed to linker     │ │ → R8/ProGuard removes them      │
└─────────────────────────────────┘ └─────────────────────────────────┘
                    │                               │
                    └───────────────┬───────────────┘
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ NATIVE LINKING                                                          │
├─────────────────────────────────────────────────────────────────────────┤
│ Link ONLY the .o files for surviving types                              │
│ Result: libapp.so contains only needed JNI marshal methods              │
└─────────────────────────────────────────────────────────────────────────┘
```

### Debug vs Release Build Behavior

#### Debug Builds (No Trimming)

When `$(PublishTrimmed)=false` (typical Debug configuration):

```
GenerateTypeMaps
      │
      ├─► ALL .java files → javac → d8 → DEX (no ProGuard/R8)
      │
      ├─► ALL .ll files → .o → link ALL into libmarshal_methods.so
      │
      └─► TypeMapAssembly.dll with ALL types
```

**Key behavior:**
- **No filtering needed** - all types survive (no trimming)
- ALL .java files compiled and packaged
- ALL .o files linked into native library
- Larger APK size (acceptable for Debug)
- Faster build (no trimming or filtering overhead)
- `FilterMarshalMethodsAndGenerateProguard` task is **SKIPPED**

#### Release Builds (With Trimming)

When `$(PublishTrimmed)=true` (typical Release configuration):

```
GenerateTypeMaps → ILLink/ILC → FilterMarshalMethodsAndGenerateProguard
                                         │
                                         ├─► Surviving .o files only → link
                                         └─► ProGuard config → R8 removes dead Java
```

**Key behavior:**
- Post-trimming filtering is **REQUIRED**
- Only surviving types' .o files are linked
- ProGuard config ensures R8 removes dead Java classes
- Smaller APK size (dead code removed)
- Longer build (trimming + filtering overhead)

#### MSBuild Condition

```xml
<Target Name="_FilterMarshalMethodsByTrimming"
        AfterTargets="ILLink;IlcCompile"
        BeforeTargets="_CollectTypeMapMarshalMethodSources"
        Condition=" '$(PublishTrimmed)' == 'true' ">
  <!-- Only run for Release builds with trimming enabled -->
</Target>
```

#### Comparison Table

| Aspect | Debug | Release |
|--------|-------|---------|
| Trimming | OFF | ON |
| .ll/.o files | Link ALL | Link surviving only |
| Java classes | Compile ALL | R8 removes dead |
| ProGuard config | Not generated | Generated for surviving types |
| APK size | Larger | Optimized |
| Build speed | Faster | Slower (trimming overhead) |
| Post-trimming filter | SKIPPED | REQUIRED |

### Key Design Decisions

1. **One .ll file per type** (not one combined file)
   - Enables selective linking based on trimming results
   - Naming: `marshal_methods_{TypeHash}.ll` where TypeHash uniquely identifies the type

2. **Compile .ll → .o per-architecture**
   - 4 .o files per type (arm64, x64, arm, x86)
   - Can be cached between builds (input .ll unchanged → skip recompile)

3. **Single "surviving types" computation drives both**:
   - `.o` file filtering for native link
   - ProGuard rule generation for R8

4. **Java dynamically loads symbols** via `dlsym`
   - Linker can't see which symbols are needed
   - We MUST explicitly control which .o files are linked

### Implementation Details

#### Type Hash / Identifier

Need a stable identifier for each type that:
- Maps .NET type → .ll/.o filename
- Can be computed both at generation time AND post-trimming
- Survives trimming (based on type metadata, not assembly position)

Options:
- **Full type name hash**: `SHA256(FullTypeName).Substring(0, 16)`
- **Java class name hash**: Based on `[Register]` attribute value
- **Incremental index**: Simpler but requires stable ordering

#### File Naming Convention

```
$(IntermediateOutputPath)android/
├── marshal_methods_a1b2c3d4.ll          # TypeA
├── marshal_methods_e5f6g7h8.ll          # TypeB  
├── marshal_methods_i9j0k1l2.ll          # TypeC (will be filtered out)
├── arm64-v8a/
│   ├── marshal_methods_a1b2c3d4.o       # TypeA
│   ├── marshal_methods_e5f6g7h8.o       # TypeB
│   └── marshal_methods_i9j0k1l2.o       # TypeC (exists but not linked)
├── x86_64/
│   └── ...
└── ...
```

#### Post-Trimming Task

```csharp
public class FilterMarshalMethodsAndGenerateProguard : AndroidTask
{
    [Required]
    public ITaskItem[] TrimmedAssemblies { get; set; }
    
    [Required]
    public string MarshalMethodsDirectory { get; set; }
    
    [Required]
    public string ProguardOutputFile { get; set; }
    
    [Output]
    public ITaskItem[] SurvivingMarshalMethodObjects { get; set; }
    
    public override bool Execute()
    {
        var survivingTypes = new List<SurvivingType>();
        
        // Scan trimmed assemblies for surviving [Register] types
        foreach (var asm in TrimmedAssemblies)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(asm.ItemSpec);
            foreach (var type in assembly.MainModule.GetTypes())
            {
                var register = type.CustomAttributes
                    .FirstOrDefault(a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
                if (register != null)
                {
                    string javaClass = register.ConstructorArguments[0].Value.ToString();
                    string typeHash = ComputeTypeHash(type);
                    survivingTypes.Add(new SurvivingType(type, javaClass, typeHash));
                }
            }
        }
        
        // Generate ProGuard rules for surviving types
        using (var writer = File.CreateText(ProguardOutputFile))
        {
            foreach (var st in survivingTypes)
            {
                writer.WriteLine($"-keep class {st.JavaClass.Replace('/', '.')}");
            }
        }
        
        // Build list of .o files to include in linking
        var objectFiles = new List<ITaskItem>();
        foreach (var st in survivingTypes)
        {
            // For each ABI, add the corresponding .o file
            foreach (var abi in new[] { "arm64-v8a", "x86_64", "armeabi-v7a", "x86" })
            {
                string oFile = Path.Combine(MarshalMethodsDirectory, abi, $"marshal_methods_{st.TypeHash}.o");
                if (File.Exists(oFile))
                {
                    var item = new TaskItem(oFile);
                    item.SetMetadata("Abi", abi);
                    objectFiles.Add(item);
                }
            }
        }
        
        SurvivingMarshalMethodObjects = objectFiles.ToArray();
        return true;
    }
}
```

---

## TypeMap V3 Spec Updates Needed

Based on this discussion, the V3 spec should clarify:

1. **Pre-trimming generation**: All artifacts (TypeMap assembly, JCW .java, LLVM IR .ll) generated ONCE before trimming

2. **Per-type .ll files**: Each Java-backed .NET type gets its own `marshal_methods_{hash}.ll` file

3. **Per-arch compilation**: Each .ll file compiled to .o for each target architecture

4. **Post-trimming filtering**: Single task scans surviving types and:
   - Outputs list of .o files to link (per-abi)
   - Outputs ProGuard configuration

5. **No `--gc-sections` reliance**: Java uses dynamic symbol loading, linker can't help

---

## Updated Implementation Plan

### Phase 1.5: Post-Trimming Filtering

- [ ] **1.5.1** Migrate `GenerateProguardConfiguration` from ILLink step to MSBuild task
  - Input: `@(_ShrunkAssemblies)` or `@(_IlcCompiledAssemblies)`
  - Output: `$(IntermediateOutputPath)proguard/proguard_project_references.cfg`
  - Run `AfterTargets="ILLink"` for CoreCLR
  - Run `AfterTargets="IlcCompile"` for NativeAOT

- [ ] **1.5.2** Implement LLVM IR filtering
  - After trimming, enumerate surviving types with `[Register]`
  - Compute expected .ll filenames
  - Remove/skip .ll files for trimmed types from `@(AndroidNativeSource)`
  - Alternative: Add `Condition` metadata to items based on survival

- [ ] **1.5.3** Update `_CollectTypeMapMarshalMethodSources` target
  ```xml
  <Target Name="_FilterMarshalMethodsByTrimming"
          AfterTargets="ILLink;IlcCompile"
          BeforeTargets="_CollectTypeMapMarshalMethodSources">
    <!-- Compute which types survived -->
    <GetSurvivingJavaTypes TrimmedAssemblies="@(_ShrunkAssemblies)">
      <Output TaskParameter="SurvivingTypeHashes" ItemName="_SurvivingTypeHash" />
    </GetSurvivingJavaTypes>
    
    <!-- Filter marshal method files -->
    <ItemGroup>
      <_TypeMapMarshalMethodsSource Remove="@(_TypeMapMarshalMethodsSource)" 
          Condition=" !@(_SurvivingTypeHash->AnyHaveMetadataValue('Hash', '%(Filename)'))" />
    </ItemGroup>
  </Target>
  ```

- [ ] **1.5.4** Test trimming scenarios
  - Verify trimmed types don't have JCWs in APK
  - Verify trimmed types don't have marshal methods in .so
  - Verify runtime works correctly (no JNI lookup errors)

---

### Phase 1: Skip ILLink for NativeAOT

- [ ] **1.1** Add condition to skip ILLink for NativeAOT:
  ```xml
  <Target Name="_RunILLink" 
          Condition=" '$(PublishTrimmed)' == 'true' AND '$(_AndroidRuntime)' != 'NativeAOT' ">
  ```

- [ ] **1.2** Ensure AssemblyModifierPipeline runs for BOTH CoreCLR and NativeAOT
  - Currently tied to ILLink; must run independently for NativeAOT
  - Add target: `_RunAssemblyModifierPipeline` that runs regardless of ILLink

- [ ] **1.3** Update NativeAOT targets to receive unlinked assemblies
  - ILC receives original assemblies (not ILLink output)
  - ILC does its own trimming with proper root descriptors

- [ ] **1.4** Pass TypeMap and preserve info to ILC
  - Generate ILC-compatible root descriptors from TypeMap data
  - Ensure Java interop types survive ILC trimming

### Phase 2: Move TypeMap to Outer Build

- [ ] **2.1** Create `_ResolveAssembliesForTypeMap` target in outer build
  - Gather assemblies from `ReferencePath` items (available after `ResolveReferences`)
  - Run before `_ResolveAssemblies`

- [ ] **2.2** Modify `_GenerateTypeMapAssembly` to run in outer build
  - Add condition: `Condition=" '$(_ComputeFilesToPublishForRuntimeIdentifiers)' != 'true' "`
  - Output to shared location: `$(IntermediateOutputPath)typemap\`

- [ ] **2.3** Pass TypeMap outputs to inner builds
  ```xml
  <_AdditionalProperties>
    ...
    ;_TypeMapAssemblyPath=$(_TypeMapAssemblyPath)
    ;_TypeMapJavaSourceDirectory=$(_TypeMapJavaSourceDirectory)
  </_AdditionalProperties>
  ```

- [ ] **2.4** Inner builds consume TypeMap instead of generating
  - Skip `_GenerateTypeMapAssembly` when `_TypeMapAssemblyPath` is set
  - Add TypeMap assembly to `ManagedAssemblyToLink`

### Phase 3: Run ILLink Once (CoreCLR), Share Output

- [ ] **3.1** Run ILLink in outer build for CoreCLR
  - Pick canonical RID (android-arm64) for runtime pack resolution
  - Or use reference assemblies if possible

- [ ] **3.2** Share ILLink output across RIDs
  - Trimmed user assemblies → copy to all RID directories
  - Trimmed framework assemblies → same (Mono.Android.dll is identical)

- [ ] **3.3** Inner builds skip ILLink, receive trimmed assemblies
  - `Condition=" '$(_ILLinkCompleted)' != 'true' "`
  - Only run per-RID AOT/native linking

### Phase 4: Move Java Compilation Earlier

- [ ] **4.1** Run Java compilation in outer build
  - After TypeMap generates .java files
  - Before per-RID inner builds

- [ ] **4.2** Skip Java compilation in inner builds
  - Pass compiled .dex to inner builds
  - Only native packaging per-RID

### Phase 5: Optimize Caching

- [ ] **5.1** Hash-based cache for TypeMap
  - Input: assembly content hashes + metadata
  - Output: TypeMap assembly, Java files, LLVM IR templates
  - Skip if cache hit

- [ ] **5.2** Separate stamp files
  - `_TypeMap.stamp` - outer build
  - `_ILLink.stamp` - outer build (CoreCLR only)
  - `_AOT.$(RID).stamp` - per-RID
  - `_Native.$(RID).stamp` - per-RID

- [ ] **5.3** Fine-grained invalidation
  - TypeMap change → invalidate ILLink, AOT, native
  - ILLink change → invalidate AOT, native
  - Native change → rebuild only affected .so

---

## Expected Performance Improvements

| Build Type | Current | Proposed | Improvement |
|------------|---------|----------|-------------|
| CoreCLR Release (4 RIDs) | TypeMap x4, ILLink x4 | TypeMap x1, ILLink x1 | **~75% reduction** |
| NativeAOT (2 RIDs) | TypeMap x2, ILLink x2, ILC x2 | TypeMap x1, ILC x2 | **~50% reduction** |
| Incremental (no changes) | Full rebuild | Cache hit | **~90% reduction** |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| ILC trimming differs from ILLink | High | Generate comprehensive root descriptors; test thoroughly |
| AssemblyModifierPipeline ordering | Medium | Explicit step ordering; test each step independently |
| Reference assembly limitations | Medium | Fall back to canonical RID resolution if needed |
| Cache invalidation bugs | Medium | Conservative invalidation; add diagnostic logging |
| NativeAOT without ILLink fails | High | Extensive testing; feature flag to re-enable ILLink |

---

## Success Metrics

1. **Build time**: 50-75% reduction for multi-RID Release builds
2. **NativeAOT**: Works correctly without ILLink
3. **Cache hits**: 90%+ for no-op incremental builds
4. **Correctness**: All existing tests pass

---

## Dependencies

- PR #10694 (GenerateProguardConfiguration migration)
- PR #10695 (StripEmbeddedLibraries migration)
- ILC compatibility with Android-specific trimming needs

---

## Open Questions

1. Can ILC consume TrimmerRootDescriptor XML format, or does it need ILC-specific format?
2. Are there any ILLink custom steps that MUST run for NativeAOT beyond IL modifications?
3. What's the minimal set of types ILC needs rooted for Java interop to work?

---

## Incremental Build Opportunities

### Current State Audit

#### Targets WITH Proper Inputs/Outputs (✅ Good Examples)

| Target | File | Pattern |
|--------|------|---------|
| `_AndroidAotCompilation` | Microsoft.Android.Sdk.Aot.targets:75-153 | Stamp file + WriteLinesToFile/ReadLinesFromFile |
| `_GenerateJavaStubs` | Xamarin.Android.Common.targets | Stamp file |
| `_CompileJava` | Xamarin.Android.Javac.targets:151-178 | Inputs from file list + stamp |
| `_ConvertCustomView` | Xamarin.Android.Common.targets:1718-1729 | Stamp file |
| `_GeneratePackageManagerJava` | Xamarin.Android.Common.targets:1879-1883 | Stamp file |
| `_CreateApplicationSharedLibraries` | Xamarin.Android.Common.targets:2215-2233 | Direct outputs |
| `_CompileNativeAssemblySources` | Xamarin.Android.Common.targets:2192-2205 | Comprehensive inputs |
| `_UpdateAndroidResgen` | Xamarin.Android.Common.targets:1259-1342 | Flag file |

#### Targets MISSING Inputs/Outputs (❌ Needs Improvement)

| Target | File | Issue | Fix |
|--------|------|-------|-----|
| `_GenerateTypeMapAssembly` | Microsoft.Android.Sdk.ILLink.targets:20-67 | No Inputs/Outputs | Add `Inputs="@(_TypeMapInputAssemblies)"` `Outputs="$(_TypeMapStamp)"` |
| `_PrepareLinking` | Microsoft.Android.Sdk.ILLink.targets:69-163 | No incrementality | Add stamp file after ILLink prep |
| `_RunAssemblyModifierPipeline` | Xamarin.Android.Common.targets | Missing stamp | Add `$(_AssemblyModifierPipelineStamp)` |
| `_AfterILLinkAdditionalSteps` | Xamarin.Android.Common.targets | Missing stamp | Track post-link modifications |

### Redundant Work Patterns Found

#### 1. ProcessRuntimePackLibraryDirectories Runs Twice ⚠️

**Location**: 
- `Microsoft.Android.Sdk.AssemblyResolution.targets:115-120` (outer build)
- `Microsoft.Android.Sdk.Aot.targets:85-90` (inner build)

**Issue**: Same task, same inputs, runs in BOTH outer and inner builds.

**Fix**: Cache result in outer build, pass via `_AdditionalProperties`:
```xml
;_RuntimePackLibraryDirectories=@(_RuntimePackLibraryDirectory)
```

#### 2. AlwaysCreate="True" Defeats Stamp Purpose ⚠️

**Found 20+ instances** where stamp files are created unconditionally:
```xml
<Touch Files="$(_AndroidStampDirectory)_Example.stamp" AlwaysCreate="True" />
```

**Issue**: `AlwaysCreate="True"` recreates stamp even when nothing changed, defeating incremental builds.

**Fix**: Most should be:
```xml
<Touch Files="$(_Stamp)" AlwaysCreate="True" />
```
This IS correct - the stamp indicates "this target ran", not "something changed". The Inputs/Outputs on the target control whether it runs.

**HOWEVER**: Some uses are incorrect - they touch stamps without proper Inputs/Outputs on the target.

#### 3. Inner Builds Repeat All Work

**Pattern**: Each RID inner build independently runs:
- `ProcessAssemblies` 
- `ProcessNativeLibraries`
- Type resolution
- ILLink (when enabled)

**Fix**: 
- Run RID-agnostic work in outer build
- Pass results to inner builds
- Inner builds only do per-RID native work

#### 4. No Cross-Build Caching

**Missing**: Hash-based caching that persists across builds.

**Example opportunity**: TypeMap generation
- Hash input assemblies' metadata
- If hash matches cached, skip regeneration
- Store: `$(IntermediateOutputPath)typemap-cache/$(hash)/`

### Proposed Incremental Build Improvements

#### Phase 6: Add Missing Inputs/Outputs

- [ ] **6.1** Add Inputs/Outputs to `_GenerateTypeMapAssembly`:
  ```xml
  <Target Name="_GenerateTypeMapAssembly"
      Inputs="@(_TypeMapInputAssemblies);$(MSBuildAllProjects)"
      Outputs="$(_TypeMapAssemblyOutputDir)_Microsoft.Android.TypeMaps.dll;$(_TypeMapStamp)">
  ```

- [ ] **6.2** Add stamp file for `_PrepareLinking`:
  ```xml
  <Target Name="_PrepareLinking"
      ...
      Outputs="$(_AndroidStampDirectory)_PrepareLinking.stamp">
    <!-- ... existing work ... -->
    <Touch Files="$(_AndroidStampDirectory)_PrepareLinking.stamp" AlwaysCreate="True" />
  </Target>
  ```

- [ ] **6.3** Add Inputs/Outputs to AssemblyModifierPipeline target:
  ```xml
  <Target Name="_RunAssemblyModifierPipeline"
      Inputs="@(_ShrunkAssemblies)"
      Outputs="$(_AssemblyModifierPipelineStamp)">
  ```

#### Phase 7: Eliminate Redundant Work

- [ ] **7.1** Cache ProcessRuntimePackLibraryDirectories in outer build
  - Store in property, pass to inner builds
  - Skip task in inner builds when property set

- [ ] **7.2** Move RID-agnostic processing to outer build
  - `ProcessAssemblies` user assembly processing
  - Java library resolution (`ProcessNativeLibraries` for JARs)

- [ ] **7.3** Create hash-based assembly cache
  - Hash assembly content + build config
  - Skip processing if hash matches previous run
  - Useful for: TypeMap, ILLink, AOT inputs

#### Phase 8: Parallelize Independent Work

- [ ] **8.1** Parallelize LLVM IR compilation
  - Each .ll file can compile to .o independently
  - Use `BuildInParallel="true"` for native compilation tasks

- [ ] **8.2** Overlap Java and native compilation
  - Java compilation is independent of native .so linking
  - Can run in parallel with per-RID native work

- [ ] **8.3** Batch assembly processing
  - `AssemblyModifierPipeline` already batches
  - Ensure other assembly operations batch properly

---

## Quick Wins Summary

| Optimization | Effort | Impact | Phase |
|--------------|--------|--------|-------|
| Add Inputs/Outputs to `_GenerateTypeMapAssembly` | Low | High | 6.1 |
| Cache ProcessRuntimePackLibraryDirectories | Low | Medium | 7.1 |
| Skip ILLink for NativeAOT | Medium | Very High | 1.1-1.4 |
| Move TypeMap to outer build | Medium | High | 2.1-2.4 |
| Run ILLink once (share output) | High | Very High | 3.1-3.3 |
| Hash-based assembly caching | High | Medium | 7.3 |

---

## Implementation Priority

### Immediate (This Sprint)
1. Complete PR #10694, #10695 (ILLink step migrations)
2. Add Inputs/Outputs to `_GenerateTypeMapAssembly`
3. Fix TypeMap timing for both CoreCLR and NativeAOT

### Short-term (Next Sprint)
4. Skip ILLink for NativeAOT (Phase 1)
5. Move TypeMap to outer build (Phase 2)
6. Cache ProcessRuntimePackLibraryDirectories (7.1)

### Medium-term (Following Sprints)
7. Run ILLink once, share output (Phase 3)
8. Move Java compilation earlier (Phase 4)
9. Hash-based caching (7.3)

### Long-term (Future)
10. Full incremental optimization (Phases 5-8)
11. Design-time build sharing
12. Cross-build caching infrastructure

---

## Additional Discoveries (Continued Analysis)

### 5. ILC Cannot Absorb TypeMap Work ❌

Investigated whether ILC could directly handle TypeMap-related work for NativeAOT builds.

**Findings:**
- **TypeMap generates JNI-specific LLVM IR** via `MarshalMethodsNativeAssemblyGenerator` - ILC can't do this
- **ILC lacks JNI calling convention awareness** - it's a managed-to-native compiler, not JNI-aware
- **No extension points** - ILC has no plugin model for custom metadata processing
- **Current design is cleaner** - `GenerateTypeMapAssembly` + attributes + ILC intrinsics works well

**Conclusion**: Keep TypeMap as separate MSBuild task; focus on running it ONCE in outer build.

### 6. Critical Race Condition in Per-RID Builds ⚠️

**Found**: All RID inner builds write to the SAME hash file:
```xml
<!-- Microsoft.Android.Sdk.AssemblyResolution.targets:173-184 -->
<PropertyGroup>
  <_ResolvedUserAssembliesHashFile>$(IntermediateOutputPath)resolvedassemblies.hash</_ResolvedUserAssembliesHashFile>
</PropertyGroup>
```

**Problem**: When `BuildInParallel="true"` is used, multiple RIDs race to read/write this file.

**Fix Options**:
1. Make hash file per-RID: `$(IntermediateOutputPath)$(RuntimeIdentifier)/resolvedassemblies.hash`
2. Move hash computation to outer build (preferred - hash is RID-invariant)
3. Add file locking (not recommended - slows builds)

### 7. Merging TypeMap + AssemblyModifierPipeline 🔧

**Observation**: Both systems scan assemblies for Java interop types:
- `GenerateTypeMapAssembly` scans for `[Register]` attributes
- `FindJavaObjectsStep` (in AssemblyModifierPipeline) scans for Java-backed types
- `FindTypeMapObjectsStep` scans for TypeMap entries

**Optimization Opportunity**:
- Single assembly scan with multiple outputs (TypeMap, JCWs, assembly modifications)
- Share Cecil/S.R.M assembly graph between steps
- Avoid loading assemblies multiple times

**Implementation**: Add TypeMap generation as an `IAssemblyModifierPipelineStep`:
```csharp
public class GenerateTypeMapStep : IAssemblyModifierPipelineStep
{
    // Runs during the same pipeline that does FindJavaObjectsStep
    // Shares loaded assembly context
}
```

### 8. LLVM IR Is Architecture-Specific (Analysis) 🔬

**Question**: Is the LLVM IR we generate RID-specific? Does it have to be?

**Answer**: **The IR BODY is architecture-neutral, only the HEADER is per-architecture!**

#### Analysis of Our Generated Marshal Method IR

Looking at the actual code we generate:

```llvm
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8
@fn_ptr_{N} = internal unnamed_addr global ptr null, align 8
@class_name_{TypeName} = internal constant [{len} x i8] c"{utf16_bytes}", align 2

define default void @Java_{pkg}_{Class}_{method}(ptr %env, ptr %obj, ...) #0 {
entry:
 %cached = load ptr, ptr @fn_ptr_{N}, align 8
 %is_null = icmp eq ptr %cached, null
 br i1 %is_null, label %resolve, label %call

resolve:
 %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
 call void %get_fn(ptr @class_name_{TypeName}, i32 {charCount}, i32 {methodIndex}, ptr @fn_ptr_{N})
 %resolved = load ptr, ptr @fn_ptr_{N}, align 8
 br label %call

call:
 %fn = phi ptr [ %cached, %entry ], [ %resolved, %resolve ]
 tail call void %fn(ptr %env, ptr %obj, ...)
 ret void
}

attributes #0 = { noinline nounwind "frame-pointer"="non-leaf" }
```

**This is almost entirely architecture-NEUTRAL because:**

| Element | Architecture-Dependent? | Notes |
|---------|------------------------|-------|
| `ptr` type | ❌ NO | Opaque pointer (LLVM 15+), size determined at compile time |
| `align 8` | ❌ NO | Works on all architectures (may be over-aligned on 32-bit) |
| `i32 {charCount}` | ❌ NO | Fixed-size integer, same on all archs |
| `load ptr, ptr` | ❌ NO | Generic pointer load |
| `icmp eq ptr` | ❌ NO | Pointer comparison |
| `phi ptr` | ❌ NO | SSA phi node, arch-neutral |
| `tail call void` | ❌ NO | Calling convention hint |
| `attributes #0` | ⚠️ MAYBE | `"frame-pointer"="non-leaf"` is generic |

#### What IS Architecture-Specific (Only the Header!)

```llvm
; ARM64
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

; x64  
target datalayout = "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128"
target triple = "x86_64-unknown-linux-android21"
```

#### RADICAL OPTIMIZATION: Generate Body Once, Prepend Headers! 🚀

**We could generate the IR body ONCE and create 4 files by prepending different headers:**

```csharp
// Generate body ONCE (in outer build)
string irBody = GenerateMarshalMethodsIrBody(javaTypes, marshalMethods);

// Create 4 architecture-specific files by prepending headers
var headers = new Dictionary<string, string> {
    ["arm64-v8a"] = @"target datalayout = ""e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128""
target triple = ""aarch64-unknown-linux-android21""",
    ["x86_64"] = @"target datalayout = ""e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128""
target triple = ""x86_64-unknown-linux-android21""",
    // ... arm, x86
};

foreach (var (abi, header) in headers) {
    File.WriteAllText($"marshal_methods_{abi}.ll", header + "\n\n" + irBody);
}
```

**Performance Impact:**
- **Before**: Scan assemblies 4x, generate IR 4x
- **After**: Scan assemblies 1x, generate IR body 1x, write 4 files with different headers

**Caveats to Check:**
1. ✅ `ptr` works on all architectures (opaque pointer)
2. ⚠️ `align 8` may need to be `align 4` on 32-bit for fn_ptr (or leave as 8, will over-align)
3. ⚠️ If any method params use signext/zeroext, those ARE per-arch
4. ⚠️ Check if attributes need arch-specific CPU features

**If the IR body is truly arch-neutral (as it appears), this is a MAJOR optimization!**

---

## Radical Optimization Ideas (Explored but Not Recommended)

### Idea A: Eliminate Nested MSBuild Entirely

**Concept**: Instead of per-RID inner builds, do all work in outer build with multi-output tasks.

**Why NOT Recommended**:
- ILC requires RID-specific runtime packs
- Native linker needs per-architecture toolchain
- MSBuild's multi-targeting infrastructure handles RID resolution well
- Risk of regression too high

**Alternative**: Minimize inner build scope to ONLY architecture-specific work.

### Idea B: Pre-compute TypeMap at SDK Pack Time

**Concept**: Generate TypeMap for Mono.Android.dll when building the SDK, ship pre-computed.

**Why NOT Recommended**:
- User assemblies aren't known at SDK time
- TypeMap must include user types with `[Register]`
- Would only cover framework types (partial win)

**Possible Future**: Pre-compute LLVM IR templates for framework types only.

### Idea C: Lazy TypeMap Generation

**Concept**: Generate TypeMap data on first use at runtime instead of build time.

**Why NOT Recommended**:
- JNI native methods must exist at load time
- LLVM IR for marshal methods needed before ILC/AOT
- Would break NativeAOT entirely
- Runtime overhead unacceptable for startup

---

## NEW: Assembly Deduplication Opportunities

### The 4x Copy Problem

Currently, **identical assemblies are processed 4x** (once per RID):

| Problem | Location | Fix |
|---------|----------|-----|
| `ProcessAssemblies` per-RID | AssemblyResolution.targets:140-162 | Add `->Distinct()` for framework assemblies |
| No deduplication on InputAssemblies | Line 143 vs 144 | Java libs use `->Distinct()`, assemblies don't |
| Assembly copy per-RID | Common.targets:2103-2128 | Copy once, symlink per-RID |
| ILLink per-RID | ILLink.targets | Run once, share output |

### Framework vs User Assemblies

**Framework assemblies** (Mono.Android.dll, System.*.dll) are **IDENTICAL** across all RIDs.

**User assemblies** are also identical (they're architecture-neutral IL).

**Only native libraries** (.so files) differ per-RID.

### Deduplication Strategy

```xml
<!-- In outer build, deduplicate assemblies -->
<ItemGroup>
  <_UniqueAssemblies Include="@(ReferencePath->Distinct())" />
</ItemGroup>

<!-- Process ONCE -->
<ProcessAssemblies InputAssemblies="@(_UniqueAssemblies)" ... />

<!-- In inner builds, just reference the already-processed output -->
```

---

## NEW: Design-Time Build Sharing

### Existing Caches (Can Reuse)

| Cache | What It Contains | Populated By |
|-------|------------------|--------------|
| `_AndroidBuildPropertiesCache` | 30+ build properties | `_CreatePropertiesCache` |
| `_AndroidResourcePathsDesignTimeCache` | Resource path enumeration | Design-time build |
| `_AndroidLibraryImportsDesignTimeCache` | AAR/Library resources | Design-time build |
| `_AndroidLayoutBindingsDependencyCache` | Layout binding deps | Design-time build |

### Cache Validation Pattern

```xml
<!-- Check if cache is still valid -->
<Target Name="_ValidateCache"
    Inputs="$(MSBuildProjectFile);@(ReferencePath)"
    Outputs="$(_CacheValidationStamp)">
  <!-- Recompute cache -->
</Target>

<!-- Use cached result if valid -->
<Target Name="_UseCache" 
    Condition=" Exists('$(_CacheValidationStamp)') ">
  <!-- Skip expensive work -->
</Target>
```

### Opportunity: Extend Caching to More Tasks

| Task | Can Cache? | Cache Key |
|------|------------|-----------|
| TypeMap generation | YES | Hash of input assemblies |
| ILLink trimming | YES | Hash of assemblies + trim config |
| Java compilation | YES | Hash of JCW sources |
| DEX compilation | YES | Hash of .class files |
| AOT compilation | Per-RID | Hash of trimmed assemblies + RID |

---

## NEW: Minimal Inner Build Scope

### Current: Everything in Inner Build

```
Inner Build (per-RID):
├── ResolveReferences (redundant)
├── ComputeFilesToPublish (triggers ILLink)
├── _GenerateTypeMapAssembly (redundant)
├── ILLink (redundant for CoreCLR)
├── MonoAOT / ILC (required)
└── Native linking (required)
```

### Proposed: Minimal Inner Build

```
Outer Build (once):
├── ResolveReferences
├── _GenerateTypeMapAssembly
├── ILLink (CoreCLR only)
├── AssemblyModifierPipeline
├── _CompileJava
└── _CompileDex

Inner Build (per-RID, minimal):
├── Copy trimmed assemblies from outer
├── MonoAOT / ILC (NativeAOT)
└── Native linking (.so creation)
```

### What MUST Stay Per-RID

1. **ILC compilation** - requires RID-specific runtime pack
2. **MonoAOT compilation** - architecture-specific machine code
3. **Native linking** - per-ABI toolchain (arm64-v8a, x86_64, etc.)
4. **Native library copying** - .so files are per-architecture

### What Can Move to Outer Build

1. **TypeMap generation** - RID-invariant, scans same assemblies
2. **ILLink trimming** - RID-invariant, produces identical output
3. **Assembly modification** - RID-invariant IL changes
4. **Java/DEX compilation** - already in outer build ✅
5. **ProcessRuntimePackLibraryDirectories** - same output per-RID

---

## CONCRETE IMPLEMENTATION: Move TypeMap to Outer Build

### Current Problem (Confirmed via Analysis)

**TypeMap runs 4x (once per RID)** because:
1. Outer build calls `_ResolveAssemblies` which spawns inner builds
2. Each inner build calls `_ComputeFilesToPublishForRuntimeIdentifiers` 
3. This triggers `ComputeFilesToPublish` → `_PrepareLinking` → `_GenerateTypeMapAssembly` + `_RunILLink`
4. TypeMap is RID-invariant but runs in EVERY inner build

### Detection: Outer vs Inner Build

| Condition | Build Type |
|-----------|------------|
| `RuntimeIdentifiers != '' AND RuntimeIdentifier == ''` | Outer build (multi-RID) |
| `RuntimeIdentifier != ''` | Inner build (per-RID) |
| `_ComputeFilesToPublishForRuntimeIdentifiers == 'true'` | Inner build |
| `RuntimeIdentifiers == '' AND RuntimeIdentifier != ''` | Single-RID build (no inner builds) |

### Step 1: Add Outer Build TypeMap Target

**File: `Microsoft.Android.Sdk.ILLink.targets`**

Add NEW target that runs in outer build, BEFORE `_ResolveAssemblies`:

```xml
<!-- NEW: Run TypeMap generation ONCE in outer build -->
<Target Name="_GenerateTypeMapAssemblyInOuterBuild"
    Condition=" '$(PublishTrimmed)' == 'true' AND '$(RuntimeIdentifiers)' != '' AND '$(RuntimeIdentifier)' == '' "
    DependsOnTargets="ResolveReferences"
    BeforeTargets="_ResolveAssemblies"
    Inputs="@(ReferencePath);$(MSBuildAllProjects)"
    Outputs="$(_TypeMapAssemblyOutputDir)_Microsoft.Android.TypeMaps.dll">
  
  <PropertyGroup>
    <_TypeMapAssemblyOutputDir>$(IntermediateOutputPath)typemap\</_TypeMapAssemblyOutputDir>
    <_TypeMapJavaSourceOutputDir>$(_AndroidIntermediateJavaSourceDirectory)</_TypeMapJavaSourceOutputDir>
    <_TypeMapLlvmIrOutputDir>$(IntermediateOutputPath)android\</_TypeMapLlvmIrOutputDir>
  </PropertyGroup>
  
  <ItemGroup>
    <_TypeMapInputAssemblies Include="@(ReferencePath)" />
  </ItemGroup>
  
  <GenerateTypeMapAssembly
      ResolvedAssemblies="@(_TypeMapInputAssemblies)"
      OutputDirectory="$(_TypeMapAssemblyOutputDir)"
      JavaSourceOutputDirectory="$(_TypeMapJavaSourceOutputDir)"
      LlvmIrOutputDirectory="$(_TypeMapLlvmIrOutputDir)"
      ErrorOnCustomJavaObject="$(AndroidErrorOnCustomJavaObject)">
    <Output TaskParameter="GeneratedAssembly" ItemName="_TypeMapGeneratedAssembly" />
    <Output TaskParameter="TypeMapEntryAssemblyName" PropertyName="_TypeMapEntryAssemblyName" />
    <Output TaskParameter="UpdatedResolvedAssemblies" ItemName="_TypeMapUpdatedAssemblies" />
    <Output TaskParameter="GeneratedJavaFiles" ItemName="_GeneratedJavaFiles" />
  </GenerateTypeMapAssembly>
  
  <PropertyGroup>
    <TypeMapEntryAssembly Condition=" '$(_TypeMapEntryAssemblyName)' != '' ">$(_TypeMapEntryAssemblyName)</TypeMapEntryAssembly>
    <_TypeMapGeneratedInOuterBuild>true</_TypeMapGeneratedInOuterBuild>
  </PropertyGroup>
  
  <Message Importance="high" Text="[OUTER] Generated TypeMap assembly: @(_TypeMapGeneratedAssembly)" />
</Target>
```

### Step 2: Pass TypeMap Path to Inner Builds

**File: `Microsoft.Android.Sdk.AssemblyResolution.targets`**

Modify `_ResolveAssemblies` to pass TypeMap path to inner builds:

```xml
<PropertyGroup>
  <_AdditionalProperties>
    _ComputeFilesToPublishForRuntimeIdentifiers=true
    ;SelfContained=true
    ;DesignTimeBuild=$(DesignTimeBuild)
    ;AppendRuntimeIdentifierToOutputPath=true
    ;ResolveAssemblyReferencesFindRelatedSatellites=false
    ;SkipCompilerExecution=true
    ;_OuterIntermediateAssembly=@(IntermediateAssembly)
    ;_OuterIntermediateSatelliteAssembliesWithTargetPath=@(IntermediateSatelliteAssembliesWithTargetPath)
    ;_OuterOutputPath=$(OutputPath)
    ;_OuterIntermediateOutputPath=$(IntermediateOutputPath)
    ;_OuterCustomViewMapFile=$(_CustomViewMapFile)
    ;_AndroidNdkDirectory=$(_AndroidNdkDirectory)
    <!-- NEW: Pass TypeMap from outer build -->
    ;_TypeMapGeneratedInOuterBuild=$(_TypeMapGeneratedInOuterBuild)
    ;_OuterTypeMapAssemblyPath=$(_TypeMapAssemblyOutputDir)_Microsoft.Android.TypeMaps.dll
    ;_OuterTypeMapEntryAssembly=$(TypeMapEntryAssembly)
  </_AdditionalProperties>
</PropertyGroup>
```

### Step 3: Skip TypeMap in Inner Builds

**File: `Microsoft.Android.Sdk.ILLink.targets`**

Modify existing `_GenerateTypeMapAssembly` target to SKIP if already generated:

```xml
<Target Name="_GenerateTypeMapAssembly"
    Condition=" '$(PublishTrimmed)' == 'true' AND '$(_TypeMapGeneratedInOuterBuild)' != 'true' "
    DependsOnTargets="_ComputeManagedAssemblyToLink"
    BeforeTargets="_RunILLink">
  <!-- existing implementation - now only runs for single-RID builds -->
</Target>

<!-- NEW: Use TypeMap from outer build in inner builds -->
<Target Name="_UseTypeMapFromOuterBuild"
    Condition=" '$(_TypeMapGeneratedInOuterBuild)' == 'true' "
    BeforeTargets="_RunILLink">
  
  <PropertyGroup>
    <TypeMapEntryAssembly>$(_OuterTypeMapEntryAssembly)</TypeMapEntryAssembly>
  </PropertyGroup>
  
  <ItemGroup>
    <ManagedAssemblyToLink Include="$(_OuterTypeMapAssemblyPath)" />
  </ItemGroup>
  
  <Message Importance="high" Text="[INNER] Using TypeMap from outer build: $(_OuterTypeMapAssemblyPath)" />
</Target>
```

### Step 4: Handle Single-RID Builds

For builds with only one RID (no inner builds), the original `_GenerateTypeMapAssembly` runs.

**Detection condition for original target:**
```xml
Condition=" '$(PublishTrimmed)' == 'true' AND '$(_TypeMapGeneratedInOuterBuild)' != 'true' "
```

This allows:
- Multi-RID: Outer build generates, inner builds consume
- Single-RID: Original target generates (no outer/inner split)

---

## CONCRETE IMPLEMENTATION: ILLink Optimization

### Option A: Skip ILLink for NativeAOT (Simpler)

**Prerequisite**: All IL-modifying ILLink steps must be migrated to `AssemblyModifierPipeline`.

**File: `Microsoft.Android.Sdk.NativeAOT.targets`**

```xml
<!-- Remove ILLink from IlcCompileDependsOn -->
<PropertyGroup>
  <IlcCompileDependsOn>
    _AndroidBeforeIlcCompile;
    SetupOSSpecificProps;
    PrepareForILLink;
    <!-- ILLink;  REMOVED - ILC does its own trimming -->
    ComputeIlcCompileInputs;
    _AndroidComputeIlcCompileInputs;
    $(IlcCompileDependsOn)
  </IlcCompileDependsOn>
</PropertyGroup>
```

**File: `Microsoft.Android.Sdk.ILLink.targets`**

```xml
<!-- Skip ILLink for NativeAOT -->
<Target Name="_RunILLink"
    Condition=" '$(PublishTrimmed)' == 'true' AND '$(_AndroidRuntime)' != 'NativeAOT' ">
  <!-- existing ILLink invocation -->
</Target>

<!-- Run AssemblyModifierPipeline ALWAYS (after ILLink for CoreCLR, standalone for NativeAOT) -->
<Target Name="_RunAssemblyModifierPipeline"
    AfterTargets="_RunILLink"
    Condition=" '@(_AssemblyModifierPipelineSteps->Count())' &gt; '0' ">
  
  <!-- Choose input assemblies based on whether ILLink ran -->
  <ItemGroup Condition=" '$(_AndroidRuntime)' == 'NativeAOT' ">
    <_ModifierPipelineInputs Include="@(ReferencePath)" />
  </ItemGroup>
  <ItemGroup Condition=" '$(_AndroidRuntime)' != 'NativeAOT' ">
    <_ModifierPipelineInputs Include="@(_ShrunkAssemblies)" />
  </ItemGroup>
  
  <AssemblyModifierPipeline
      Assemblies="@(_ModifierPipelineInputs)"
      Steps="@(_AssemblyModifierPipelineSteps)"
      ... />
</Target>
```

### Option B: Run ILLink Once in Outer Build (More Complex, Better)

**Rationale**: ILLink output is RID-invariant. Trimmed assemblies are the same for all Android RIDs.

**File: `Microsoft.Android.Sdk.ILLink.targets`**

```xml
<!-- NEW: Run ILLink ONCE in outer build (CoreCLR only) -->
<Target Name="_RunILLinkInOuterBuild"
    Condition=" '$(PublishTrimmed)' == 'true' AND 
                '$(_AndroidRuntime)' != 'NativeAOT' AND
                '$(RuntimeIdentifiers)' != '' AND 
                '$(RuntimeIdentifier)' == '' "
    DependsOnTargets="_GenerateTypeMapAssemblyInOuterBuild;PrepareForILLink"
    AfterTargets="_GenerateTypeMapAssemblyInOuterBuild"
    BeforeTargets="_ResolveAssemblies">
  
  <!-- Run ILLink using SDK's standard mechanism -->
  <ILLink
      AssemblyPaths="@(ManagedAssemblyToLink)"
      ... />
  
  <PropertyGroup>
    <_ILLinkRanInOuterBuild>true</_ILLinkRanInOuterBuild>
    <_OuterLinkedAssembliesPath>$(IntermediateOutputPath)linked\</_OuterLinkedAssembliesPath>
  </PropertyGroup>
  
  <ItemGroup>
    <_OuterLinkedAssemblies Include="$(_OuterLinkedAssembliesPath)*.dll" />
  </ItemGroup>
</Target>
```

**File: `Microsoft.Android.Sdk.AssemblyResolution.targets`**

Pass linked assemblies to inner builds:

```xml
<PropertyGroup>
  <_AdditionalProperties>
    ...existing properties...
    <!-- Pass ILLink results from outer build -->
    ;_ILLinkRanInOuterBuild=$(_ILLinkRanInOuterBuild)
    ;_OuterLinkedAssembliesPath=$(_OuterLinkedAssembliesPath)
  </_AdditionalProperties>
</PropertyGroup>
```

**File: `Microsoft.Android.Sdk.ILLink.targets`**

Skip ILLink in inner builds if already ran:

```xml
<Target Name="_RunILLink"
    Condition=" '$(PublishTrimmed)' == 'true' AND 
                '$(_AndroidRuntime)' != 'NativeAOT' AND
                '$(_ILLinkRanInOuterBuild)' != 'true' ">
  <!-- existing ILLink - only for single-RID builds -->
</Target>

<Target Name="_UseLinkedAssembliesFromOuterBuild"
    Condition=" '$(_ILLinkRanInOuterBuild)' == 'true' "
    BeforeTargets="ComputeFilesToPublish">
  
  <ItemGroup>
    <ResolvedFileToPublish Remove="@(ResolvedFileToPublish)" 
        Condition=" '%(Extension)' == '.dll' " />
    <ResolvedFileToPublish Include="$(_OuterLinkedAssembliesPath)*.dll" />
  </ItemGroup>
</Target>
```

---

## Expected Performance Gains

| Scenario | Before | After | Improvement |
|----------|--------|-------|-------------|
| CoreCLR Release (4 RIDs) | TypeMap x4 + ILLink x4 | TypeMap x1 + ILLink x1 | **~75%** |
| NativeAOT (2 RIDs) | TypeMap x2 + ILLink x2 | TypeMap x1 + ILLink x0 | **~60%** |
| Incremental (no changes) | Full rebuild | Skip (Inputs/Outputs) | **~95%** |

---

## Implementation Phases (Updated)

### Phase 0: Prerequisites (In Progress)
- [ ] Complete PR #10694 (GenerateProguardConfiguration migration)
- [ ] Complete PR #10695 (StripEmbeddedLibraries migration)
- [ ] Migrate remaining IL-modifying steps to AssemblyModifierPipeline

### Phase 1: Move TypeMap to Outer Build (Next)
- [ ] Add `_GenerateTypeMapAssemblyInOuterBuild` target
- [ ] Modify `_ResolveAssemblies` to pass TypeMap path to inner builds
- [ ] Add `_UseTypeMapFromOuterBuild` target for inner builds
- [ ] Update original `_GenerateTypeMapAssembly` condition
- [ ] Test: Multi-RID CoreCLR Release build
- [ ] Test: Multi-RID NativeAOT build
- [ ] Test: Single-RID builds still work

### Phase 2: Skip ILLink for NativeAOT
- [ ] Remove ILLink from `IlcCompileDependsOn`
- [ ] Ensure AssemblyModifierPipeline runs for NativeAOT
- [ ] Test: NativeAOT builds skip ILLink
- [ ] Test: CoreCLR builds still use ILLink

### Phase 3: Move ILLink to Outer Build (CoreCLR)
- [ ] Add `_RunILLinkInOuterBuild` target
- [ ] Pass linked assemblies to inner builds
- [ ] Add `_UseLinkedAssembliesFromOuterBuild` target
- [ ] Test: Multi-RID CoreCLR Release runs ILLink once

### Phase 4: Incremental Build Support
- [ ] Add Inputs/Outputs to `_GenerateTypeMapAssembly`
- [ ] Add Inputs/Outputs to `_RunILLinkInOuterBuild`
- [ ] Fix hash file race condition (per-RID or outer build)
- [ ] Test: Second build skips completed work

---

## Summary: What To Do Next

### Immediate Actions (This Week)

1. **Fix the timing bug** - Ensure `_GenerateTypeMapAssembly` runs at the right time for BOTH CoreCLR and NativeAOT
   - Currently may run too late for NativeAOT (after ILC starts)
   - Add explicit `BeforeTargets="IlcCompile"` for NativeAOT path

2. **Add Inputs/Outputs** - Make TypeMap target incremental:
   ```xml
   Inputs="@(_TypeMapInputAssemblies);$(MSBuildAllProjects)"
   Outputs="$(_TypeMapStamp)"
   ```

3. **Fix race condition** - Make hash file per-RID or move to outer build

### Short-term Actions (Next 2 Weeks)

4. **Complete ILLink step migrations** (PRs #10694, #10695)

5. **Move TypeMap to outer build** - Run once, pass to inner builds

6. **Skip ILLink for NativeAOT** - After all IL-modifying steps are migrated

### Validation

After each change:
- [ ] CoreCLR Debug build works
- [ ] CoreCLR Release build works (with ILLink)
- [ ] NativeAOT build works
- [ ] Multi-RID build works (all 4 Android RIDs)
- [ ] Incremental build works (second build should be faster)
- [ ] TypeMap-generated types callable from Java
