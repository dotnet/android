# Building .NET for Android on Linux and macOS

Building .NET for Android on Linux and macOS uses the .NET SDK and MSBuild.
Install [dotnetup](https://learn.microsoft.com/dotnet/core/tools/dotnetup/getting-started)
and put `dotnetup` on `PATH`. No shell-profile activation, SDK `PATH` changes,
or global `DOTNET_ROOT` setting is required. SDK binaries are shared across
worktrees; Android packs and manifests stay inside each worktree.

# Building .NET for Android

 1. Install the [build dependencies](dependencies.md).

 2. Clone the dotnet/android repo:

        git clone https://github.com/dotnet/android.git

 3. Navigate to the `android` directory

 4. (Optional) [Configure the build](../configuration.md).

 5. Install the pinned SDK:

        dotnetup sdk install

 6. Build the project:

        dotnetup dotnet build Microsoft.Android.slnx

    The solution prepares build dependencies, builds the configured Android
    API levels and ABIs, and configures the local Android workload. No separate
    preparation command is needed. Workload setup runs for command-line
    solution builds, not IDE builds or individual project builds.

    For a Release build:

        dotnetup dotnet build Microsoft.Android.slnx -c Release

    Use `-bl` on `dotnetup dotnet build` to capture a binary log.

    Building the solution does not create distribution packages or run tests.

To check the selected version, run `dotnetup dotnet -- --version`.
The separator also forwards help options: `dotnetup dotnet -- build --help`.
See [SDK selection and CI](../../../eng/README.md#sdk-selection-and-ci) for
dependency-update and CI setup details.

# Creating a local .NET for Android Workload

Once `dotnetup dotnet build Microsoft.Android.slnx` has completed, your local
`bin/$(Configuration)/lib/packs` directory will be populated with a
local Android "workload" in `Microsoft.Android.Sdk.$(HostOS)` matching
your operating system.

Create a new project with `./dotnet-local.sh new android`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net12.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Build the project with:

    $ ./dotnet-local.sh build foo.csproj

The `dotnet-local` script sets worktree-local workload overrides and forwards
arguments unchanged through `dotnetup dotnet`, using normal `global.json` lookup.
It defaults to Debug workload paths. Set `CONFIGURATION=Release` to select Release
packs; `-c Release` only controls the forwarded build/test command.
Extracted distribution workloads are preferred over source-build packs when present.
Dependencies are restored through the NuGet cache and extracted locally.
Build and packaging operations never install or remove workloads in the
shared SDK.

See the [One .NET Documentation](../../guides/OneDotNet.md) for further details.

# Creating installers

Build and create the .NET for Android workload packages with:

    dotnetup dotnet pack Microsoft.Android.slnx

Packing defaults to Release. Use `--no-build -c Debug` to package an existing
Debug build without rebuilding it.

Several `.nupkg` files will be output in `./bin/Build$(Configuration)/nuget-unsigned`.
Packing creates the distribution artifacts; it does not install workloads.
CI extracts these packages into `bin/$(Configuration)/workload` for testing,
separate from source-build packs in `bin/$(Configuration)/lib`.
`dotnet-local` uses the extracted workload when present; rebuilding the solution
switches back to source-build output.

# Running Unit Tests


The `dotnet/android` repo contains several unit tests:

  * NUnit-based unit tests, for stand-alone assemblies and utilities.

  * `.apk`/instrumentation-based unit tests, which need to execute on an
    Android device.

Run the host-side build tests with:

    ./dotnet-local.sh test bin/TestDebug/net11.0/Xamarin.Android.Build.Tests.dll

Standalone test projects can be run with `dotnetup dotnet test <project>.csproj`.

All `.apk`-based (on-device) unit tests are run with `dotnetup dotnet test`; see
[Running On-Device Tests](#running-on-device-tests) below.


## Listing Nunit Tests

    ./dotnet-local.sh test bin/TestDebug/net11.0/Xamarin.Android.Build.Tests.dll --list-tests

This lists the tests in the specified assembly.

## Running Specific Nunit Tests

    ./dotnet-local.sh test bin/TestDebug/net11.0/Xamarin.Android.Build.Tests.dll \
        --filter "FullyQualifiedName~Aapt2Tests.Aapt2Compile"

## Running On-Device Tests

The on-device test apps are ordinary `dotnetup dotnet test` projects that run their
NUnit tests inside an Android instrumentation. Install the app, then run the
tests against the attached device or emulator:

    $ export CONFIGURATION=Release
    $ ./dotnet-local.sh build -t:Install -c Release \
        tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj
    $ (cd tests/Mono.Android-Tests/Mono.Android-Tests && \
        ../../../dotnet-local.sh test Mono.Android.NET-Tests.csproj --no-build -c Release \
            --report-trx --results-directory ../../../bin/TestRelease/TestResults)

The same pattern works for
`tests/CodeGen-Binding/Xamarin.Android.JcwGen-Tests/Xamarin.Android.JcwGen-Tests.csproj`.

Results are written as `.trx` files into the `--results-directory` directory.

Individual results stream as tests finish; use `--output Detailed` to display
them. If instrumentation is interrupted, the adapter also prints observed
outcomes, partial counts, and unfinished tests without needing a TRX file.
Unfinished tests are context, not proven crash causes, and partial counts do
not establish that every intended test ran. Normal TRX reporting is unchanged.

### Running Specific On-Device Tests

`dotnetup dotnet test` filtering options apply, for example:

    $ ../../../dotnet-local.sh test Mono.Android.NET-Tests.csproj --no-build \
        --filter-class "Xamarin.Android.RuntimeTests.JnienvTest"

### Running On-Device Tests with Include/Exclude

If an on-device test uses the NUnit `[Category]` custom attribute, then those
tests can be explicitly included or excluded from execution by setting the
`$(IncludeCategories)` or `$(ExcludeCategories)` MSBuild properties when
building the test app. These flow to the on-device instrumentation through
`runtimeconfig.json`.

For example, to exclude tests that use the internet (`InetAccess`) category:

    $ export CONFIGURATION=Release
    $ ./dotnet-local.sh build -t:Install -c Release \
        -p:ExcludeCategories=InetAccess \
        tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj

To specify multiple categories, separate each category with a `:` character.

### Isolated JNI reference leak checks

Run `JniReferenceLeak` separately from unrelated tests: reference counts are
process-wide. `JavaSideActivation` in `Xamarin.Android.JcwGen-Tests` is an explicit
test; the category filter selects it without adding it to ordinary full-suite runs.

    $ export CONFIGURATION=Debug
    $ ./dotnet-local.sh build -t:Install -c Debug \
        -p:IncludeCategories=JniReferenceLeak -p:RuntimeIdentifier=android-arm64 \
        -p:AdbTarget="-s emulator-PORT" \
        tests/CodeGen-Binding/Xamarin.Android.JcwGen-Tests/Xamarin.Android.JcwGen-Tests.csproj
    $ (cd tests/CodeGen-Binding/Xamarin.Android.JcwGen-Tests && \
        ../../../dotnet-local.sh test Xamarin.Android.JcwGen-Tests.csproj --device emulator-PORT --no-build -c Debug \
            -p:IncludeCategories=JniReferenceLeak -p:RuntimeIdentifier=android-arm64 \
            -p:AdbTarget="-s emulator-PORT" \
            --report-trx --results-directory ../../../bin/TestDebug/JniReferenceLeakResults)

Replace `emulator-PORT` with a dedicated device's serial. Use the same pattern
with `Mono.Android.NET-Tests.csproj` from the examples above to run the string
and UTF-8 `TryFindClass` leak checks and the four direct `ReleasePeer` checks. These checks
remain category-tagged, not explicit; the runtime test instrumentation excludes
them unless `JniReferenceLeak` is the only included category.
To run only the deterministic completion-protocol regressions on Android, use
`IncludeCategories=JniReferenceLeakMeasurement` with `Mono.Android.NET-Tests.csproj`.
When changing categories on a NativeAOT build, use `-t:Rebuild;Install` to refresh
the embedded runtime configuration and APK. Verify the reported test count:
one in `Xamarin.Android.JcwGen-Tests` and six in `Mono.Android.NET-Tests`.
If rebuilding removes a referenced project's NuGet assets, run
`./dotnet-local.sh clean` first, then
`./dotnet-local.sh build '-t:Build;Install'` so restore follows cleaning.

Both baseline and measured samples wait for collection completion, not for the
count to fall below a desired value. Android uses an unreachable peer cycle as
a collection witness, a changed bridge-completion generation on CoreCLR/NativeAOT,
and a stable snapshot with no transient weak globals. Timeout diagnostics
include strong/weak counts and generation; a timeout must not be mistaken for
evidence of a leak. The retained-GREF positive controls have been removed because
their exact process-wide deltas were unstable. This loses positive-control
verification that the measurement helper detects deliberately retained references;
it does not fix a runtime leak or establish its root cause. The remaining checks,
measurement helper, and thresholds are unchanged.

The witness publishes a finalization flag rather than inspecting a peer weak
reference: CoreCLR/NativeAOT weak-reference reads can themselves wait for bridge
completion. Managed GC/finalizer waits run off the timeout thread; JNI peer
draining remains on the instrumentation thread.
Successful sampling also requires the collection worker to finish. Timeout
handling checks for a completed worker fault before reporting the timeout.
If another collection is needed to finalize the witness, the previous bridge
must complete first, and the next sample requires a generation newer than
that new request. An additional unreachable cycle ensures the retry requests
bridge work even if the original witness finalizes just before its GC.
A timeout does not cancel an uninterruptible runtime
GC/finalizer wait; it poisons the
measurement helper for the rest of that process, preventing further batches or
GC workers. Restart the test process instead of retrying in the same process.


# How do I build `Mono.Android.dll` for a given API Level?

The solution builds every configured `AndroidBuildApiLevel`. To build one
binding assembly directly after building the solution, pass its API properties:

    dotnetup dotnet build src/Mono.Android/Mono.Android.csproj \
        -p:AndroidApiLevel=37.1 -p:AndroidPlatformId=37.1 -p:AndroidFrameworkVersion=v17.1

Use the matching values from `AndroidBuildApiLevel` in `Configuration.props`.
Alternatively, use [`Configuration.Override.props`][override-props] to change
the default API configuration.

[override-props]: ../../README.md#build-configuration

# How do I rebuild native binaries?

`dotnetup dotnet build Microsoft.Android.slnx` invokes the CMake-backed native projects
for the configured ABIs and tracks native source changes. The CoreCLR and
NativeAOT wrappers can also be built directly after preparation:

    dotnetup dotnet build src/native/native-clr.csproj
    dotnetup dotnet build src/native/native-nativeaot.csproj

Runtime and reference assemblies from `dotnet/runtime` are restored as NuGet
dependencies; the old `external/mono` rebuild shortcuts no longer apply.
