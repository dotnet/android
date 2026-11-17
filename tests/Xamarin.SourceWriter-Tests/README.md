# SourceWriter host tests

This host-only project uses MSTest on Microsoft.Testing.Platform (MTP). It builds
as a native MTP executable (`EnableMSTestRunner`); package versions come from
`eng/Versions.props`. `MSTest.SourceGeneration` generates the test registry; this is
generated metadata, not fully reflection-free execution. No Android SDK, build
of the product or device is required.

## Run from the repository root

The repository `global.json` selects MTP for `dotnet test`:

```sh
dotnet test \
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

## CI

The public pipeline runs this suite only when the `runNativeMtpHostTests`
parameter is enabled, using the command above on Linux and publishing the TRX.
