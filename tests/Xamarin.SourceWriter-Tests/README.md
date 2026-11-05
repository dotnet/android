# SourceWriter host tests

This project is the host-only MSTest pilot. It keeps `Microsoft.NET.Sdk` and runs
as a native Microsoft.Testing.Platform (MTP) executable. Its MSTest and MTP preview
versions are pinned in this project, independently of the product's MTP version.
No Android SDK build or device is required.

`IsTestingPlatformApplication` is set explicitly so native `dotnet test` can
identify this project before its first package restore. Starting with an empty
project `obj/` directory does not require a separate restore or build command.
The runner package generates the executable entry point and registers MSTest
and the TRX extension; these settings do not apply to the referenced library.

## Run from the repository root

Use .NET SDK 11 Preview 6 or later. Select MTP for just this invocation rather than
changing the repository's `global.json` or exporting the runner for other suites:

```sh
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test \
  --project tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj \
  -c Debug --report-trx \
  --results-directory bin/TestDebug/TestResults/SourceWriter
```

For Release, use `-c Release` and `bin/TestRelease/TestResults/SourceWriter`.
Use `--list-tests` to discover tests, or
`--filter 'FullyQualifiedName=Xamarin.SourceWriter.Tests.ClassWriterTests.Basics'`
to select a test. Native MTP options such as `--report-trx` go directly after the
project options, not after a VSTest-style `--` separator.

From this directory, the same command works with
`--project Xamarin.SourceWriter-Tests.csproj`; adjust the results path accordingly.
The process-local runner selection is still needed: `dotnet test` selects its
runner from the working directory's `global.json` and `DOTNET_TEST_RUNNER`, not
from the project path.

An ordinary VSTest-mode `dotnet test` invocation is not supported for this
project: MTP rejects it on the .NET 11 SDK. Use the scoped command above or execute
the built application directly.

## Run already-built tests

After building the desired configuration, CI can select only this module from
the repository root without evaluating the rest of the test projects:

```sh
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test \
  --test-modules bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll \
  --report-trx --results-directory bin/TestDebug/TestResults/SourceWriter
```

Alternatively, execute the native test application directly, without selecting a
`dotnet test` runner:

```sh
dotnet bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll \
  --report-trx --results-directory bin/TestDebug/TestResults/SourceWriter
```

Keep unconverted NUnit suites in separate, ordinary VSTest invocations, with no
MTP environment override, for example:

```sh
dotnet test tests/Xamarin.Android.Tools.Aidl-Tests/Xamarin.Android.Tools.Aidl-Tests.csproj \
  -c Debug --logger trx
```

Do not pass a solution containing unconverted NUnit projects to the native MTP
runner. This pilot does not change repository-wide runner selection or CI jobs.
