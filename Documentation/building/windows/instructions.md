# Building .NET for Android on Windows

Building .NET for Android on Windows requires
[dotnetup](https://learn.microsoft.com/dotnet/core/tools/dotnetup/getting-started)
on `PATH`. Use the **Developer Command Prompt** that Visual Studio installs
so the native build tools are available. The required .NET SDK version is
pinned in `global.json`.

## Building .NET for Android

 1. Install the [build dependencies](dependencies.md).

 2. Clone the dotnet/android repo:

        git clone https://github.com/dotnet/android.git

 3. Navigate to the `android` directory

 4. (Optional) [Configure the build](../configuration.md).

 5. In a [Developer Command Prompt][developer-prompt], install the pinned SDK:

        dotnetup sdk install

    SDK binaries are shared across worktrees; Android packs and manifests stay
    worktree-local. No per-worktree .NET SDK installation is needed.

 6. Build the project:

        dotnetup dotnet build Microsoft.Android.slnx

    The solution automatically prepares build dependencies, builds the configured
    Android API levels and ABIs, and configures the local Android workload.
    No build wrapper or separate preparation command is needed. Use `-c Release`
    for a Release build.

After the solution has built successfully, you can use `dotnet-local.cmd` to create and build Android projects.

[developer-prompt]: https://docs.microsoft.com/dotnet/framework/tools/developer-command-prompt-for-vs

See [SDK selection and CI](../../../eng/README.md#sdk-selection-and-ci) for
dependency-update and CI setup details.

## Windows Build Notes

Opening `Microsoft.Android.slnx` in Visual Studio currently tends to hold file
locks on output assemblies containing MSBuild tasks.  If you are only making
changes to Xamarin.Android.Build.Tasks, one way to avoid this issue is to open
`Microsoft.Android.Build.Tasks.slnx` instead.  But if you are working on changes
outside of the build tasks, then you might prefer to work in an editor like
Visual Studio Code instead and build via the command-line.

## Creating a local .NET for Android Workload

After the solution build, `bin\$(Configuration)\lib\packs` will be
populated with a local Android "workload" in
`Microsoft.Android.Sdk.$(HostOS)` matching your operating system.

Create a new project with `dotnet-local.cmd new android`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net12.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Build the project:

    > dotnet-local.cmd build foo.csproj

The `dotnet-local` script sets worktree-local workload overrides and forwards
arguments unchanged through dotnetup, using normal `global.json` lookup.
It defaults to Debug workload paths. Use `set CONFIGURATION=Release` in cmd
or `$env:CONFIGURATION = 'Release'` in PowerShell to select Release packs;
`-c Release` only controls the forwarded build/test command.
Extracted distribution workloads are preferred over source-build packs when present.

See the [One .NET Documentation](../../guides/OneDotNet.md) for further details.

## Creating installers

.NET for Android workload packs can be built and packaged with:

    dotnetup dotnet pack Microsoft.Android.slnx

Several `.nupkg` files will be output in `.\bin\Build$(Configuration)\nuget-unsigned`.
Packing defaults to Release. Use `--no-build -c Debug` to package an existing
Debug build without rebuilding it.

## Building Unit Tests

Once `dotnetup dotnet build Microsoft.Android.slnx` has completed, the unit tests may
be built with e.g.:

    dotnet-local.cmd build Microsoft.Android-Tests.slnx /restore /p:Configuration=Debug /bl:bin\TestDebug\msbuild-build-tests.binlog

For Release tests, set `CONFIGURATION=Release` and pass `-c Release`.

## Running Unit Tests

Run the host-side build tests with:

    dotnet-local.cmd test bin\TestDebug\net11.0\Xamarin.Android.Build.Tests.dll

Append `--list-tests` to list tests or `--filter "FullyQualifiedName~Aapt2Tests"`
to select a subset. Standalone test projects use
`dotnetup dotnet test <project>.csproj`; see [Running Tests](../../workflow/UnitTests.md)
for other test assemblies.

## Running On-Device Tests

The on-device test apps are ordinary `dotnet test` projects that run their
NUnit tests inside an Android instrumentation. Install the app, then run the
tests against the attached device or emulator:

    set CONFIGURATION=Release
    dotnet-local.cmd build -t:Install -c Release tests\Mono.Android-Tests\Mono.Android-Tests\Mono.Android.NET-Tests.csproj
    pushd tests\Mono.Android-Tests\Mono.Android-Tests
    ..\..\..\dotnet-local.cmd test Mono.Android.NET-Tests.csproj --no-build -c Release --report-trx --results-directory ..\..\..\bin\TestRelease\TestResults
    popd

The same pattern works for
`tests\CodeGen-Binding\Xamarin.Android.JcwGen-Tests\Xamarin.Android.JcwGen-Tests.csproj`.

Results are written as `.trx` files into the `--results-directory` directory.

Individual results stream as tests finish; use `--output Detailed` to display
them. If instrumentation is interrupted, the adapter also prints observed
outcomes, partial counts, and unfinished tests without needing a TRX file.
Unfinished tests are context, not proven crash causes, and partial counts do
not establish that every intended test ran. Normal TRX reporting is unchanged.

### Running Specific On-Device Tests

`dotnet test` filtering options apply, for example:

    ..\..\..\dotnet-local.cmd test Mono.Android.NET-Tests.csproj --no-build --filter-class "Xamarin.Android.RuntimeTests.JnienvTest"

### Running On-Device Tests with Include/Exclude

If an on-device test uses the NUnit `[Category]` custom attribute, then those
tests can be explicitly included or excluded from execution by setting the
`$(IncludeCategories)` or `$(ExcludeCategories)` MSBuild properties when
building the test app. These flow to the on-device instrumentation through
`runtimeconfig.json`.

For example, to exclude tests that use the internet (`InetAccess`) category:

    set CONFIGURATION=Release
    dotnet-local.cmd build -t:Install -c Release /p:ExcludeCategories=InetAccess tests\Mono.Android-Tests\Mono.Android-Tests\Mono.Android.NET-Tests.csproj

To specify multiple categories, separate each category with a `:` character.
