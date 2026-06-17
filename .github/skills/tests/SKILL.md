---
name: tests
description: >-
  Runs, discovers, or recommends dotnet/android tests. Handles standalone tests
  (plain dotnet test), full-build tests (local SDK required), and on-device
  MTP/NUnit tests. Maps source files to relevant test suites. Trigger on test
  execution requests, questions about available tests, or "which tests should I
  run for this change?"
---

# Tests

Consult `references/test-catalog.md` for the full mapping of test areas → assemblies, filters, and commands.

## Test categories

- **Standalone** — `dotnet test <project>.csproj`. No local SDK needed.
- **Full-build** — requires the local SDK at `bin/Debug/dotnet/dotnet` (or Release). Use `./dotnet-local.sh` (macOS/Linux) or `dotnet-local.cmd` (Windows).

Check for the local SDK:
```bash
ls bin/Debug/dotnet/dotnet 2>/dev/null || ls bin/Release/dotnet/dotnet 2>/dev/null
```

**If the local SDK is missing and full-build tests are requested, use `ask_user` before building.** Never silently skip tests. Present the choice: build with `make prepare && make all` (slow) or skip full-build tests.

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
| `src/Xamarin.Android.Build.Tasks/Utilities/` | Host: `Xamarin.Android.Build.Tests.dll` — grep for test classes referencing the utility |
| `src/Xamarin.Android.Build.Tasks/**/*.targets` | Host: `Xamarin.Android.Build.Tests.dll`. Device: `MSBuildDeviceIntegration.dll` |
| `src/Microsoft.Android.Sdk.TrimmableTypeMap/` | Standalone: `TrimmableTypeMap.Tests.csproj`. Full-build: `TrimmableTypeMap.IntegrationTests` |
| `external/Java.Interop/src/` | Tests under `external/Java.Interop/tests/` |
| `src/native/` | On-device runtime tests in `Mono.Android.NET-Tests.csproj` |

Present results as: **Must run** → **Should run** → **Consider running**, with exact commands.

## Running tests

The `${TFM}` placeholder = `DotNetStableTargetFramework` from `Directory.Build.props` (currently `net10.0`).

### Standalone
```bash
dotnet test <project>.csproj -v minimal
dotnet test <project>.csproj -v minimal --filter "Name~TestName"
```

### Host-side MSBuild tests (full-build)
```bash
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "Name~BuildBasicApplication"
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "FullyQualifiedName~AotTests"
```

### Device integration tests (full-build + device)
```bash
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll --filter "Name~InstallAndRunTests"
```

### On-device runtime tests (stock NUnit/MTP, full-build + device)

Build and install the APK first, then run `dotnet test` from the test project directory so the project-local `global.json` enables Microsoft Testing Platform:
```bash
./dotnet-local.sh build -t:Install -c Debug tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj
(
  cd tests/Mono.Android-Tests/Mono.Android-Tests
  ../../../dotnet-local.sh test Mono.Android.NET-Tests.csproj --no-build -c Debug --report-trx --results-directory ../../../bin/TestDebug/TestResults
)
```
Results are `.trx` files under `bin/TestDebug/TestResults/`. Repeat any `-c` and `-p:` arguments on both the build/install and `dotnet test --no-build` commands.

### Java.Interop tests
Tooling tests are standalone (`dotnet test` on `.csproj`). JVM tests require the local SDK:
```bash
./dotnet-local.sh build external/Java.Interop/Java.Interop.sln -c Debug
./dotnet-local.sh test external/Java.Interop/tests/Java.Interop-Tests/bin/Debug/${TFM}/Java.Interop-Tests.dll
```
