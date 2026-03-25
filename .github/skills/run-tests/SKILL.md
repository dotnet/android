---
name: run-tests
description: >-
  Run dotnet/android tests. Trigger on "run tests", "run unit tests", "run build tests",
  "run device tests", "run msbuild tests", "run java interop tests", "run networking tests",
  or any request to run, list, or filter tests in this repository. Knows what to build,
  which assembly to target, and how to apply filters for specific test areas.
---

# Run Tests

Run tests in the dotnet/android repository. This skill knows the build prerequisites, test assemblies, filter syntax, and result interpretation for every test type in the repo.

## Prerequisites

Tests in this repo fall into two tiers based on build requirements:

- **Standalone tests (Tier 1):** Can be run with plain `dotnet test <project>.csproj` — no local SDK build needed. These include TrimmableTypeMap, AIDL, and many Java.Interop tooling tests. See the test catalog for the full list.
- **Full-build tests (Tier 2):** Require the local SDK (`dotnet-local.sh` / `dotnet-local.cmd`). These include MSBuild integration tests, device tests, and on-device runtime tests.

### Check whether the local SDK exists

```bash
# macOS/Linux
ls bin/Debug/dotnet/dotnet 2>/dev/null || ls bin/Release/dotnet/dotnet 2>/dev/null

# Windows
dir bin\Debug\dotnet\dotnet.exe 2>nul || dir bin\Release\dotnet\dotnet.exe 2>nul
```

### If the local SDK does NOT exist

1. **Always run Tier 1 (standalone) tests immediately** — they don't need the SDK.
2. **You MUST use `ask_user` to ask the user** whether they want to do a full repo build to enable Tier 2 tests. Present the choice clearly:
   - **YES** → Run `make prepare && make` (macOS/Linux) or the equivalent Windows commands. This takes a long time (30+ minutes). After the build completes, run all requested Tier 2 tests.
   - **NO** → Skip Tier 2 tests and report only the Tier 1 results. Clearly list which tests were skipped and why.

**IMPORTANT:** Never silently skip tests. Always ask the user for a decision when Tier 2 tests are requested but the local SDK is missing.

### If the local SDK exists

All tests (Tier 1 and Tier 2) can be run. Use `./dotnet-local.sh` (macOS/Linux) or `dotnet-local.cmd` (Windows) for Tier 2 tests.

## Workflow

### 1. Determine what the user wants to test

Parse the user's request to identify:
- **Test area** (e.g., "networking", "aot", "bindings", "java-interop", "packaging")
- **Test type** (build/msbuild tests, device tests, on-device runtime tests)
- **Specific test name** (if the user names a particular test)

Load `references/test-catalog.md` from this skill's directory for the full mapping of test areas to assemblies and filters.

### 2. Determine the operating system

Use the environment context to determine if the user is on macOS/Linux or Windows. This affects:
- Script: `./dotnet-local.sh` vs `dotnet-local.cmd`
- Path separators: `/` vs `\`
- On-device tests require macOS (emulators) or a connected Android device

### 3. Build the test assembly (if needed)

**Important:** The test assembly output path uses the `$(DotNetStableTargetFramework)` property. Check `Directory.Build.props` for the current value (look for `<DotNetStableTargetFramework>`). As of writing, it is `net10.0`, but this changes with each .NET release.

#### Standalone tests (Tier 1) — no build step needed

These tests are built and run in one step using `dotnet test` directly on the `.csproj`:

```bash
# Example: run TrimmableTypeMap unit tests
dotnet test tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests.csproj -v minimal

# Example: run AIDL tests
dotnet test tests/Xamarin.Android.Tools.Aidl-Tests/Xamarin.Android.Tools.Aidl-Tests.csproj -v minimal

# Example: run Java.Interop tooling tests
dotnet test external/Java.Interop/tests/logcat-parse-Tests/logcat-parse-Tests.csproj -v minimal
```

See the test catalog for the full list of standalone test projects and their `dotnet test` commands.

#### Full-build tests (Tier 2) — requires local SDK

Different test types require different build steps. The general pattern is:

```bash
# Build the main solution (includes test projects)
./dotnet-local.sh build Xamarin.Android.sln -c Debug

# Or build just the test project
./dotnet-local.sh build Xamarin.Android.Build.Tasks.sln -c Debug
```

### 4. Run the tests

#### Host-side tests (MSBuild integration + task unit tests) — Tier 2

```bash
# Run all MSBuild tests
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll

# Run with a name filter (contains match)
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "Name~BuildBasicApplication"

# Run with an exact name filter
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "Name=BuildBasicApplication"

# Run by test class (FullyQualifiedName contains)
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "FullyQualifiedName~AotTests"

# Run by NUnit category
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll --filter "cat=SmokeTests"

# List all available tests
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll -lt
```

Where `${TFM}` is the current target framework (e.g., `net10.0`).

#### Device integration tests (requires device/emulator) — Tier 2

```bash
# Run all device integration tests
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll

# Run a specific device test
./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll --filter "Name~InstallAndRunTests"

# Target a specific device
ADB_TARGET="-s emulator-5554" ./dotnet-local.sh test bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll
```

#### On-device runtime tests (NUnitLite on device) — Tier 2

These tests run directly on an Android device/emulator using `NUnitLite`. They are NOT run with `dotnet test` — instead, use the `RunTestApp` MSBuild target:

```bash
# Run all on-device Mono.Android tests
./dotnet-local.sh build -t:RunTestApp tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj

# Run locale tests
./dotnet-local.sh build -t:RunTestApp tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj

# Run embedded DSO tests
./dotnet-local.sh build -t:RunTestApp tests/EmbeddedDSOs/EmbeddedDSO/EmbeddedDSO.csproj
```

After running, a `TestResult*.xml` file is created in the repo root with the NUnit XML results.

#### Java.Interop tests — mixed tiers

Java.Interop has its own test projects under `external/Java.Interop/tests/`. **Tooling tests** (Tier 1) can run with plain `dotnet test` on the `.csproj`. **JVM-based tests** (Tier 2) require the local SDK and a JVM.

Tier 1 (standalone — plain `dotnet test`):
```bash
dotnet test external/Java.Interop/tests/logcat-parse-Tests/logcat-parse-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaSource-Tests/Java.Interop.Tools.JavaSource-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.Maven-Tests/Java.Interop.Tools.Maven-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaCallableWrappers-Tests/Java.Interop.Tools.JavaCallableWrappers-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaTypeSystem-Tests/Java.Interop.Tools.JavaTypeSystem-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.Expressions-Tests/Java.Interop.Tools.Expressions-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Java.Interop.Tools.Generator-Tests/Java.Interop.Tools.Generator-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/generator-Tests/generator-Tests.csproj -v minimal
dotnet test external/Java.Interop/tests/Xamarin.Android.Tools.Bytecode-Tests/Xamarin.Android.Tools.Bytecode-Tests.csproj -v minimal
```

**Note:** Bytecode-Tests and Expressions-Tests require `javac` on PATH. If they fail to build, report that Java compiler is needed.

Tier 2 (requires local SDK + JVM):
```bash
# Build Java.Interop tests
./dotnet-local.sh build external/Java.Interop/Java.Interop.sln -c Debug

# Run Java.Interop core tests
./dotnet-local.sh test external/Java.Interop/tests/Java.Interop-Tests/bin/Debug/${TFM}/Java.Interop-Tests.dll

# Run generator tests
./dotnet-local.sh test external/Java.Interop/tests/generator-Tests/bin/Debug/${TFM}/generator-Tests.dll
```

**Note:** JVM-based Java.Interop tests require a JVM. The `TestJVM` project handles JVM discovery.

#### Trimmable type map tests (xUnit) — Tier 1

```bash
# Run scanner/generator unit tests (standalone — no local SDK needed)
dotnet test tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests.csproj -v minimal
```

The integration tests require the local SDK (Tier 2):
```bash
./dotnet-local.sh test tests/Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests/bin/Debug/${TFM}/Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests.dll
```

#### AIDL tests — Tier 1

```bash
# Standalone — no local SDK needed
dotnet test tests/Xamarin.Android.Tools.Aidl-Tests/Xamarin.Android.Tools.Aidl-Tests.csproj -v minimal
```

#### Android SDK tools tests — Tier 1

```bash
# Standalone — no local SDK needed
dotnet test external/Java.Interop/external/xamarin-android-tools/tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj -v minimal
dotnet test external/Java.Interop/external/xamarin-android-tools/tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj -v minimal
```

#### Makefile targets (macOS/Linux convenience)

```bash
make run-all-tests       # Run everything
make run-nunit-tests     # Run NUnit host-side tests
make run-ji-tests        # Run Java.Interop tests
```

### 5. Filter syntax reference

The `dotnet test` filter uses NUnit's filter language via the `--filter` flag:

| Filter | Example | Meaning |
|--------|---------|---------|
| `Name=X` | `--filter "Name=BuildBasicApplication"` | Exact test name match |
| `Name~X` | `--filter "Name~AOT"` | Test name contains "AOT" |
| `FullyQualifiedName~X` | `--filter "FullyQualifiedName~AotTests"` | FQN contains (matches class name) |
| `cat=X` | `--filter "cat=SmokeTests"` | NUnit category |
| `cat!=X` | `--filter "cat!=XamarinBuildDownload"` | Exclude category |
| Combined | `--filter "Name~Build & cat!=AOT"` | AND logic |
| Or | `--filter "Name~Build \| Name~Package"` | OR logic |

To **list tests without running** them, use `-lt`:

```bash
./dotnet-local.sh test bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll -lt
```

### 6. Interpret results

#### dotnet test output
- **Passed**: Test succeeded
- **Failed**: Test failed — check the error message and stack trace
- **Skipped**: Test was skipped (usually due to platform/configuration constraints)

Look for the summary line: `Passed: X, Failed: Y, Skipped: Z`

#### On-device test results
Results are written to `TestResult-*.xml` files in the repo root. These are NUnit XML format. Look for:
- `<test-case result="Passed">` — success
- `<test-case result="Failed">` — failure with `<failure><message>` and `<stack-trace>`

#### Common failure patterns
- **"Build should have succeeded"** — The test app failed to build. Check the build output above the assertion.
- **"Activity should have started"** — The app didn't launch on the device. Check `logcat.log` in the test output directory.
- **Timeout** — Device tests can time out if the emulator is slow. Consider increasing timeout or using a faster emulator image.
- **"No connected devices"** — Device tests require `adb devices` to show at least one device.

### 7. Tips

- **Incremental testing**: After modifying build tasks in `src/Xamarin.Android.Build.Tasks/`, rebuild just that project before re-running tests.
- **Parallel tests**: MSBuild tests run in parallel by default. Use `NUnit.NumberOfTestWorkers=1` if you need serial execution for debugging.
- **Test output**: Failed tests leave their output in `bin/TestDebug/temp/` for inspection.
- **Windows path**: On Windows, use backslashes and `dotnet-local.cmd` instead of `./dotnet-local.sh`.
