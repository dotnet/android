# Mono Android startup metadata

Startup metadata is a diagnostics-only, bounded observation path for instrumented
Mono Android runtime packs. It does not change debugger configuration, expiry
comparisons, retries, connections, waits, timeouts, or initialization results.
It is not enabled by a log level or a device property.

## Build and application opt-in

Build the normal native MonoVM targets with the MSBuild property
`AndroidStartupDiagnosticsBuildId`, forwarded to the CMake cache entry
`XA_STARTUP_DIAGNOSTICS_BUILD_ID`. Its value must match
`[a-z0-9][a-z0-9._-]{0,63}`. An invalid nonempty marker fails configuration;
an empty marker excludes the recorder and Android adapter and compiles the
observation sites to no-ops. The marker is a producer-receipt identifier, **not**
a measured source or binary hash. Do not reuse an existing configured output
directory across different producer markers without reconfiguration. The normal
MSBuild route tracks marker changes as a configure input, validates before any
shell command, and explicitly clears the CMake cache value when disabling.

For an instrumented pack, add an ordinary `AndroidEnvironment` file to the
application containing exactly:

```text
DOTNET_ANDROID_STARTUP_CAPTURE_ID=0123456789abcdef0123456789abcdef
```

Use a newly generated opaque 32-character lowercase hexadecimal identifier for
each capture. It is a correlation identifier, not a secret or authentication.
Do not encode a user name, path, application string, or debugger session ID.
Android looks up this key directly in the linked compiled application environment
array at `Runtime_initInternal`, before existing environment setup. Absent,
malformed, or duplicate compiled keys disable Android observation without
identity reads, clock reads, formatting, or sink writes.

An `AndroidEnvironment` item alone is not proof of this compiled gate:
`_AndroidFastDeployEnvironmentFiles=true` excludes those items from the compiled
environment. It defaults to true when `EmbedAssembliesIntoApk=false`. Qualify the
actual generated `$(IntermediateOutputPath)android\environment.<abi>.ll` for
each ABI and its `app_environment_variables` pointer/value array. Any explicit
change to environment deployment policy must be an approved, retained capture
configuration applied consistently to the comparison; this recorder does not
change that policy. Source environment keys are consolidated last-wins before
generation; the duplicate check protects the compiled array, not source lines.

The runtime debugger component independently reads the effective environment
after Android's existing setup. Debug environment overrides remain unchanged.
An Android-disabled gate does not guarantee a runtime-disabled gate. Consumers
must reject mismatching capture identities rather than treating either side as
proof of the other's participation.

## Observation sites and wire contract

One ASCII JSON object (schema `1`, component `android`) is sent per Android log
message under `mono-startup-meta`, at most 2048 bytes excluding the terminator.
The contract is shared with the separately instrumented runtime component.
There are no raw configuration strings, addresses, application names, pointers,
environment dumps, protocol payloads, or extra socket operations.

`capture_start` samples `CLOCK_MONOTONIC`, `CLOCK_REALTIME`, then
`CLOCK_MONOTONIC` immediately after gate/marker validation, **before** identity
file reads. Its retained bracket is attached to the subsequently acquired
identity. The later `jni_init` begin/end events observe their own sites, not
process birth or an exact JNI invocation instant.

Other Android events are:

| Event | Meaning |
|---|---|
| `config_parse` | Existing debugger argument parser returned absent, parsed, or invalid. No parser behavior is changed. |
| `expiry_decision` | Existing comparison branch and its actual signed wall-clock seconds/deadline; no recomputed deadline. |
| `runtime_options` | `mono_runtime_init` options, profiler, and hook setup, **not** full VM initialization. |
| `runtime_config` | Presence and begin/end of the existing runtime-config call. Its ignored result remains ignored. |
| `domain_init` | Outer domain initialization boundary and null/non-null returned pointer classification. |
| `vm_init` | Exact `mono_jit_init_version` boundary, including a null result before existing downstream handling. |
| `capture_health` | Phase-boundary or reserved cap snapshot, not a lifetime-completion marker. |

Identity includes numeric PID/TID/UID, decimal-string process-start ticks parsed
from `/proc/self/stat`, and a validated boot UUID. Missing start/boot evidence is
explicitly `null` with `identity_status=partial`. Clock failures or reversed
monotonic brackets give `clock_status=partial`. All 64-bit timestamps, sequence
values, and counters use decimal strings, not JSON floating-point numbers.
Observation helpers preserve ambient `errno`; logical outcomes never borrow it.

Each component maintains an independent atomic request ordinal and independent
counters. Ordinals 1 through 256 are eligible for one complete record; failed
formatting or sink writes do not refund a slot. Request 257 is dropped and
substitutes the sole reserved cap-health record at sequence 257. Later requests
only increment attempted/cap-dropped counts. There are at most 257 sink attempts,
no retry, no fallback sink, and no health thread.

Health snapshots contain `attempted`, `writeAccepted`, `writeFailed`,
`formatDropped`, and `capDropped`, sampled before their own eventual write or
format outcome. Concurrent fields are not transactional; no conservation
equation or lifetime-complete accounting is promised. Sequence order is request
allocation order, not syscall, sink, or cross-thread causal order.

Initialization publishes the recorder only after `capture_start`. Concurrent
observations while initialization is in progress are skipped without waiting;
this is an explicit unknown observation window, not evidence of no activity.
Successful Android log API returns prove sink acceptance only, not collector
retention. No events, missing health, process termination, and incomplete
prefixes are **unknown**, not proof that initialization never ran.

## Validation and packaging boundaries

The standalone native tests compile the real gate, identity parsing, clock
conversion, bounded recorder, and health accounting helpers:

```powershell
cmake -S tests\native-startup-diagnostics -B bin\guest-readiness-tests
cmake --build bin\guest-readiness-tests --config Release
ctest --test-dir bin\guest-readiness-tests -C Release --output-on-failure
```

`startup-diagnostics-tests --jsonl` emits synthetic records from deterministic
host callbacks for strict consumer/schema validation, including 64-bit limits.
It is not an Android execution or debugger-readiness test. A separate test
links the disabled public API with no recorder or platform implementation.
`test-build-id.ps1 -MSBuildPath <msbuild executable>` exercises the actual native
MSBuild targets' marker validation and empty/set/change/disable invalidation.
Android target compilation, guest execution, and signed package validation are
separate gates.

A qualified producer must retain baseline and patch identity, the marker,
generated build inputs, ABI, normal pack versions and hashes, signing receipts,
and actual application pack resolution. Build complete normal Android/runtime
packs for both app ABIs; do not replace individual ELF files or extend a host SDK
file overlay. This instrumentation does not authorize publication, installation,
or relaxation of signing/release policy.

## Manual artifact-only producer

`build-tools\automation\azure-pipelines-guest-readiness.yaml` is a separate
manual pipeline with `trigger: none`, `pr: none`, and `enable: false` by default.
It does not import the release graph or request signing grants. Parent review
of the source-owned graph and the Azure service preview is required before a
queue is authorized; adding this file does not authorize a run.

The entry script `build-tools\scripts\guest-readiness-runtime-pack.ps1` returns
a command plan unless explicitly given `-Execute`. Execution requires a clean
committed checkout, the exact reviewed source commit, its binary diff SHA-256
against baseline `d549e1dc4e2a083b08b4f24cb5495e81b99d79b5`, and pinned submodules.
The normal solution preparation/build is a dependency build, not a replacement
SDK for the consumer. Only the two direct normal
`Microsoft.Android.Runtime.proj` builds for Mono/API 36/android-arm64 and
android-x64 create package outputs.

Each native build and pack process receives the global version
`36.1.69-guest.<Build.BuildId>.<System.JobAttempt>` and the producer marker.
Neither `BuildDotNet`, `PackDotNet`, `CreateAllPacks`, nor workload installation
is selected: those convenience routes have broader effects. The entry does
not pass dependency/audit/signature-check bypass switches.

Artifacts include command/build/test logs, source and submodule receipts,
compiler/generated-input hashes, both normal nupkgs, complete ZIP entry
inventories, and actual `dotnet nuget verify --all` outputs. Each package has
`inventory.<id>.json` and `signature.<id>.json` sidecars with the shared
`schemaVersion: 1` inventory/signature kinds. Inventory records use the actual
archive `fileName`, `sizeBytes`, SHA256, and each entry's path, `sizeBytes`, and
SHA256. Signature records bind that archive hash to the actual verifier version,
arguments, exit code, and SHA256 of its combined stdout/stderr log. The producer
root references both sidecars by filename and hash. Failed verification is
retained as `verification-failed` before the producer fails. Unsigned packages
are labeled unsigned; signature presence alone never proves validity, and
signature verification never implies an approved signer. `packageAdmission`
remains false pending approved signing and independent consumer receipts.
There are no feed/BAR uploads, symbol promotion, workload installation, or
guest deployment steps.

The source-specific `producer-receipt.json` is an opaque, unadmitted build
receipt, not a shared provenance schema. Its current fields are `schema: 2`,
`component`, `baseline`, `sourceCommit`, `patchSha256`, `version`, `buildId`,
`normalPackProject`, `planRef`, `submodulesRef`, `compilerInputsRef`, `commands`,
`status`, `failure`, `verificationOutputStreams`, `packages`, `packageAdmission`,
and `executionNotice`. References bind retained files by `fileName` and `sha256`.
Consumers supporting only root schema 1 must reject schema 2 rather than
reinterpret it; the shared package inventory/signature schemas remain version 1.
`compilerInputsRef` is null until compiler input inspection completes.
Each package contains `inventory`,
`verificationExitCode`, `signatureClass`, `inventoryRef`, `signatureRef`, and
`admission`; both references contain `fileName` and `sha256`.

Every invoked test/build/pack/compiler/verification command appends an actual
result to `commands`: `name`, `requestedTool`, `resolvedTool`, `arguments`,
`workingDirectory`, `startedAtUtc`, `endedAtUtc`, `result`, `exitCode`, `error`,
`outputFileName`, `outputSha256`, and `outputStreams`. Results are
`exited-zero`, `exited-nonzero`, or `invocation-failed`; an invocation failure
does not fabricate an exit code. The combined stdout/stderr text log is closed
and hashed before the root receipt is atomically replaced, including before
throwing for a failed command. Post-command validation failures also retain
the root with `status: failed` and `failure: {stage, message}`. Completion uses
`completed-unadmitted`, never a trust or runtime-readiness label.
`plan.json` remains a plan; the old planned-only `executed-command-plan.json`
is no longer emitted or treated as execution evidence.

The inventory bounds archive bytes and total decompressed contents to 2 GiB,
each entry to 512 MiB, and the count to 20,000. The entire metadata set is checked
before decompression; streaming hashing then checks actual versus declared size
and the ZIP's CRC32, including readers that clamp a forged short declared size.
Every entry is retained in ordinal path order, including zero-byte directories
with a canonical trailing slash. Paths are at most 1,024 characters. NFC-normalized,
case-insensitive duplicate paths and file/directory conflicts are rejected, as
are empty/dot/dotdot components, absolute paths, backslashes, drive separators,
control bytes through 0x1f and DEL, trailing dot/space components, and Windows
reserved device names. Archive leaf filenames use at most 240 ASCII
alphanumeric/dot/underscore/hyphen characters with an alphanumeric first character.
The standard .NET `ZipArchiveEntry.IsEncrypted` and `ExternalAttributes` metadata
are used to reject encrypted entries, symlinks, and unsupported file types;
no package is extracted. Signature presence requires the exact `.signature.p7s`
file name; case aliases and directory impostors fail validation. These checks
do not establish signature trust or source/build provenance.

For the selected RID, the native subtree also has exactly two permitted
generated host-metadata paths under `runtimes/<RID>/lib/netstandard2.0/`:
`Microsoft.Android.Runtimes.deps.json` and
`Microsoft.Android.Runtimes.runtimeconfig.json`. The held SharedFramework SDK
generates them even for Mono native-only packs; actual producer PackTask items
and the held stock archive agree on those paths. Their bytes receive the same
size, CRC and SHA256 checks as every other member. Only their canonical empty
ancestor directories are additionally permitted, not arbitrary managed files,
other JSON names/TFMs, case aliases or another RID.

`test-runtime-pack-producer.ps1` exercises the real command-plan and bounded ZIP
inventory helpers using actual malformed ZIP files, exact-limit cases, and
explicitly synthetic packages. It also launches real child processes to check
combined output, exit codes, invocation failures, and persisted log hashes.
`test-stock-runtime-pack-layout.ps1 -StockPackage <held-x64-36.1.69.nupkg>`
checks all 24 entries of the byte-unchanged stock archive. Only its temporary
module copy models the expected-version predicate; the test verifies exactly one
substitution and unchanged remaining code. Production rejects the held version
both before and after. This validates layout/CRC/hashing, not diagnostic
admission or signature trust.
`test-producer-graph.py`
uses PyYAML to parse the complete source-owned YAML and checks the selected
normal package/version target shapes. Neither test is an Azure service preview
or evidence that full normal packages have been produced.
Synthetic inventory and signature sidecars are retained under
`bin/guest-runtime-pack-fixtures/`; their verifier outcomes are test inputs,
not results of signing or verifying those synthetic packages.

### Existing official pipeline diagnostic mode

The existing `build-tools/automation/azure-pipelines.yaml` has a default-false
`guestReadiness` parameter and a bounded positive decimal `guestReadinessAttempt`
(default `1`). This is not a new signing definition. Enabled execution requires
manual `Xamarin.Android` definition 11410, repository `dotnet/android`, a non-release
`refs/heads/` source, `Skip1ESComplianceTasks=false`, an exact checked-out commit
descending from the held baseline, and actual resolved template commits.
Publish the reviewed additive source to a dedicated upstream feature ref before
selecting that ref and exact commit in the existing definition. An unpublished
fork branch is not its `self` source; a fork PR's signing eligibility is not
established. Publication and execution still require separate authorization.

The ordinary macOS/Linux `make jenkins` and `create-installers` route remains
responsible for Prepare, compilation and the full SDK/runtime package set.
`create-installers` is the existing alias of `create-nupkgs`. The build wrapper
passes `AndroidPackVersionLong`, `PackageVersion`,
`AndroidStartupDiagnosticsBuildId`, `AndroidGuestReadinessBuild` and `RunningOnCI`
through `MSBUILD_ARGS`. `GuestReadinessPackProperties.targets` forwards the same
globals through the existing `CreateAllPacks` nested processes, checking that
version `36.1.69-guest.<Build.BuildId>.<attempt>` and marker
`android-d549-<Build.BuildId>-<attempt>` agree. The attempt is shared across jobs,
not derived independently from their retry counts.

The diagnostic version is also preserved at the actual `GetXAVersionInfo`
target, not only forwarded as a global property: MSBuild target-time assignments
can replace command-line values. Pack projects validate the version/marker/config
before that target. Ordinary builds retain their Git-derived version calculation;
diagnostic builds retain the same numeric `AndroidMSIVersion` calculation for
MSI/workload consumers, separately from the unique prerelease NuGet identity.

Run `pwsh -NoProfile -File tests/native-startup-diagnostics/test-pack-version.ps1`
for the version-authority regression. It compiles the unchanged production Git
tasks, evaluates the real runtime pack project and imported shared-framework SDK,
runs the real version targets, and invokes the SDK's real NuGet `PackTask`.
Both app RIDs cover diagnostic, unset and explicit-false modes, checking archive
names, nuspec identities, numeric MSI values and invalid diagnostic inputs.
The package payload and dependency graph are test fixtures, not built Android
binaries; these tests establish version authority, not a full pack, signing,
restore/audit qualification or guest execution.

On Linux execution hosts, the diagnostic Build/Pack wrapper passes an explicit absolute
`RestoreConfigFile` for the unchanged repository-root `NuGet.config`, and retains
its byte-identical hash-bound copy. The existing nested pack global-property
forwarder carries that path into its child processes and rejects missing config
files on Windows/Linux execution hosts. The check uses the executing OS, not
`HostOS` (a Mac can produce Windows SDK packs). macOS retains ordinary config
discovery: the retained successful Mac Build retrieved the NuGet vulnerability
index and base/update data, unlike the blocked Windows/Linux requests. That
observation establishes retrieval there, not whole-solution audit closure.
This prevents the held debugger dependency's nested `<clear />` from
replacing the approved root feeds with nuget.org; it does not change package
versions, ignore failed sources, relax signature checks, or change network rules.
Default-off builds retain their ordinary config discovery.
The diagnostic Windows job uses the same MSBuild property through its scoped
`RestoreConfigFile` pipeline variable. Azure exports it as `RESTORECONFIGFILE`;
MSBuild and child processes inherit it for the unchanged Prepare/Build/Pack
commands. Its value uses `$(Build.Repository.LocalPath)\NuGet.config`, the actual
single-repository Windows checkout, not the macOS/Linux `s/android` layout.
Tests check the case-sensitive Git filename `NuGet.config`, ordinary nested
MSBuild environment propagation, and real NuGet config selection; no global
NuGet policy or Windows package-version override is introduced.

Immediately before Smoke tests, diagnostic Windows also configures the exact
`ANDROID_GUEST_READINESS_TEST_ACQUISITION=1` opt-in for test-process acquisitions
that bypass MSBuild/Gradle. The step requires the existing absolute root
`RESTORECONFIGFILE` and uses the harness's `NUNIT_MSBUILD_ARGS` to pass its quoted
path as a global restore property to nested builds. A pre-existing nonempty
`NUNIT_MSBUILD_ARGS` fails rather than being discarded; malformed opt-in/config
values fail explicitly. Ordinary mode and macOS retain their existing paths.

The explicitly enabled diagnostic Linux test jobs `linux_tests_smoke_1` and
`linux_tests_smoke_2` also run this setup, with `-AllowLinux` and their actual
`$(System.DefaultWorkingDirectory)/NuGet.config`. This is an intentional extension
of diagnostic test acquisition, not a claim that diagnostic Linux behavior is
unchanged. The shared helper uses actual Linux identity, not `PlatformID.Unix`
(which can also identify macOS). Linux root-path validation is case-sensitive;
Windows retains case-insensitive validation. Default-off jobs get neither the
new variables nor setup task. The changed helper is built into normal test
assemblies by the producer; no downloaded assembly is replaced.

`Builder` consumes `NUNIT_MSBUILD_ARGS`; `DotNetCLI` does not. Its fresh child
process inherits `RESTORECONFIGFILE` as an MSBuild environment property. Separate
Windows characterizations with the installed local `10.0.400-preview.0.26356.102`
and exact held consumer `10.0.401` restored a genuine existing local package
through both that environment route and the explicit property, retaining the
intended config path with spaces in each actual dgspec. The held consumer check
pins `global.json` with roll-forward disabled and records each child's actual
SDK version/base path. These prove the Windows mechanisms, not actual Linux
CI/producer SDK identity or global-property precedence: project properties can still
override environment properties. No `DotNetCLI` wrapper change is made.

The diagnostic Linux test setups and MAUI integration setup pass an explicit
config only to the existing apkdiff installer. Linux uses the same root above;
MAUI uses `$(Build.SourcesDirectory)/android/NuGet.config`, not the MAUI checkout.
The installer replaces its ordinary `--add-source` argument with a quoted
`--configfile` when the optional path is nonempty. Empty preserves all ordinary
arguments, and test-slicer is untouched. `apkdiff` remains `0.0.17`; this exact
version was listed by the already-approved `dotnet-public` feed. No global tool
installation, new source, fallback, credential or package-version change is
introduced by this plumbing.

**Audit-data qualification remains blocked.** The failing Linux dgspecs record
`enableAudit=true`, `auditLevel=low`, `auditMode=all`, and user-config nuget.org
discovery. Their NU1900 warnings are real vulnerability-data acquisition
failures, not cosmetic warnings. The unchanged repository config has no explicit
`auditSources`; the observed `dotnet-public` service index advertises no
`VulnerabilityInfo`. Warning-free restore from that mirror therefore does not
prove auditing. Retain effective package sources, explicit audit sources or
package-source fallback, audit settings, advertised resources and actual data
retrieval outcomes separately. No audit disable, warning suppression,
ignore-failed-source setting, fabricated data, or network-policy approval follows
from these setup changes; test assertions remain unchanged.

For this setup delta, compare the actual service preview against `01402b` using
`--require-linux-test-setup --require-root-observer --expanded-preview <new>
--baseline-preview <01402b-preview>`. Only the two Linux setup insertions/root
variables, their derived pre-job `StepsLength` changes (23 to 24 for
`linux_tests_smoke_1`, 32 to 33 for `linux_tests_smoke_2`), and three apkdiff
argument changes may differ. Each count increase requires exactly one verified
Configure task insertion in that named job. Every other pre-job argument, task,
target, guard and graph object remains exact; counts in other jobs are not
normalized. Actual service-preview validation establishes this derived metadata;
the corresponding model is not itself a service preview. Existing tests cover
source default-off graphs, exact quoted config paths, malformed inputs, host
identity and inherited child properties. Hosted Linux results and audit-data
qualification remain distinct from Windows local checks.

`DownloadedCache` maps only the exact HTTPS Maven Central `/maven2/` origin with
ordinary artifact path segments to the existing anonymous `dotnet-public-maven`
mirror, preserving suffix/version, filename and cache behavior. Query/fragment,
credentials, alternate origins/ports and encoded/traversal paths are not remapped.
No retries or fallback sources are added. Diagnostic `DotNetBuild` does not append
its ordinary nuget.org source. The analyzer, code-fix, refactoring and suppressor
test wrappers use the held analyzer-testing 1.1.2
`ReferenceAssemblies.WithNuGetConfigFilePath` API with that same root config,
without changing reference packages/frameworks/versions. Unavailable packages,
including the test's existing floating WebView requirement, remain real failures:
there is no substitution, cache seeding, test skip or network-policy relaxation.

The diagnostic Windows/Linux execution paths also pass
`--init-script ".../guest-readiness-repositories.gradle"` through the existing
`GradleArgs` environment/MSBuild property. The script routes Maven repositories
for settings plugins, settings dependencies/buildscript, and project
dependencies/buildscript to the anonymous `dotnet-public-maven` feed. This is
the CFSClean mirror used by immutable successful source
`f1052b05df0157cc4ed10b1151b34b31b8cfcae2` in
`eng/gradle/{plugin,dependency}-repositories.gradle`. Lifecycle hooks apply before
settings/project evaluation, including the unchanged Java.Interop submodule.
The Linux wrapper retains the script bytes in its existing receipt, preserves
existing Gradle options, and restores its environment even when Make fails.
The Windows diagnostic job uses its actual self-checkout path. No implicit
conversion of MSBuild `RunningOnCI` into an environment variable is assumed.
Mac and default-off builds keep their ordinary Gradle repository selection.
No dependency/plugin versions, integrity checks, credentials, cache seeding or
network policy are changed.

`test-gradle-repositories.ps1 -JavaHome <existing-jdk>` exercises the held Gradle
wrapper offline with ON/OFF settings/project/child-project repository fixtures
and paths containing spaces. It also evaluates the three actual affected
MSBuild projects with uppercase environment input and unchanged defaults.
Separate real-target validation built/copied `java-source-utils.jar`,
`manifestmerger.jar` and the normal ProGuard rules using their unchanged MSBuild
targets and anonymous mirror. These are host-build observations, not isolated
CI, whole-solution audit, signing or guest-execution qualification.

Diagnostic Windows builds configure `CustomAfterMicrosoftCommonTargets` through
a job variable before Prepare. The source-owned common import registers its
CoreCompile predecessor before Guardian's CSharp import. Only when Guardian is
present and `ErrorLog` is empty, it selects
`bin/guest-readiness-roslyn/<project>.csproj.<fresh-guid>.gdn.sarif,version=1.0`.
This prevents concurrent compilations (including the same project/TFM/configuration
and nested submodules) sharing Guardian's default project-relative file. Explicit
`ErrorLog` values and NuGet's task-level restore-hook override remain authoritative;
Configure rejects an existing environment hook instead of replacing it.
No analyzer, ruleset, scanner filter, clean-tree check or submodule source changes.
Mac/Linux and default-off Windows retain their original behavior.

Before the existing Build Results upload, an always-running capture step copies
the exact owned GUID leaves and, if present, the unexplained checkout-root `.sarif`.
It records absence/presence, original path, size, write time and SHA-256 in
`capture.json`. Original files are never moved, deleted or ignored. Regular files
only, at most 64 MiB each, 10,000 owned leaves and 2 GiB total, are copied with
concurrent writers/deletion denied. Unexpected entries fail without a success
receipt. Every existing source/destination ancestor is checked for reparse points
before creating directories or copying files, including nonexistent descendants
of junctions. The copies are observations, not claims of scanner processing. The
unchanged collector still discovers the original GUID logs beneath the source
root with its existing `*.csproj.*.sarif` wildcard. Historical bare `.sarif` bytes
and their writer were absent from the two selected Windows artifacts; if present
at capture, its bytes are retained but the ordinary clean-tree gate still fails.

The early capture can miss a root `.sarif` created later. A separate DIAGWindows
observation runs after SDL analysis and the existing Build Results publisher,
immediately before the unchanged clean-tree check. It logs only presence,
size, SHA-256, creation/write UTC times and a fixed phase/schema/health record
for that exact file. It does not parse, print, copy or upload its contents, and
creates no files. The same full ancestor reparse checks apply; a regular file
of at most 64 MiB is hashed with writers/deletion denied, comparing size and
timestamps before and after the read. Healthy absence describes only that
observation, not the entire job lifetime.

Unsafe, oversized, busy or changing files produce a fixed `unavailable` reason
and nonzero process exit, without raw exception details. Only this new
diagnostic task uses `continueOnError: true`: Azure's `SucceededWithIssues`
still satisfies the existing clean check's `succeeded()` condition. An earlier
failed task is not made successful, and no existing task condition is changed.
The normal SDL log publisher remains **after** the clean check. A later hash
match to those published bytes is content evidence, never proof of the creator.
`test-root-sarif-observation.ps1` exercises the real process, exact JSON/exit
contract, 64 MiB boundary, active writer and leaf/ancestor junction rejection.
The graph regression's `--require-root-observer --expanded-preview <file>`
also enforces the placement in an actual service-expanded preview; source and
modeled graph checks alone do not claim hosted execution.

DIAGWindows also fixes the single SPMI task's optional user-copy destination with
`GDNP_1ESSECRETSCANNING_OUTPUT=$(Agent.TempDirectory)\guest-readiness-spmi.sarif`.
The retained `1ESSecretScanning` 1.86.0 task defaults its **file** Output input
to the checkout directory with a trailing separator. Exact Guardian
0.293.26265.1 metadata/IL shows that it preserves this input as a user-copy
location, gives the scanner its normal `.gdn\.r\...\sarifpatternmatcher.sarif`
path, and later changes the copy destination's extension to `.sarif`.
For the directory-valued default, this produces checkout-root `.sarif`.
The explicit filename changes only that optional copy; scan targets, arguments,
raw results, normal SDL processing/publication and the clean gate are unchanged.
This mechanism explains the filename, not historical process attribution:
the observed root hash matched none of the 156 published SDL files.

This is a closed diagnostic experiment, not a general override-preservation
feature. The task preserves an existing environment value, including empty,
but Azure job variables shadow inherited stage/root/UI values before execution.
Before queueing, inspect definition/queue variables and referenced groups for
the name and aliases normalized by uppercasing and replacing periods with
underscores; an existing conflict or required unreadable group blocks admission.
The retained rendered baseline has no such override and exactly one Windows
SPMI invocation. Multiple invocations sharing this filename are not supported:
Guardian's duplicate-name suffix can converge again during extension replacement.
Existing filesystem collision/overwrite behavior is not changed or bypassed.
Default-off Windows and Mac/Linux do not receive the variable.

`test-spmi-output-path.ps1 -TaskDirectory <retained-exact-task>
-GuardianProofDirectory <retained-output-proof>` checks retained source facts,
actual Windows `Path` values and explicitly modeled environment precedence;
it never executes acquired task/Guardian code. Use the graph regression with
`--require-spmi-output --require-root-observer --expanded-preview <new>
--baseline-preview <0cbc-preview>` to require whole-provider-object equality
after removing only this one Windows job variable. Source/model checks are not
hosted proof. Any later root absence remains a point-in-time observation.

DIAGWindows sets the existing `XA.PublishAllLogs` control to the string `'true'`
at job scope. Without it, healthy tests skip normal Build Results retention and
its attached SDL scans: a clean checkout and absent root `.sarif` then do not
exercise the SPMI copy correction. This setting enables the existing successful
build retention path, including captured GUID evidence, without adding a
publisher or changing artifact/scan inputs. All 15 existing retention conditions
remain identical. Only their inner predicate changes for `Succeeded`;
`SucceededWithIssues`, failure and cancellation already satisfy that predicate.
Separate `succeeded()`, fork, enforced-tool and injected-ruleset guards remain,
so this is not a promise that every scanner runs after failure or cancellation.
`ONEES_HASACTIVESDLTASK` is not forced; normal final SDL publication still depends
on actual activation. The late observer, clean gate and later publishers stay
in place; Mac/Linux/signing and default-off Windows are unchanged.

This closed diagnostic job constant intentionally takes precedence over inherited
values. Before queueing, check definition, referenced-group and queue names for
both `XA.PublishAllLogs` (normalized `XA_PUBLISHALLLOGS`) and the SPMI output
variable. No existing root parameter or queue-override permission is assumed.
For this delta, use `--require-windows-retention --require-root-observer
--expanded-preview <new> --baseline-preview <36e62-preview>` instead of the older
`--require-spmi-output` delta check. It requires whole-provider-object equality
after removing only the retention variable, including unchanged pre-job length,
accepting either its string value or the actual provider's unquoted `true`
scalar rendering for this exact variable only. Source YAML must still contain
the quoted string `'true'`; a source boolean is rejected separately. No other
provider field is normalized, and false values, aliases and additional fields
remain invalid. The retained actual service preview, not a synthetic rendering,
qualifies this serialization distinction. The equality check also preserves
one SPMI invocation and its fixed output, all guards and all other jobs.
Predicate tests are models, not hosted execution. Qualification still requires
actual scanner/report execution, published raw/processed evidence, and the
point-in-time root observation plus unchanged clean result.

`test-roslyn-output.ps1 -GuardianTargets <retained-injector-target>
-GuardianCli <existing-1.24.0-cli>` runs three real concurrent SDK Csc invocations,
checks NuGet restore traversal, explicit/OFF behavior and bounded capture. The
optional actual Guardian copy/sanitize run checks one-to-one GUID leaf mapping,
valid SARIF 2.1 and preserved real CA5350 findings/messages/source locations.
Expected filtering of ordinary CS1030 warnings is not treated as evidence loss.
This exercises the installed SDK security analyzer and retained injector target,
not the hosted Guardian ruleset merger, a full product build or policy acceptance.

No audit setting is disabled or overridden. The approved `dotnet-public` service
index inspected for this change exposes no `VulnerabilityInfo` resource, so root
feed selection alone does **not** establish vulnerability-data coverage. Actual
transitive restore, signature validation, and audit-data availability remain
separate qualification gates; an authorized audit source or existing approved
audit mechanism must supply that evidence. The newer main pipeline's audit
disabling variables are not copied into this held-baseline diagnostic mode.

The SDK project supplies its existing `SignList.xml`; runtime-only packing is
not substituted. Both existing signing jobs, their Test/Real predicate,
compliance/security steps, and ordinary artifacts remain.
Diagnostic mode selects the **Official MicroBuild/1ES envelope** required by
definition 11410, independently of the unchanged **Test** signing branch selected
by the manual non-release gate. Successful main build 15420423 at
`f1052b05df0157cc4ed10b1151b34b31b8cfcae2` demonstrates this combination.
The collector hashes the selected Official entry from the resolved MicroBuild
checkout, not the inactive Unofficial entry. No breakglass or security suppression
is added. Default-off envelope selection remains exactly as on the held baseline.

Diagnostic mode overrides `HostedMacImage` and `HostedMacImageWithEmulator` to
the explicit `macOS-15` label used by that successful build, after loading the
baseline variables. The normal Azure Pipelines `vmImage` pool declarations remain
unchanged; the pinned MicroBuild/1ES templates preserve this supported property.
This qualifies a new producer host/toolchain, not reproduction of the baseline's
historical `macOS-14-arm64` host. Windows/test pools and ordinary commands remain unchanged.
Only producer pipeline hosts change: consumer macOS, Android emulator image and
workload pins, SDK bits, and the held source baseline are not upgraded.
A preview alone does not establish runtime template admission or image
availability; the actual run must qualify both. Only the two Android Mono native packs from
the macOS artifact receive the new post-sign evidence; the Linux SDK signing job
is unchanged. The diagnostic graph omits the conversion and
`push_signed_nugets` jobs at template-expansion time, including their BAR/Maestro,
Darc and symbol-promotion operations. This is not a false runtime push variable.
The full SDK signing artifact is retained, not installed as an expanded SDK overlay.

The official wrapper emits a distinct root contract:
`schema: 2`, `kind: android-official-guest-readiness-phase`. Its fields are
`component`, `phase`, `baseline`, `sourceCommit`, `version`, `buildId`,
`patchSha256`, `definitionId`, `pipelinePath`, `sourceBranch`, `buildNumber`,
`jobName`, `jobAttempt`, `requestedSignType`, `templates`, `commands`, `files`,
`packages`, `status`, `failure`, and `packageAdmission` (always false).
It is not the standalone producer's root decoder. Schema1 consumers must reject it.
The existing actual-command ledger described above is reused, including failure
logs before throwing. Phase-prefixed log names prevent Pack from overwriting
Build command evidence. Root files are `build-receipt.json`, `pack-receipt.json`,
`input-receipt.json` and `output-receipt.json`; in the signing evidence directory,
`build-receipt.json` is a byte-identical alias of the downloaded **pack** receipt.
Source patches are `<phase>.source.patch`.

Before packing, the wrapper validates the earlier Build root's completed command
ledger, closed log hashes, exact normal build arguments, source/version/marker and
resolved templates. It retains a byte-identical copy as
`build-phase-receipt.json`, hash-linked in the Pack root's existing `files` array.
This distinct leaf prevents a cycle with the signing directory's `build-receipt`
alias of the Pack root. Nested references resolve in the family's original build
evidence directory. `SignList.xml` is also copied byte-identically into that
directory and hash-linked; the original signing input is untouched. Existing
different destination bytes, missing sources or a copy hash mismatch fail.

Build/Pack evidence is published as
`guest-readiness-<Agent.OS>-Build` and `guest-readiness-<Agent.OS>-Pack`.
The diagnostic steps use the existing `1ES.PublishPipelineArtifact@1` publisher
with `condition: always()` so failed command receipts remain available. The
ordinary `PublishPipelineArtifact@1` task is not permitted by this 1ES envelope.
Artifact names remain exact (no retry suffix) for the signing job's download.
Before layout classification, Pack retains only the two exact expected runtime
archive leaves, bounded to regular nonempty files of at most 2 GiB each.
`unadmitted-runtime-candidates.json` records their sizes/hashes and explicitly
labels them unadmitted. A failed layout/marker/signature check still fails the
phase; retained raw bytes are failure evidence, never a substitute for a valid
inventory or signing/admission evidence.
Pack retains ordinary package hashes, complete native inventories, raw
`buildtoolsinventory.csv`, `Configuration.props`, `Configuration.Generated.props`,
and per-ABI Debug/Release `CMakeCache-<abi>-<configuration>.txt` plus
`CMakeCCompiler-<abi>-<configuration>.cmake` and
`CMakeCXXCompiler-<abi>-<configuration>.cmake`. Cache validation accepts actual
CMake type spellings, including `UNINITIALIZED`. These are raw generated bytes,
not reconstructed property summaries.

The signing hooks retain `guest-readiness-sign-Input`,
`guest-readiness-sign-Output`, and `guest-readiness-signed-output`, including
produced archives when subsequent standard verification fails. Input capture
and its byte-preserving snapshot still occur before signing, including on Input
failure. All three uploads occur in the Output hook after the normal signing
sequence: the supported 1ES publisher injects binary scans before each upload,
so an early Input upload would scan signed-output targets before they exist.
The artifact-bound AntiMalware target follows the exact byte-identical Input
snapshot directory (`guest-readiness-sign` to `guest-readiness-sign-Input`).
BinSkim and signing targets, scanner policy, conditions and intended coverage
remain unchanged. The expanded-preview regression permits only that single
Input-publisher-bound AntiMalware path transition; all other protected task
fields and the other 48 protected steps must remain identical.
For each package,
they emit `input.inventory.<id>.json`, `output.inventory.<id>.json`,
`output.signature.<id>.json`, `member-delta.<id>.json`,
`native-provenance.<id>.json`, and `postsign.<id>.json`. Shared inventory/signature
schemas remain version1. Postsign uses the shared `operationEvidence` references
to actual normal signing binlogs and the completed verifier ledger, plus
`memberDelta` and `nativeProvenance`. Its status is
`verified-policy-unqualified` or `produced-verification-failed`; neither admits a
package. The latter retains standard verifier exit code, SDK version, exact
leaf argument/cwd and combined stdout/stderr log hash, then fails the hook.
The normal NuGet repack task's provider result is not captured by this helper;
the delta explicitly states that limitation rather than fabricating a receipt.

The Output provenance checkout and evidence step also use `always()`, not just
the publishers. Evidence reads the normal signer's
`$(Agent.TempDirectory)/artifact-signing/packed` directory directly: ordinary
verification precedes `Copy Signed Output`, so a failed verification can leave
`signed` absent. Only the two exact expected runtime archives with a signature
entry are copied, byte-identically, into
`$(Build.ArtifactStagingDirectory)/guest-readiness-signing-output` for retention.
Signature presence is not verification or trust. Missing packed outputs or
signature entries fail explicitly; unsigned inputs are never substituted.
Fresh standard-verifier results and logs are retained, without inventing the
earlier task's exit code. A prior job status other than `Succeeded` remains a
failure even if fresh verification succeeds. The ordinary signer, verification,
and copy steps are unchanged.
Before template/tool provenance checks, Output also retains the two exact
bounded raw normal-packed candidates in the **evidence** directory, with
`unadmitted-runtime-candidates.json`; these raw copies are not published as
signed output. The sidecar explicitly lists missing candidates, including both
when signing never produced any archives. Missing provenance still fails the
root without creating package/admission records; it no longer prevents retaining
existing raw bytes. Unsigned inputs are never used as a fallback.
`output-signing-context.json`, referenced by the Output root's existing `files`,
records the exact pre-hook `Agent.JobStatus`, observation phase and UTC time.
This is not the final provider job result; the owner must qualify that from the
actual run timeline. Post-sign verification evidence references the finalized
Output root, never the reverse, avoiding a circular hash dependency.

Native provenance independently hashes and reads the complete source-owned
`runtimes/<rid>/native/libmono-android.debug.so` and
`libmono-android.release.so` carriers before and after signing. It checks bounded
ELF64 little-endian headers, machine 183/62, and a NUL-terminated compiled marker
with a streaming overlap window, and links the six actual generated compiler/cache
files per RID. `gnuBuildIdStatus: not-collected` makes no GNU-note claim.
Member deltas describe all added/removed/changed entries, not a signing allow-list.
Keep original build evidence, unsigned archives and final archives in separate
owned directories; same-leaf pre/post packages must never overwrite one another.

Template provenance uses actual resource versions and checkout bytes for
`yaml-templates` (`DevDiv/Xamarin.yaml-templates`, declared `refs/heads/main`) and
`1esPipelines` (`1ESPipelineTemplates/MicroBuildTemplate`, existing default ref).
Diagnostic mode adds the checked-out `1esPipelines` alias to SDL's
`sourceRepositoriesToScan.include`; existing scan coverage and the default-off
SDL configuration are unchanged. The provenance checkout is not removed or excluded.
The reviewed snapshots are not hard-coded historical pins. MicroBuild's transitive
`1ESPipelineTemplates` release-tag resolution is not exposed by this helper:
the owner must capture/pin qualified provider resolutions and compare actual run
resources. An Azure preview without a resource-resolution field does not supply
that evidence. Test-job certificate setup also does not establish unmodified
consumer trust. No signing grants, certificates, trust-store edits or audit
bypasses are added.

`test-official-producer-graph.py` proves parsed default-off graph equality in the
four modified root/build templates and diagnostic promotion omission with the
existing signing/security/full-pack graph preserved. This is not a full external
1ES expansion. `test-official-producer.ps1` exercises actual source gates, nested
MSBuild OFF/ON/error forwarding, real ZIP bytes with explicitly synthetic ELF
headers, streaming marker boundaries, member deltas, and shared receipt
construction. It also invokes the actual standard NuGet verifier on an invalid
synthetic signature and retains its failure ledger. Synthetic success-status
constructor fixtures are not successful verification or signing evidence.
The official test runs `test-runtime-pack-producer.ps1` in an isolated PowerShell
process to recreate its ZIP fixtures and checks its exit code.
Evidence is retained in `bin/guest-readiness-official-tests/`.
