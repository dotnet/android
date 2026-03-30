# Test Catalog

Mapping of test area keywords to assemblies, filters, and build prerequisites.

**Legend:**
- **Assembly**: The test DLL to pass to `dotnet test` (relative to repo root). `${TFM}` = current `DotNetStableTargetFramework` from `Directory.Build.props`.
- **Filter**: The `--filter` argument for `dotnet test`, or special instructions for non-`dotnet test` runs.
- **Build**: What must be built before running:
  - **Standalone** — Can run with plain `dotnet test <project>.csproj`. No local SDK needed.
  - **Full-build** — Requires the local SDK (`dotnet-local.sh`). Build with `./dotnet-local.sh build Xamarin.Android.sln -c Debug` or `make`.
- **Device**: Whether an Android device/emulator is required.

---

## Standalone Tests — No Local SDK Required

These tests can be run immediately with `dotnet test` on the `.csproj`, even if the repo has not been fully built.

**Note:** Git submodules must be initialized. Run `git submodule update --init --recursive` if needed.

| Test Area | Project | Command |
|-----------|---------|---------|
| **trimmable type map** (unit) | `tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/` | `dotnet test tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests.csproj -v minimal` |
| **aidl** | `tests/Xamarin.Android.Tools.Aidl-Tests/` | `dotnet test tests/Xamarin.Android.Tools.Aidl-Tests/Xamarin.Android.Tools.Aidl-Tests.csproj -v minimal` |
| **logcat** | `external/Java.Interop/tests/logcat-parse-Tests/` | `dotnet test external/Java.Interop/tests/logcat-parse-Tests/logcat-parse-Tests.csproj -v minimal` |
| **source writer** | `external/Java.Interop/tests/Xamarin.SourceWriter-Tests/` | `dotnet test external/Java.Interop/tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj -v minimal` |
| **java source** | `external/Java.Interop/tests/Java.Interop.Tools.JavaSource-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaSource-Tests/Java.Interop.Tools.JavaSource-Tests.csproj -v minimal` |
| **maven** | `external/Java.Interop/tests/Java.Interop.Tools.Maven-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.Maven-Tests/Java.Interop.Tools.Maven-Tests.csproj -v minimal` |
| **api xml adjuster** | `external/Java.Interop/tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests/` | `dotnet test external/Java.Interop/tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests.csproj -v minimal` |
| **java callable wrappers** | `external/Java.Interop/tests/Java.Interop.Tools.JavaCallableWrappers-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaCallableWrappers-Tests/Java.Interop.Tools.JavaCallableWrappers-Tests.csproj -v minimal` |
| **java type system** | `external/Java.Interop/tests/Java.Interop.Tools.JavaTypeSystem-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.JavaTypeSystem-Tests/Java.Interop.Tools.JavaTypeSystem-Tests.csproj -v minimal` |
| **expressions** | `external/Java.Interop/tests/Java.Interop.Tools.Expressions-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.Expressions-Tests/Java.Interop.Tools.Expressions-Tests.csproj -v minimal` ⚠️ Requires `javac` |
| **generator tools** | `external/Java.Interop/tests/Java.Interop.Tools.Generator-Tests/` | `dotnet test external/Java.Interop/tests/Java.Interop.Tools.Generator-Tests/Java.Interop.Tools.Generator-Tests.csproj -v minimal` |
| **generator** | `external/Java.Interop/tests/generator-Tests/` | `dotnet test external/Java.Interop/tests/generator-Tests/generator-Tests.csproj -v minimal` |
| **bytecode** | `external/Java.Interop/tests/Xamarin.Android.Tools.Bytecode-Tests/` | `dotnet test external/Java.Interop/tests/Xamarin.Android.Tools.Bytecode-Tests/Xamarin.Android.Tools.Bytecode-Tests.csproj -v minimal` ⚠️ Requires `javac` |
| **base tasks** | `external/.../tests/Microsoft.Android.Build.BaseTasks-Tests/` | `dotnet test external/Java.Interop/external/xamarin-android-tools/tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj -v minimal` |
| **android sdk tools** | `external/.../tests/Xamarin.Android.Tools.AndroidSdk-Tests/` | `dotnet test external/Java.Interop/external/xamarin-android-tools/tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj -v minimal` |

---

## Host-Side MSBuild Tests (full-build — requires local SDK)

Assembly: `bin/TestDebug/${TFM}/Xamarin.Android.Build.Tests.dll`
Build: Full-build — `./dotnet-local.sh build Xamarin.Android.sln -c Debug` or `make`
Device: No

| Test Area | Filter | Test Classes / Notes |
|-----------|--------|---------------------|
| **build** (general) | `--filter "FullyQualifiedName~BuildTest"` | `BuildTest`, `BuildTest2`, `BuildTest3` — core build pipeline tests |
| **smoke** | `--filter "cat=SmokeTests"` | Quick subset of build, packaging, and asset pack tests |
| **aot** | `--filter "cat=AOT"` | `AotTests` + AOT-related tests in `BuildTest`, `IncrementalBuildTest` |
| **llvm** | `--filter "cat=LLVM"` | LLVM-specific AOT compilation tests |
| **bindings** | `--filter "FullyQualifiedName~BindingBuildTest"` | Java binding generation and build tests |
| **packaging** | `--filter "FullyQualifiedName~PackagingTest"` | APK/AAB packaging, signing, zipalign |
| **incremental build** | `--filter "FullyQualifiedName~IncrementalBuildTest"` | Incremental build correctness tests |
| **resources** | `--filter "FullyQualifiedName~AndroidUpdateResourcesTest"` | Android resource processing (aapt2) |
| **aapt2** | `--filter "FullyQualifiedName~Aapt2"` | aapt2 task-level tests (in `Tasks/`) |
| **manifest** | `--filter "FullyQualifiedName~ManifestTest"` | AndroidManifest.xml generation and merging |
| **sdk** | `--filter "FullyQualifiedName~XASdkTests"` | .NET SDK-style project tests |
| **single project** | `--filter "FullyQualifiedName~SingleProjectTest"` | Android single-project build and packaging tests |
| **fsharp** | `--filter "cat=FSharp"` | F# project build tests |
| **wear** | `--filter "FullyQualifiedName~WearTests"` | Wear OS build tests |
| **code behind** | `--filter "FullyQualifiedName~CodeBehindTests"` | Layout code-behind generation |
| **marshal methods** | `--filter "FullyQualifiedName~MarshalMethodTests"` | Marshal method generation tests |
| **asset packs** | `--filter "FullyQualifiedName~AssetPackTests"` | Play Asset Delivery tests |
| **gradle** | `--filter "FullyQualifiedName~AndroidGradleProjectTests"` | Gradle project integration |
| **deferred build** | `--filter "FullyQualifiedName~DeferredBuildTest"` | Deferred build tests |
| **linker** | `--filter "FullyQualifiedName~LinkerTests"` | IL Linker / trimmer tests |
| **library** | `--filter "FullyQualifiedName~BuildWithLibraryTests"` | Building with library references |
| **environment** | `--filter "FullyQualifiedName~EnvironmentContentTests"` | Environment variable injection tests |
| **dependencies** | `--filter "FullyQualifiedName~AndroidDependenciesTests"` | Android SDK/NDK dependency resolution |
| **trimmable type map (build)** | `--filter "FullyQualifiedName~TrimmableTypeMapBuildTests"` | Trimmable type map build integration |

### Task-level unit tests

Same assembly as above. These test individual MSBuild tasks in isolation with `MockBuildEngine`.

| Test Area | Filter | Notes |
|-----------|--------|-------|
| **tasks** (all) | `--filter "FullyQualifiedName~Xamarin.Android.Build.Tests.Tasks"` | All task-level tests |
| **d8/dex** | `--filter "FullyQualifiedName~D8Tests"` | D8 dexing task |
| **filter assemblies** | `--filter "FullyQualifiedName~FilterAssembliesTests"` | Assembly filtering |
| **resource generation** | `--filter "FullyQualifiedName~GenerateResourceCaseMapTests"` | Resource case map generation |
| **package manager** | `--filter "FullyQualifiedName~GeneratePackageManagerJavaTests"` | Package manager Java generation |
| **key tool** | `--filter "FullyQualifiedName~KeyToolTests"` | Keystore/signing tasks |
| **ndk** | `--filter "FullyQualifiedName~NdkUtilTests"` | NDK utility tasks |

---

## Device Integration Tests (full-build — requires local SDK + device)

Assembly: `bin/TestDebug/MSBuildDeviceIntegration/${TFM}/MSBuildDeviceIntegration.dll`
Build: Full-build + device/emulator connected
Device: **Yes** (most tests have `[Category("UsesDevice")]`)

| Test Area | Filter | Notes |
|-----------|--------|-------|
| **device** (all) | (no filter) | All device integration tests |
| **install and run** | `--filter "FullyQualifiedName~InstallAndRunTests"` | App installation and launch |
| **install** | `--filter "FullyQualifiedName~InstallTests"` | Installation-only tests |
| **debugging** | `--filter "FullyQualifiedName~DebuggingTest"` | Debugger attach and breakpoint tests |
| **performance** | `--filter "cat=Performance"` | Startup time, build time measurements |
| **localization** | `--filter "cat=Localization"` | Locale/culture device tests |
| **timezone** | `--filter "cat=TimeZoneInfo"` | Time zone handling on device |
| **wear** | `--filter "cat=WearOS"` | Wear OS device tests |
| **aot profile** | `--filter "cat=ProfiledAOT"` | AOT profiling on device |
| **export** | `--filter "FullyQualifiedName~MonoAndroidExportTest"` | `[Export]` attribute tests |
| **bundletool** | `--filter "FullyQualifiedName~BundleToolTests"` | AAB bundle tool tests |
| **uncaught exceptions** | `--filter "FullyQualifiedName~UncaughtExceptionTests"` | Unhandled exception behavior |
| **system app** | `--filter "FullyQualifiedName~SystemApplicationTests"` | System application tests |
| **instant run** | `--filter "FullyQualifiedName~InstantRunTest"` | Hot reload / instant run |

---

## On-Device Runtime Tests (full-build — requires local SDK + device)

These use NUnitLite and run directly on the device via `-t:RunTestApp`. They do NOT use `dotnet test`.

Build: Full-build + the test project itself
Device: **Yes**

| Test Area | Project | Notes |
|-----------|---------|-------|
| **runtime** (all) | `tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj` | Core runtime tests |
| **networking** | Same project — tests in `Xamarin.Android.Net/` and `System.Net/` | `AndroidMessageHandlerTests`, `AndroidClientHandlerTests`, `HttpClientIntegrationTests` |
| **java interop (on-device)** | Same project — tests in `Java.Interop/` | `JnienvTest`, `JavaListTest` |
| **android app** | Same project — tests in `Android.App/` | `Application`, `Activity` tests |
| **android views** | Same project — tests in `Android.Views/` | Layout inflater, view tests |
| **system.io** | Same project — tests in `System.IO/` | File I/O tests |
| **system.text.json** | Same project — tests in `System.Text.Json/` | JSON serialization tests |
| **system.xml** | Same project — tests in `System.Xml/` | XML processing tests |
| **threading** | Same project — tests in `System.Threading/` | Threading and async tests |
| **drawing** | Same project — tests in `System.Drawing/` | System.Drawing tests |
| **locales (on-device)** | `tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj` | Locale/culture/globalization |
| **embedded DSOs** | `tests/EmbeddedDSOs/EmbeddedDSO/EmbeddedDSO.csproj` | Native library loading |

### On-device test categories

The `Mono.Android.NET-Tests.csproj` dynamically excludes categories based on runtime:
- **CoreCLR runtime**: Excludes `CoreCLRIgnore`, `NTLM`
- **NativeAOT runtime**: Excludes `NativeAOTIgnore`, `SSL`, `NTLM`, `AndroidClientHandler`, `Export`, `NativeTypeMap`
- **LLVM**: Excludes `LLVMIgnore`, `InetAccess`, `NetworkInterfaces`

Other categories: `SSL`, `InetAccess`, `AndroidClientHandler`, `JavaList`, `RuntimeConfig`, `Intune`, `NTLM`

Command:
```bash
./dotnet-local.sh build -t:RunTestApp tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj
```

Results appear in `TestResult-*.xml` in the repo root.

---

## Java.Interop Tests — Mixed Tiers

Tooling tests are standalone (listed in the Standalone Tests table above).
JVM-based tests are full-build (require local SDK + JVM).

Build (full-build only): `./dotnet-local.sh build external/Java.Interop/Java.Interop.sln -c Debug`
Device: **No** (runs on host JVM via `TestJVM`)

| Test Area | Assembly / Project | Notes |
|-----------|--------------------|-------|
| **java-interop** (core) | `external/Java.Interop/tests/Java.Interop-Tests/` | Core JNI interop tests |
| **java-interop export** | `external/Java.Interop/tests/Java.Interop.Export-Tests/` | Export/callable wrapper tests |
| **java-interop dynamic** | `external/Java.Interop/tests/Java.Interop.Dynamic-Tests/` | Dynamic interop |
| **java-interop performance** | `external/Java.Interop/tests/Java.Interop-PerformanceTests/` | JNI performance benchmarks |
| **java-base** | `external/Java.Interop/tests/Java.Base-Tests/` | Java.Base runtime tests |
| **generator** | `external/Java.Interop/tests/generator-Tests/` | Binding generator tests |
| **java callable wrappers** | `external/Java.Interop/tests/Java.Interop.Tools.JavaCallableWrappers-Tests/` | JCW generation |
| **java type system** | `external/Java.Interop/tests/Java.Interop.Tools.JavaTypeSystem-Tests/` | Java type system tooling |
| **java source** | `external/Java.Interop/tests/Java.Interop.Tools.JavaSource-Tests/` | Java source generation |
| **maven** | `external/Java.Interop/tests/Java.Interop.Tools.Maven-Tests/` | Maven integration |
| **bytecode** | `external/Java.Interop/tests/Xamarin.Android.Tools.Bytecode-Tests/` | Bytecode analysis |
| **api xml adjuster** | `external/Java.Interop/tests/Xamarin.Android.Tools.ApiXmlAdjuster-Tests/` | API XML adjustment |
| **source writer** | `external/Java.Interop/tests/Xamarin.SourceWriter-Tests/` | Source code generation |
| **logcat** | `external/Java.Interop/tests/logcat-parse-Tests/` | Logcat parser |

Run these tests with `dotnet test` from each test project directory listed above.

---

## Trimmable Type Map Tests (xUnit) — Mixed Tiers

| Test Area | Tier | Assembly | Notes |
|-----------|------|----------|-------|
| **trimmable type map** (unit) | **Standalone** | `tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/` | Scanner + generator unit tests — `dotnet test` on `.csproj` |
| **trimmable type map** (integration) | **Full-build** | `tests/Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests/` | End-to-end with Mono.Android + build tasks |

---

## Other Tests

| Test Area | Tier | Assembly / Project | Notes |
|-----------|------|--------------------|-------|
| **aidl** | **Standalone** | `tests/Xamarin.Android.Tools.Aidl-Tests/` | AIDL compiler tests — `dotnet test` on `.csproj` |
| **api compatibility** | N/A | `tests/api-compatibility/` | Not a test runner — reference data for API surface checks |
| **android sdk tools** | **Standalone** | `external/Java.Interop/external/xamarin-android-tools/tests/` | Android SDK helper tooling tests — `dotnet test` on `.csproj` |
