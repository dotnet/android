---
name: tests
description: >-
  Runs, discovers, or recommends dotnet/android tests. Handles standalone tests
  (dotnetup dotnet test), full-build tests (local Android workload required), and on-device
  MTP/NUnit tests. Maps source files to relevant test suites. Trigger on test
  execution requests, questions about available tests, or "which tests should I
  run for this change?"
---

# Tests

Consult `references/test-catalog.md` for the full mapping of test areas → assemblies, filters, and commands.

## Test categories

- **Standalone** — `dotnetup dotnet test <project>.csproj`. Only the pinned shared SDK is needed, not a local Android build.
- **Full-build** — requires worktree-local Android packs and manifests. Use `./dotnet-local.sh` (macOS/Linux) or `dotnet-local.cmd` (Windows) with the shared SDK.

Install the pinned SDK with `dotnetup sdk install` from the repository root.
For full-build tests, build `Microsoft.Android.slnx` with `dotnetup dotnet build`
first; rebuild after source changes. The wrappers report missing local workloads.
They default to Debug; export `CONFIGURATION=Release` for Release workloads and
also pass `-c Release` to build/test commands. Normal `global.json` lookup applies.
Forward SDK help with `dotnetup dotnet -- test --help`.

Validate locally before pushing: build missing prerequisites, run focused tests,
then expand coverage as needed. Do not skip required builds/device tests because
of presumed duration or use CI as a substitute. Report exact commands and outcomes,
including concrete blockers and remaining unvalidated coverage.

## Workflow

Classify the user's request:

**A) Run tests** — identify the test area/name, look up the command in `references/test-catalog.md`, build if needed, execute.

**B) Discover tests** — load `references/test-catalog.md` and summarize. Don't run anything.

**C) Recommend tests for a change** — identify changed files (from user description, `git diff`, or class name search), then map to tests using the source-to-test table below and `grep` for additional coverage.

### Source-to-test mapping

| Source path | Relevant tests |
|---|---|
| `src/Mono.Android/Xamarin.Android.Net/` | On-device: networking tests in `Mono.Android.NET-Tests.csproj`. Host: `CheckClientHandlerTypeTests`, `LinkerTests` |
| `src/Mono.Android/Android.*/**` | On-device: corresponding namespace tests in `Mono.Android.NET-Tests.csproj` |
| `src/Xamarin.Android.Build.Tasks/Tasks/` | Host: task tests in `Xamarin.Android.Build.Tests.dll` (filter by task name) |
| `src/Microsoft.Android.Build.Tasks/Tasks/` | Standalone: `Microsoft.Android.Build.Tasks.Tests.csproj` |
| `src/Xamarin.Android.Build.Tasks/Utilities/` | Host: `Xamarin.Android.Build.Tests.dll` — grep for test classes referencing the utility |
| `src/Xamarin.Android.Build.Tasks/**/*.targets` | Host: `Xamarin.Android.Build.Tests.dll`. Device: `MSBuildDeviceIntegration.dll` |
| `src/Microsoft.Android.Sdk.TrimmableTypeMap/` | On-device: `Mono.Android.NET-Tests.csproj`. Host: `GenerateTrimmableTypeMapTests`, `TrimmableTypeMapIncrementalTests`, `TrimmableTypeMapManifestAliasTests`, `TrimmableTypeMapRidCallbackTests`, `ExtractTypeMapKeysFromNativeAotObjectTests`, `TrimmableTypeMapBuildTests` |
| `src/Java.Interop/` | On-device: core JNI tests in `tests/Mono.Android-Tests/Java.Interop-Tests/`, run with `Mono.Android.NET-Tests.csproj` |
| `src/Java.Interop.Tools.*/`, `src/generator/`, `src/class-parse/` | Standalone tooling tests under `tests/` |
| `src/native/` | On-device runtime tests in `Mono.Android.NET-Tests.csproj` |

Present results as: **Must run** → **Should run** → **Consider running**, with exact commands.

## Running tests

The `${TFM}` placeholder = `DotNetStableTargetFramework` from `Directory.Build.props` (currently `net11.0`).

### Standalone
```bash
dotnetup dotnet test <project>.csproj -v minimal
dotnetup dotnet test <project>.csproj -v minimal --filter "Name~TestName"
```

### Host-side MSBuild tests (full-build)
```bash
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "Name~BuildBasicApplication"
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "FullyQualifiedName~NativeAotBuildTests"
```

### Device integration tests (full-build + device)
```bash
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll --filter "Name~InstallAndRunTests"
```

### On-device runtime tests (stock NUnit/MTP, full-build + device)

Build and install the APK first, then run `dotnet-local test` from the test project directory so the project-local `global.json` enables Microsoft Testing Platform:
```bash
./dotnet-local.sh build -t:Install -c Debug tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj
(
  cd tests/Mono.Android-Tests/Mono.Android-Tests
  ../../../dotnet-local.sh test Mono.Android.NET-Tests.csproj --no-build -c Debug --report-trx --results-directory ../../../bin/TestDebug/TestResults
)
```
Results are `.trx` files under `bin/TestDebug/TestResults/`. Repeat any `-c` and `-p:` arguments on both the build/install and `dotnet-local test --no-build` commands.

### Java.Interop tests
Tooling tests are standalone (`dotnetup dotnet test` on `.csproj`). Core JNI interop tests
live in `tests/Mono.Android-Tests/Java.Interop-Tests/` and run on Android as part
of `Mono.Android.NET-Tests.csproj`, using the on-device commands above.

To build the generator tooling and its tests using the main solution's filter:
```bash
dotnetup dotnet build src/generator/generator.slnf -c Debug
```
