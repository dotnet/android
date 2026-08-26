# Host build tests in Helix prototype

Tracking: [AB#12334](https://dev.azure.com/dnceng/internal/_workitems/edit/12334)

This guarded prototype stages the already-prepared .NET SDK/workload, Android SDK,
JDK, selected Gradle caches, repository inputs, and test assemblies as shared Helix
correlation payloads. Individual payloads are capped at 1 GiB to avoid the Helix SDK's
in-memory ZIP limit. Work item payloads contain only a runsettings filter, a small
command script, and generation metadata. To control total payload size, the source
copy omits `src/Mono.Android` and the NuGet package cache; host build tests consume the
product through the prepared workload packs and use isolated per-work-item NuGet
caches. Device-only Android SDK content, SDK documentation, the Java.Interop checkout,
and unrelated test assemblies are also excluded. The prepared NDK and the local
`nuget-unsigned` feed remain shared payloads because host tests consume both directly.

`prepare-host-build-test-helix-payload.ps1` discovers NUnit tests with the repository's
`dotnet-test-slicer`, reads its `balance.xml` format, and uses deterministic best-fit
decreasing bin packing. The target is `15` estimated wall-clock minutes by default.
`durationParallelism` translates summed per-test elapsed time into expected wall time
while preserving the current NUnit worker setting. The public prototype defaults this
factor to `1.3` on Windows and `1.65` on Linux, uses `60` seconds per test for the
explicit first-run fallback, and allows `90` minutes before Helix terminates a
first-run work item.

If no timing history is provided, generation explicitly reports `count-fallback` and
uses the configured fallback duration per test. It does not label count-only slicing
as duration-balanced. Each completed run publishes a new `balance.xml`; pass that
run's build ID through `hostBuildTestsHelixTimingBuildId` on the next queued build.
TRX, console output, runsettings, work-item metadata, and a diagnostic archive are
downloaded from each Helix results container for publication in Azure Pipelines.
If an initial test invocation fails, the existing `dotnet-test-slicer retry` flow
reruns only the failed tests once; persistent failures retain a nonzero work-item exit.

The public pipeline keeps the existing Azure Pipelines jobs and adds this path only
when `enableHostBuildTestsHelixPrototype` is `true`.
