---
name: perf-measurement
description: Plans and runs dotnet/android performance comparisons (startup, runtime, APK size, build time). Use before benchmarking a change, running a before/after matrix on a device, or claiming a performance win or regression.
---

# Performance measurement

Most wasted measurement time came from comparing the wrong things. Prove the comparison is valid before running a full matrix.

## Preflight (before the full run)

- [ ] **The change is in the measured artifact.** Inspect the built APK/AAB (`unzip -l`, assembly/native library contents, `_AndroidRuntime`, Release/`Optimize`). After installing, confirm the device has that APK; `dotnet test --no-build` and `-t:Install` can redeploy a different build.
- [ ] **The measured code runs.** Show the changed path executes in the variant (a log line, trace event, or counter), not only that the build succeeded.
- [ ] **One variable.** Same app, workload, configuration, runtime, device, and SDK on both sides; only the change differs. Don't mix managed, native, and Java outputs from different revisions.
- [ ] **Representative app.** Use the trimmed Release app the claim is about; a Debug or untrimmed sample doesn't support Release conclusions.
- [ ] **Pilot.** Run a few iterations of both variants and check they differ before starting a long matrix.
- [ ] **Owned device and durable output.** Use a device you own (see the `android-devices` skill). Write raw results under the session `files/` directory, not `/tmp`.

## Methods

- Startup: `Displayed` time from `logcat` ([profiling guide](../../../Documentation/guides/profiling.md#overall-startup)).
- Runtime/GC/JNI: EventPipe via `dotnet-trace` ([tracing guide](../../../Documentation/guides/tracing.md)).
- Build time: [MSBuild profiling](../../../Documentation/guides/profiling.md#msbuild-profiling).

## Reporting

Give before/after with iteration count, median and spread, device, SDK commit, configuration, and method. If the difference is within noise, say so. Name configurations that weren't measured instead of generalizing to them.
