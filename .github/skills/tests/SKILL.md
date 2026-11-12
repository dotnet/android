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

Check for the local SDK in the configuration needed by the tests:
```bash
CONFIGURATION=Debug # Set to Release for Release tests.
test -x "bin/${CONFIGURATION}/dotnet/dotnet"
```

**If the local SDK is missing, prepare and build it, then run the full-build tests.** On macOS/Linux, use `make prepare && make leeroy` (not `make all`); on Windows, use `build.cmd`. For Release tests on macOS/Linux, use `make prepare CONFIGURATION=Release && make leeroy CONFIGURATION=Release`. The presence of the `dotnet` executable only confirms provisioning, not that the SDK or test assemblies have been built. Rebuild after source changes so tests exercise the updated SDK. See `local-build` for per-project rebuilds and known issues, and `android-devices` for emulators.

**Validate code changes locally before pushing.** Full SDK builds and relevant host/device tests are expected development work. Do not label them prohibitively slow, ask whether to skip them solely because of presumed duration, or rely on CI as the first validation. Start with focused tests while iterating and expand coverage as needed for the affected components. Never silently skip tests: if the user explicitly limits validation or an actual environment constraint blocks it, report the exact command, failure or missing prerequisite, and remaining unvalidated coverage. Include commands and outcomes in the handoff.

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

### Smallest sufficient validation

Escalate only when the change or focused results require it.

| Change touches | Minimum validation |
|---|---|
| Docs, `.github/`, comments | None |
| Standalone libraries (`Microsoft.Android.Build.BaseTasks`, `Xamarin.Android.Tools.AndroidSdk`, `Microsoft.Android.Build.Tasks`, Java.Interop tooling, `generator`, `class-parse`) | `dotnet build` + standalone tests |
| `src/Xamarin.Android.Build.Tasks` | Focused `Xamarin.Android.Build.Tests.dll` filters on the local SDK; `MSBuildDeviceIntegration.dll` only if install/deploy/run changes |
| `src/Mono.Android`, core `src/Java.Interop`, `src/native`, typemaps, hosting | `Mono.Android.NET-Tests` on device for each affected runtime |
| `Makefile`, `build-tools/`, `eng/`, CI YAML | Run the affected entry point or pipeline command locally |

## Running tests

The `${TFM}` placeholder = `DotNetStableTargetFramework` from `Directory.Build.props` (currently `net11.0`).

### Standalone
```bash
dotnet test <project>.csproj -v minimal
dotnet test <project>.csproj -v minimal --filter "Name~TestName"
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
# With more than one device attached:
ADB_TARGET='-s emulator-5554' ./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll --filter "Name~InstallAndRunTests"
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
Results are `.trx` files under `bin/TestDebug/TestResults/`. Repeat any `-c` and `-p:` arguments on both the build/install and `dotnet test --no-build` commands. With several devices attached, add `-p:Device=<serial>` to both.

### Java.Interop tests
Tooling tests are standalone (`dotnet test` on `.csproj`). Core JNI interop tests
live in `tests/Mono.Android-Tests/Java.Interop-Tests/` and run on Android as part
of `Mono.Android.NET-Tests.csproj`, using the on-device commands above.

To build the generator tooling and its tests using the main solution's filter:
```bash
dotnet build src/generator/generator.slnf -c Debug
```
