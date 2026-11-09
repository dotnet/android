# Opt-in native MTP host CI

The public pipeline's **Run opt-in SourceWriter native MTP host tests**
(`runNativeMtpHostTests`, default `false`) adds a separate, three-slice Linux host
job. `skipTests` still skips it. Only `tests/Xamarin.SourceWriter-Tests` is built
and selected. No Android SDK, emulator, instrumentation runner, global runner
switch, or product package change is involved. Internal/nightly and existing
NUnit jobs are unchanged unless the separate isolation option is selected.

**Run only SourceWriter native MTP host tests**
(`runNativeMtpHostTestsOnly`, default `false`) enables the same pilot and disables
every unrelated stage, including template-defined package and Android Tools
tests. Use this option for pilot-only validation without `stagesToSkip`.
It does not repurpose `runNativeMtpHostTests`: that flag still adds the pilot
to the existing matrix when the isolation option is false.

| Additive flag | Only flag | `skipTests` | Selection |
| --- | --- | --- | --- |
| false | false | false | Unchanged legacy matrix; no pilot |
| true | false | false | Legacy matrix plus pilot |
| either | true | false | Three-slice pilot only |
| either | true | true | No tests or builds; pilot is skipped too |
| either | false | true | Existing skip-tests behavior; pilot skipped |

Both true is valid and means pilot-only. `skipTests` always wins for the
pilot, so use `skipTests='false'` for an isolated acceptance run. The defaults
and additive mode preserve all existing non-pilot stage conditions.

## Prerequisites and build boundary

The pilot uses SDK `11.0.100-rc.3.26522.148`, installed in a job-local directory,
MSTest and MSTest.SourceGeneration `4.6.0-preview.26524.1` and
MTP/TRX `2.6.0-preview.26524.1`.
The latter pins belong to the converted project, not the root package policy.
The adapter needs Python 3.9+ and the matching .NET runtime.

The Linux/macOS execution template explicitly selects installed `python3`,
checks that it is at least 3.9, and passes its absolute path through
`PythonScript@0`'s `pythonInterpreter` input. It does not rely on that task's
default `python` lookup or assume this custom agent pool has a Python tool
cache. A missing/unsupported interpreter fails before execution; it is not
downloaded implicitly. Windows use of the Bash-based template is not certified.
The pilot stage includes `succeeded()` in its condition so cancellation cannot
leave its otherwise-true opt-in condition running.

SourceWriter and the bounded probe both reference `MSTest.SourceGeneration`
directly with `PrivateAssets="all"`; the probe also persists compiler-generated
files for its contract checks. They use the package's default mode on
`Microsoft.NET.Sdk`, without an extra mode property or experimental engine.
The host integration checks source-generated registry/support/ModuleInitializer
output and the real native probe's active `SourceGeneratedReflectionOperations`
provider. Generated failure stacks independently prove direct invocation.
The probe verifies its complete intended test-method registry and generated
DynamicData source/display-name accessors. This is generated metadata/provider
activation, **not** a claim of universally reflection-free execution:
the framework retains bounded startup lookups and compatibility fallbacks.
No AOT, trimming or device validation is implied.

The exact SDK pin was verified on 2026-11-05 against the metadata actually used
by `UseDotNet@2`: its [release index][sdk-index] links the 11.0
[`releases.json`][sdk-releases], which lists this exact SDK and a
`linux-x64` `.tar.gz` file. The task's actual `getMatchingVersionFromList`
function, from azure-pipelines-tasks commit
`3cdc037ed3702e99641e536ed7512b3f873b9d1d`, selected this exact version with
`includePreviewVersions: true`; its Linux archive URL returned HTTP 200.
This is metadata/resolver verification, not inference from a local installation
or an archive download. No custom daily-feed installer is needed.

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

Each native process runs in an owned POSIX session/process group. On timeout
the adapter sends SIGKILL to that specific group, terminating inherited children
and grandchildren as well as the host, then reaps the direct child. It never
kills by executable name or touches the agent's group. Windows starts a separate
process group and uses `taskkill /PID <owned-root> /T /F` for tree cleanup.
Cleanup failures are explicit infrastructure errors, not ignored successful
timeouts; the owned PID/group and cleanup outcome are retained in
`command.json` alongside stdout/stderr and diagnostics. Interrupted waits also
clean the owned tree before propagating interruption.

The real spawned-child regression has been exercised on macOS, with a child
and grandchild heartbeat plus an unrelated sibling process that must survive.
The Windows `taskkill` branch and Linux group cleanup have **not** been run here.
POSIX children that deliberately detach into a different session/group escape
this ownership boundary; tests must not daemonize unmanaged background work.

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
Source-generation changes must rediscover and revalidate this mapping, not
reuse pre-generator UID lists even if observed GUIDs remain unchanged.
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
It first rejects existing results paths, including symlinks. After execution,
both publishers require a directory created by this eligible invocation.
If interpreter selection or task launch never produced a fresh directory,
publication is explicitly skipped with a warning; the earlier failure remains
gating. Existing output cannot be republished as this invocation's success.
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
Timeout coverage includes a real native test spawning a child and grandchild,
both terminated while an unrelated sibling stays alive; no effective success
report is emitted.
Local YAML task-contract checks also execute the actual Bash selection and
publication-precondition scripts. They check the selected absolute Python path,
version/missing-interpreter failures, stale-directory rejection and absent-output
publication suppression. The actual YAML-input invocation uses that selected
`pythonInterpreter`, rather than substituting the verifier's own executable.
Generated-mode coverage additionally includes deterministic `DynamicData`
properties with genuinely identical argument rows and duplicate display names,
six slices over four distinct row UIDs (two empty), native method/display/category
filters, and retrying exactly one of two same-display dynamic rows. Both
discovery and retry run through the actual generated probe, not a mock metadata
model. Generated registry and provider-activation output are retained beside
the current evidence; earlier non-sourcegen results are historical, not current
mode certification.

Local macOS arm64 coverage does **not** certify Azure task orchestration,
server-side result/attachment publication, agent image/SDK installation, Linux
or Windows execution. The public job is opt-in until those remote checks run.

[filters]: https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests
[slicer]: https://github.com/jpobst/dotnet-test-slicer/blob/e36677d51cff00f81404b8ba8cdacf9215bed7c6/src/DotNet.Test.Slicer/NUnitTestDiscoverer.cs
[sdk-index]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json
[sdk-releases]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json
