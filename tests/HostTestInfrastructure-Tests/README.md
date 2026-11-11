# Shared host helper tests

These tests exercise `Xamarin.ProjectTools` and the helper sources shared by
NUnit and MSTest consumers: downloads, environment variables, diagnostics,
bootstrap parsing, native process output, temporary-directory cleanup and
attachments. They require no Android SDK build or device.

## Run

Run from the repository root with its configured .NET SDK. Build the preparation
tasks first so ProjectTools' `AndroidBuildConfig` reference can generate its
configuration source.

```sh
dotnet build build-tools/xa-prep-tasks/xa-prep-tasks.csproj -c Debug
dotnet test tests/HostTestInfrastructure-Tests/NUnit/HostTestInfrastructure.NUnit.Tests.csproj \
  -c Debug --logger trx
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test \
  --project tests/HostTestInfrastructure-Tests/MSTest/HostTestInfrastructure.MSTest.Tests.csproj \
  -c Debug --report-trx
```

Use `-c Release` in all commands to test Release. Keep MTP selection scoped to
the MSTest invocation; the NUnit project uses VSTest. Each runner has one
intentional inconclusive download case. Retention tests leave their log/binlog
files available for attachment copying.

## Migrating consumers

The `.NUnit.cs` adapters preserve existing constructors, overloads and assertion
behavior. MSTest consumers must supply their own diagnostic/inconclusive
callbacks and compile `MSTest/HelperAssertions.cs` instead of the NUnit assertion
partials. Inconclusive callbacks must throw the framework's exception. Propagate
them through callers rather than setting a process-global current context.
ProjectTools remains a library with a NUnit framework dependency for its legacy
overloads, not a test runner.

Keep outcome and attachment policy in the calling framework. Shared name
sanitization does not give MSTest parameterized rows their original NUnit names,
and extracting cleanup helpers does not migrate fixture/device lifetimes.
These focused tests do not replace consumer builds or on-device tests.
MSTest source generation registers tests but can still use reflection fallbacks.
