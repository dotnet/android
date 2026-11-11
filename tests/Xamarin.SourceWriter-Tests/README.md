# SourceWriter host tests

This host-only project uses MSTest on Microsoft.Testing.Platform (MTP). It builds
as a native MTP executable (`EnableMSTestRunner`) and pins its own MSTest and MTP
preview versions. `MSTest.SourceGeneration` generates the test registry; this is
generated metadata, not fully reflection-free execution. No Android SDK, build
of the product or device is required.

## Run from the repository root

Use .NET SDK 11 Preview 6 or later. Select MTP for this invocation only; the
repository's `global.json` and other (NUnit/VSTest) suites are unchanged:

```sh
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test \
  --project tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj \
  -c Debug --report-trx \
  --results-directory bin/TestDebug/TestResults/SourceWriter
```

Add `--list-tests` to discover tests, or
`--filter 'FullyQualifiedName=Xamarin.SourceWriter.Tests.ClassWriterTests.Basics'`
to select one. MTP options go directly on the command line, not after `--`.

An already-built module can also be run directly:

```sh
dotnet bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll --report-trx
```

Caveats:

* Plain VSTest-mode `dotnet test` is rejected by MTP for this project.
* Don't pass a solution containing unconverted NUnit projects to the MTP runner.

## CI

The public pipeline runs this suite only when the `runNativeMtpHostTests`
parameter is enabled, using the command above on Linux and publishing the TRX.
