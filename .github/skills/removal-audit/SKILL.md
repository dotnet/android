---
name: removal-audit
description: Finds all consumers before deleting, moving, or retiring dotnet/android code, MSBuild targets or properties, native outputs, files, or public APIs. Use for dead-code cleanup, "is this still used?" questions, and removal PRs.
---

# Removal audit

C# references alone miss many consumers here. Removals that skipped these checks broke packaging, CI, or generated code after merge.

## Search every consumer type

Run `git grep -n '<name>'` (and the file or output name) across the whole repository, then check:

- **Templates:** `*.in` files, such as `src/Mono.Android/Properties/AssemblyInfo.cs.in`.
- **Generated code and its inputs:** `src/Mono.Android/metadata`, `build-tools/jnienv-gen`, generator output, and `[Register]`/JNI names called from Java.
- **Shipped MSBuild:** `src/Xamarin.Android.Build.Tasks/**/*.targets`, including runtime-conditioned paths (`$(_AndroidRuntime)` for MonoVM, CoreCLR, NativeAOT).
- **Native code:** exported symbols, `DllImport` entry points, and `src/native` CMake/target inputs.
- **Packaging:** `build-tools/installers/create-installers.targets` and `src/workloads`.
- **CI:** `build-tools/automation/**/*.yaml` caches, artifact paths, and test lists.
- **Tests:** test projects, fixtures, and `Xamarin.ProjectTools`.

## Judge liveness

- Build wiring that still exists doesn't prove a runtime consumer; trace the value to code that reads it.
- No C# reference doesn't prove dead code; Java, native, generated, and reflection callers count.
- When moving build logic, keep its side effects (`FileWrites`, copies to output, packaging).
- Retired `XA####` codes: follow the error-code lifecycle in `.github/copilot-instructions.md`.

## Report

Put the evidence of no consumers (searches run and results) in the PR description, and validate the consumers you found, such as packaging or the affected runtime.
