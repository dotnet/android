# Trimmable typemap build pipeline

This document describes how the **trimmable** typemap implementation
(`_AndroidTypeMapImplementation=trimmable`) is produced during an Android app
build, and how the MSBuild targets are kept incremental. It is aimed at
contributors working on the targets in
`src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable*.targets`
and the `GenerateTrimmableTypeMap` MSBuild task.

## Background

The legacy typemap implementations (`llvm-ir`, `managed`) embed the
managed&nbsp;↔&nbsp;Java type mapping into native binaries. The **trimmable**
implementation instead generates a set of small managed *TypeMap assemblies*
(one per input assembly, plus a `_Microsoft.Android.TypeMaps` root) and the Java
Callable Wrapper (JCW) `*.java` sources from the same scan. This keeps the
mapping trimmer-friendly: unused entries are removed by the IL linker.

The work happens in the `GenerateTrimmableTypeMap` task, invoked from the
`_GenerateTrimmableTypeMap` target. The runtime is selected by `_AndroidRuntime`
(`CoreCLR` or `NativeAOT`); the runtime-specific imports
(`*.Trimmable.CoreCLR.targets`, `*.Trimmable.NativeAOT.targets`) extend the
shared pipeline.

## Target pipeline (CoreCLR, non-trimmed Debug build)

```
CoreCompile
   └─► _GenerateTrimmableTypeMap   (AfterTargets="CoreCompile")
          • scans @(ReferencePath) + framework/SDK assemblies + the app .dll
          • writes typemap/_*.TypeMap.dll + _Microsoft.Android.TypeMaps.dll
          • writes typemap/java/**/*.java (JCWs) and acw-map.txt
          • writes the merged AndroidManifest.xml
          • touches typemap/_GenerateTrimmableTypeMap.stamp
   ...
_ReadGeneratedTrimmableTypeMapAssemblies    (reads typemap-assemblies.txt)
_PrepareTrimmableNativeConfigAssemblies     (feeds _GeneratePackageManagerJava)
_PrepareTrimmableTypeMapAssemblies          (feeds packaging / assembly store)
_GenerateJavaStubs                          (prepares manifest and native config)
   └─► _CompileJava ─► _CompileToDalvik ─► packaging
```

`_GenerateJavaStubs` **overrides** the legacy target of the same name from
`BuildOrder.targets`; in the trimmable path the JCWs already exist and are
compiled in place from the generator output directory. This target wires up
the manifest and native config.

For `CoreCLR` + `PublishTrimmed=true`, a second pass
(`_GeneratePostTrimTrimmableTypeMapJavaSources`, in the CoreCLR targets)
regenerates the JCWs from the **linked** assemblies into a `linked-java`
directory, which then becomes the source for `_GenerateJavaStubs`.

## Incrementality design

The pipeline follows the repository's
[MSBuild best practices](../../Documentation/guides/MSBuildBestPractices.md):
every expensive target declares `Inputs`/`Outputs`, re-emits its dynamic
`FileWrites`, and uses stamp files where a real output cannot serve as a
reliable timestamp sentinel.

### 1. A stamp file is the generator's incremental sentinel

`_GenerateTrimmableTypeMap` declares:

```xml
Inputs="@(ReferencePath);@(PrivateSdkAssemblies);@(FrameworkAssemblies);...;$(_AndroidManifestAbs);$(_AndroidBuildPropertiesCache)"
Outputs="$(_TrimmableTypeMapOutputStamp)"
```

The generated TypeMap DLLs are written with `Files.CopyIfStreamChanged`, so an
assembly whose **content** is unchanged keeps its old timestamp. If those DLLs
were the only `Outputs`, MSBuild would consider the target perpetually
out-of-date (its inputs are always newer than the untouched outputs) and re-run
it on every build. To avoid this, the target unconditionally `Touch`es a
dedicated stamp:

```xml
<Touch Files="$(_TrimmableTypeMapOutputStamp)" AlwaysCreate="true" />
```

so the stamp is always newer than the inputs after a run, and the target is
correctly **skipped** when none of the inputs changed.

> All assemblies that can contribute managed&nbsp;↔&nbsp;Java mappings must be
> inputs — including `@(PrivateSdkAssemblies)` and `@(FrameworkAssemblies)` —
> otherwise a change in one of them would not trigger regeneration.

### 2. `_GenerateJavaStubs` keys off both producer stamps

```xml
Inputs="$(_TrimmableTypeMapOutputStamp);$(_TrimmableJavaSourceStamp);@(_EnvironmentFiles)"
Outputs="$(_AndroidStampDirectory)_GenerateJavaStubs.stamp"
```

The pre-trim stamp covers the generated manifest and other shared outputs. For
`CoreCLR` + `PublishTrimmed`, `_TrimmableJavaSourceStamp` is the post-trim stamp
and covers the linked JCW set. Both remain stable when their producer is
skipped, so a no-op build still skips `_GenerateJavaStubs` while a manifest-only
change is not lost just because the linked assemblies stayed unchanged.

The pre-trim CoreCLR pass deliberately does not write the final `acw-map.txt`
or `ApplicationRegistration.java` in trimmed builds. Those files are owned by
the post-trim pass; sharing their paths allowed a manifest-only pre-trim run to
overwrite the linked versions while the post-trim target remained up-to-date.

### 3. Stale generated Java sources are pruned (both passes)

When a managed type is removed — or trimmed away on the `PublishTrimmed` path —
its JCW must not linger in the generator directory, where it would otherwise
be compiled and packaged. Both generator passes report the JCWs they no longer
produce as `DeletedJavaFiles` (with `RelativePath` metadata). If anything was
deleted, the owning target deletes `$(_AndroidCompileJavaStampFile)` so
`_CompileJava` re-runs and drops the stale `.class` outputs:

```xml
<Delete Files="$(_AndroidCompileJavaStampFile)" Condition=" '@(_DeletedJavaFiles->Count())' != '0' " />
```

Both passes update their output directories in place:

- **Pre-trim** (`_GenerateTrimmableTypeMap`, writing `typemap/java`): the task
  scans the output directory and deletes any `*.java` the current pass did not
  produce.
- **Post-trim** (`_GeneratePostTrimTrimmableTypeMapJavaSources`, writing
  `typemap/linked-java`): the task updates files by content and prunes stale
  `*.java` files in place. Unchanged JCWs keep their timestamps, so a managed
  method-body change does not force `javac` and D8 to rebuild identical Java
  output.

The invariant is two-directional: the active generator directory contains
exactly the JCWs that should be compiled — no missing files and no stale files.
The post-trim pass persists its expected Java paths in
`typemap/linked-java-files.txt`. Before using the post-trim stamp, an always-run
validation target compares that list with the files on disk and invalidates the
stamp if a source is missing or unexpected, or if the ACW map or application
registration source is missing. If generation logs an error, stale pruning is
suppressed so a partial result cannot delete the last known-good output set.

### 4. Dynamic `FileWrites` are re-emitted on no-op builds

The set of generated assemblies and JCWs is data-dependent, so a build that
*skips* `_GenerateTrimmableTypeMap` never executes the `ItemGroup` that registers
those files in `@(FileWrites)`. `_RecordTrimmableTypeMapFileWrites` re-reads the
generated outputs from `typemap-assemblies.txt` (and globs the JCWs) and
re-emits them — plus the stamp — into `@(FileWrites)` *before* MSBuild's
`IncrementalClean`, so the outputs are not seen as orphaned and deleted between
incremental builds.

### 5. The generator does not run in design-time builds, and runs once

`_GenerateTrimmableTypeMap` is gated on `'$(DesignTimeBuild)' != 'true'`: in a
design-time build, project references may resolve to target paths that are not
produced when `SkipCompilerExecution=true`, and the generator output is not
needed to provide IDE information. Combined with the
`'$(_OuterIntermediateOutputPath)' == ''` guard (which skips inner per-RID
builds), the generator runs exactly once per outer build.

## Files

| File | Role |
| ---- | ---- |
| `Microsoft.Android.Sdk.TypeMap.Trimmable.targets` | Shared pipeline: generation, Java stubs, packaging hookup, incremental `FileWrites`. |
| `Microsoft.Android.Sdk.TypeMap.Trimmable.CoreCLR.targets` | CoreCLR specifics, incl. the post-trim `linked-java` regeneration. |
| `Microsoft.Android.Sdk.TypeMap.Trimmable.NativeAOT.targets` | NativeAOT specifics (ILC inputs, proguard). |
| `Tasks/GenerateTrimmableTypeMap.cs` | The MSBuild task front-end for the generator. |
| `Microsoft.Android.Sdk.TrimmableTypeMap/**` | The generator/scanner library invoked by the task. |
