# Host build tests in Helix prototype

This guarded prototype stages the already-prepared .NET SDK/workload, Android SDK,
JDK, NuGet/Gradle caches, selected repository inputs, and test assemblies into one
Helix correlation payload per host OS. Work item payloads contain only a runsettings
filter, a small command script, and generation metadata. To control payload size, the
source copy omits `src/Mono.Android`; host build tests consume that product through the
prepared workload packs instead.

`prepare-host-build-test-helix-payload.ps1` discovers NUnit tests with the repository's
`dotnet-test-slicer`, reads its `balance.xml` format, and uses deterministic best-fit
decreasing bin packing. The target is `15` estimated wall-clock minutes by default.
`durationParallelism` translates summed per-test elapsed time into expected wall time
while preserving the current NUnit worker setting. The public prototype defaults this
factor to `2.5`, uses `60` seconds per test for the explicit first-run fallback, and
allows `45` minutes before Helix terminates a work item.

If no timing history is provided, generation explicitly reports `count-fallback` and
uses the configured fallback duration per test. It does not label count-only slicing
as duration-balanced. Each completed run publishes a new `balance.xml`; pass that
run's build ID through `hostBuildTestsHelixTimingBuildId` on the next queued build.

The public pipeline keeps the existing Azure Pipelines jobs and adds this path only
when `enableHostBuildTestsHelixPrototype` is `true`.
