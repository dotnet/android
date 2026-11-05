# Shared host test boundaries

These focused host tests exercise the real `Xamarin.ProjectTools` library and
the helper sources shared by the legacy build tests, modern build-task tests
and device-integration host. They do not convert those consumer suites or run
Android/device operations.

## Run

Use the repository's stable host SDK (.NET 11). Build the host-only preparation
tasks first; ProjectTools' `AndroidBuildConfig` reference needs them to generate
its configuration source. No Android SDK build is needed for these probes.

```sh
dotnet build build-tools/xa-prep-tasks/xa-prep-tasks.csproj -c Debug
dotnet build tests/HostTestInfrastructure-Tests/NUnit/HostTestInfrastructure.NUnit.Tests.csproj -c Debug
dotnet test tests/HostTestInfrastructure-Tests/NUnit/HostTestInfrastructure.NUnit.Tests.csproj \
  -c Debug --no-build --logger trx
dotnet build tests/HostTestInfrastructure-Tests/MSTest/HostTestInfrastructure.MSTest.Tests.csproj -c Debug
DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test \
  --project tests/HostTestInfrastructure-Tests/MSTest/HostTestInfrastructure.MSTest.Tests.csproj \
  -c Debug --no-build --report-trx
```

Repeat **all** commands with `-c Release` for that configuration. Only the MSTest
invocation selects MTP; root runner configuration and existing NUnit consumers
are unchanged. MSTest/MTP versions match the SourceWriter pilot's project-local
pins. ProjectTools references the existing NUnit **framework** for its legacy
overloads, not a test SDK, adapter, console runner or native test entry point.

The tests cover parser success and assertion failures, native process capture,
real HTTP success/404/503 responses, cached downloads, exhausted environment
retries, diagnostic routing, framework-native inconclusive exceptions,
name sanitization, data-row context across `await`, retained log/binlog
attachments, read-only directory cleanup, and TextWriter-based build-engine
logging/object registration. One intentional inconclusive download is expected
in each runner. Inspect TRX outcomes: NUnit's console summary can omit that case
from its passed/total counters.

Retention tests deliberately leave their registered files available until the
runner has copied them into its results directory. Each row has its own output
directory. These files are ordinary ignored test output.

## Boundaries for subsequent migrations

* `DownloadedCache.cs` and `ProcessExtensions.cs` contain the operations.
  Their `.NUnit.cs` partials preserve the original constructors/extension
  overloads, including late-bound NUnit diagnostic routing. New overloads take
  explicit per-instance/per-call diagnostic and inconclusive callbacks. An
  inconclusive callback must throw; returning is an explicit error, never a
  success-shaped filename or environment update. The tests use the **same
  built library** from both frameworks.
* Existing ProjectTools callers such as `BuildItem`, `Builder`, `DotNetCLI` and
  `GradleCLI` still use the NUnit-default overloads. A migrated consumer must
  propagate its own callbacks through those entry points before relying on
  framework-native skip handling for their internal operations. Do not solve
  that by assigning a process-global current context.
* `EnvironmentHelper` and `NativeToolTestHelper` separate their small set of
  framework assertions into required partial implementations. Existing
  consumers compile the `.NUnit.cs` implementations; the MSTest proof compiles
  `MSTest/HelperAssertions.cs` instead. Omitting an implementation is a compiler
  error, not a silently removed assertion. The MSTest parser implementation
  preserves numeric equality between the parser's signed length/version
  constants and unsigned binary fields.
* Modern Build.Tasks explicitly links the parser's NUnit implementation and
  the name helper. Legacy Build.Tests explicitly links the native helper's
  NUnit implementation. MSBuildDeviceIntegration's existing utilities glob
  includes the parser implementation automatically. The shared
  `NativeToolTestHelperTests.cs` still has NUnit attributes and assertions:
  do not convert it while either NUnit consumer compiles it. Separate framework
  wrappers/test bodies when those consumers are migrated.
* `MockBuildEngine` already accepts a `TextWriter` and is unchanged. The probes
  verify its exact prefixes, collected events and registered-object behavior,
  then forward the buffered output to each framework's own context.
* `TestNameUtilities.GetDirectoryName` preserves the old character removal
  exactly; it does not invent an MSTest row identity or fix existing name
  collisions. A later adapter must supply the agreed original row name rather
  than assume MSTest's method-level `TestName` includes arguments.
* `FileSystemUtils.CleanupTestOutputDirectory` receives the output directory,
  deletion decision, writer and attachment callback explicitly. The legacy
  NUnit wrapper still looks up directories by NUnit test ID and deletes only
  `Passed`/`Skipped` output; failed/inconclusive output retains logs/binlogs.
  The modern NUnit base still performs its original unconditional cleanup.
  MSTest's attachment API accepts a path but not NUnit's description; the
  extraction preserves the description at the callback boundary.

`BaseTest`'s setup, virtual cleanup, fixture teardown and global Windows ADB
teardown remain NUnit-owned. `DeviceTest` is unchanged: capture still runs
before base cleanup, guest users/packages remain fixture resources, and no
one-time teardown becomes per-test teardown. Test IDs, arguments, parameters,
outcome mapping, inherited resources and exactly-once lifecycle dispatch must
be explicitly adapted and validated in the later whole-suite migration; these
probes do not claim that migration is complete. In particular, do not add a
second cleanup attribute to an already-attributed shared virtual override.

The actual modern consumer targets .NET 12 and needs the SDK version in
`eng/Versions.props`. Full legacy and device-host consumer builds additionally
need a prepared repository SDK; passing these probes is not evidence that
those build/device lanes, Windows/Linux runners or IDE integration passed.
