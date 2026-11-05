# Opt-in native MTP host CI

The public pipeline's **Run opt-in SourceWriter native MTP host tests**
(`runNativeMtpHostTests`, default `false`) adds a separate, three-slice Linux host
job. `skipTests` still skips it. Only `tests/Xamarin.SourceWriter-Tests` is built
and selected. No Android SDK, emulator, instrumentation runner, global runner
switch, or product package change is involved. Internal/nightly and existing
NUnit jobs are unchanged.

## Prerequisites and build boundary

The pilot uses SDK `11.0.100-rc.3.26522.148`, installed in a job-local directory,
MSTest `4.6.0-preview.26524.1` and MTP/TRX `2.6.0-preview.26524.1`.
The latter pins belong to the converted project, not the root package policy.
The adapter needs Python 3.9+ and the matching .NET runtime.

`run-native-mtp-tests.yaml` is **execution-only**, selecting exactly one built
native-MTP DLL with its runtimeconfig, dependencies and TRX extension alongside
it. It never restores, builds, evaluates a project, accepts a wildcard, or scans
neighboring modules. The caller restores/builds the specific converted project
first; alternatively it can download the complete already-built module directory.
Use a new results directory for every invocation; stale directories are rejected.

The following are the actual steps used by the template, runnable from the
repository root on macOS/Linux (PowerShell can set the same environment variables):

```sh
dotnet build tests/Xamarin.SourceWriter-Tests/Xamarin.SourceWriter-Tests.csproj -c Debug -v:minimal
MTP_MODULE="$PWD/bin/TestDebug/net11.0/Xamarin.SourceWriter-Tests.dll" \
MTP_RESULTS_DIRECTORY="$PWD/bin/native-mtp-sourcewriter-1" \
MTP_SLICE_NUMBER=1 MTP_TOTAL_SLICES=3 MTP_RETRIES=1 \
python3 build-tools/automation/scripts/run-native-mtp-tests.py
```

Repeat with slice numbers 2 and 3 and distinct result directories. Explicit
arguments (`--module`, `--results-directory`, `--filter`, `--slice-number`,
`--total-slices`, `--retries`, `--dotnet`, `--timeout`) can replace the environment
variables. `MTP_TIMEOUT_SECONDS` defaults to 600 per discovery/execution process;
timeouts are infrastructure failures, not retries. `MTP_DOTNET` selects the host
executable. The CI template uses environment inputs so filter values and paths
aren't interpolated into shell commands.

## Discovery, selection and slicing contract

The adapter invokes **the application directly**:
`dotnet <module.dll> --list-tests json`. This is not SDK
`dotnet test --list-tests json`, whose projected discovery metadata can differ.
It requires the direct discovery envelope `schemaVersion: 1`, a `tests` array,
and unique canonical GUID `uid` values. It schedules only those UIDs, not inferred
class/method/category information or parsed display names. Optional nested `type`,
location and trait metadata are retained verbatim but aren't scheduling inputs.
Unknown schemas, absent/non-GUID UIDs and UID collisions fail closed.

`testFilter` / `MTP_FILTER` is the native MSTest `--filter` expression, applied by
MSTest during **discovery only**, before slicing. Examples:

```text
FullyQualifiedName=Xamarin.SourceWriter.Tests.TypeReferenceWriterTests.Constructor
TestCategory=selection&FullyQualifiedName~Rows
TestCategory=escaped\&category
```

Use the [native filter escaping rules][filters], not NUnit `cat != ...` syntax.
No custom filter parser, NUnit worker options, NUnit settings or display-name
fallback is used. Unsupported/nonmatching filters produce zero discovery and
fail; malformed filters preserve the platform's nonzero exit and diagnostic.

Sort the selected module's UIDs ordinally, then take
`uids[sliceNumber - 1::totalSlices]`, using one-based slice numbers. This produces
an exact disjoint union of the discovered case multiset for identical module,
filter, framework version and slice count. Data rows (including identical
arguments and duplicate displays) remain separate only when the framework
discovers separate UIDs. Discovery is not an execution pass; it can evaluate
framework data sources, so data sources must be deterministic and side-effect
free. Folded/dynamically expanded cases without individually discoverable stable
UIDs are not supported: any execution/discovery identity or multiplicity mismatch
is an infrastructure error, not deduplication or success.

UIDs are passed through an MTP response file to avoid command-line size/escaping
limits. Every completed attempt must produce exactly the requested multiset of
TRX result IDs, definitions and entries. This binds the pinned MSTest discovery
UID to TRX `testId`, rather than presuming another framework's identity mapping.
Unexpected zero discovery exits 8 even if all slices would be empty.
An **explicit empty slice of a verified nonempty baseline** succeeds without
starting execution or fabricating a TRX, and records `status: empty-slice`.
Explicit ignored tests preserve native `NotExecuted` outcomes and are not retried.

## Retry and publication

Only native exit 2 with matching completed TRX failures is retryable. Retry
exactly the failed UIDs (one retry by default, configurable 0-10); do not rerun
successful cases. The native app is restarted per attempt. It must discover
the same case identities each time; data-order/identity drift fails the TRX
multiset check. There is no in-process retry extension or test-source migration.

`attempt-N/raw.trx`, stdout/stderr, response files, native `.diag` logs,
`command.json` and all attachments remain immutable. For publication only,
`attempt-N/effective.trx` retains cases whose **last** outcome belongs to that
attempt, preserving their original definitions, output, failure stacks,
execution IDs, deployment directory and attachment paths. No files are moved.
Thus recovered cases publish Passed once, terminal failures publish Failed once,
and failed earlier attempts remain auditable in the full pipeline artifact.
`summary.json` records final UID/outcome/attempt mappings; `selection.json`
records the complete discovery baseline and the chosen slice.

The template publishes only `**/effective.trx`, including attachments, and retains
the entire results directory as a pipeline artifact on success or failure.
Missing TRX is allowed by the publisher solely because explicit empty slices
have none; adapter failures are still failing pipeline tasks. No
`continueOnError`, ignore-exit-code switch or blanket task retry masks them.
Unset `TESTINGPLATFORM_EXITCODE_IGNORE`; the adapter rejects suppression.

Terminal test failures exit 2. Native infrastructure/argument/crash/zero-discovery
exit codes remain nonzero and are not retried or converted to test success.
Adapter schema, missing/malformed TRX, identity, timeout and filesystem failures
exit 10 with `error.json`; every initialized invocation writes `exit.json`.
Invalid CLI argument syntax exits 5 before initialization, not test-failure code 2.
An interrupted/crashed attempt does not generate an effective passing report
from an earlier attempt.

## Coexistence and subsequent waves

Keep the existing `run-nunit-tests.yaml` and `run-sliced-nunit-tests.yaml` for
unconverted NUnit assemblies, including their existing filters/runsettings/retry
semantics. `dotnet-test-slicer` alpha7 remains the latest published version
checked for this change; its [discovery uses NUnit.Engine][slicer] and writes
NUnit runsettings, not native MTP UID selection. It is not invoked or installed
by the new host job.

Local workflows still use separate invocations: scoped
`DOTNET_TEST_RUNNER=Microsoft.Testing.Platform dotnet test --project <converted>`
or direct native execution for SourceWriter; ordinary `dotnet test <NUnit.csproj>`
for AIDL and other unconverted suites. Do not select a mixed solution with MTP.

Later waves should opt in per converted module only after verifying its native
discovery/UID/TRX contract with its actual categories, data sources and lifecycle.
Do not redirect all legacy jobs or device tests through this template.

## Reproducible coverage

```sh
MTP_CI_EVIDENCE=/absolute/new/evidence-directory \
python3 -m unittest discover -s build-tools/automation/tests -v
```

With no evidence directory, the tests use an automatically cleaned temporary
directory. The integration tests execute the actual template Python script with
its environment inputs from the repository root, not a mock MTP process.
They build only SourceWriter, the bounded nonproduction `NativeMtpProbe`, and
unchanged AIDL. The probe includes intentional failures/crashes and must not be
selected as a general passing suite.

Coverage includes Debug/Release SourceWriter, all three slice UID multisets,
native project/module execution-only entry points, discovery without test-body
execution, repeated discovery determinism, eight slices over six categorized
cases (two empty), method/compound/escaped filters, duplicate rows/displays,
first-failure recovery (including one of two same-display data rows), exhausted
retries, disabled retries, skipped tests,
first/retry-process crashes, timeout, output/attachment paths, schema/identity
rejection and unchanged NUnit AIDL Debug/Release/root-DLL execution.

Local macOS arm64 coverage does **not** certify Azure task orchestration,
server-side result/attachment publication, agent image/SDK installation, Linux
or Windows execution. The public job is opt-in until those remote checks run.

[filters]: https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests
[slicer]: https://github.com/jpobst/dotnet-test-slicer/blob/e36677d51cff00f81404b8ba8cdacf9215bed7c6/src/DotNet.Test.Slicer/NUnitTestDiscoverer.cs
