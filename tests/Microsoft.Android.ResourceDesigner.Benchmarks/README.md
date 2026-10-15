# Resource designer emitter benchmarks

`Microsoft.Android.Tasks.GenerateResourceDesignerAssembly` uses
`System.Reflection.Metadata`, without a Cecil dependency or framework assembly
resolution. It replaces the original Cecil task in both normal and design-time
builds, preserving resource parsing, identifier handling, case mapping,
strong-name identity, and copy-if-changed behavior. No opt-in property is needed.

## Task benchmarks

Prepare the Release build prerequisites, then build and verify the benchmark:

```sh
make prepare CONFIGURATION=Release
dotnet build tests/Microsoft.Android.ResourceDesigner.Benchmarks/Microsoft.Android.ResourceDesigner.Benchmarks.csproj -c Release
dotnet run --project tests/Microsoft.Android.ResourceDesigner.Benchmarks/Microsoft.Android.ResourceDesigner.Benchmarks.csproj -c Release --no-build -- --verify
dotnet run --project tests/Microsoft.Android.ResourceDesigner.Benchmarks/Microsoft.Android.ResourceDesigner.Benchmarks.csproj -c Release --no-build -- --filter '*ResourceDesignerBenchmarks*' --inProcess --job Short --warmupCount 3 --iterationCount 8
```

BenchmarkDotNet 0.15.8 does not recognize the .NET 11 runtime for its generated
out-of-process projects, so use its in-process toolchain. The task is built in
Release. Setup checks assembly identity, deterministic metadata, and the expected
resource types before timing. It also loads application output and reads every
property and field, executing array initializers. Task tests verify resource
values against the parsed input. The harness has no legacy task or Cecil
dependency and covers six application/library/scenario combinations.

The default input is the repository's captured MAUI `R.txt` (6,817 lines).
To use an actual application's generated inputs:

```sh
RESOURCE_DESIGNER_RTXT=/absolute/path/to/obj/Debug/net11.0-android/R.txt \
RESOURCE_DESIGNER_CASE_MAP=/absolute/path/to/obj/Debug/net11.0-android/case_map.txt \
dotnet run --project tests/Microsoft.Android.ResourceDesigner.Benchmarks/Microsoft.Android.ResourceDesigner.Benchmarks.csproj -c Release --no-build -- --filter '*ResourceDesignerBenchmarks*' --inProcess --job Short --warmupCount 3 --iterationCount 8
```

`RESOURCE_DESIGNER_CASE_MAP` is optional. Missing explicitly supplied files fail
setup instead of falling back to the default fixture.

| Scenario | Work measured |
|---|---|
| `Generate` | New task instance, output deletion, parsing, emission, and writing a fresh output |
| `CompareUnchanged` | New task instance, full regeneration, and byte comparison against an identical output; input timestamp forces regeneration |
| `UpToDate` | New task instance with input older than output; task timestamp fast path |

These are warm-process task measurements, not complete application builds.
`UpToDate` is not an MSBuild no-op build: MSBuild can skip the containing target
without invoking the task. The old Cecil task resolved framework assemblies and
constructed its metadata preamble before checking timestamps; the metadata task
checks timestamps before constructing metadata.

## Recorded head-to-head measurements

These results were collected before removing the original Cecil task and its
benchmark baseline. Both tasks were built in Release and executed in the same
runtime. Setup compared strong-name identity, types, fields, constants,
properties, attributes, and normalized getter IL. It checked each emitter's own
`GeneratedCodeAttribute` version and compared every application property and
field value, including static arrays. All 12 combinations completed successfully.
The current harness measures only the metadata emitter.

Apple M5 Max, macOS 26.7.1, .NET SDK 11.0.100-rc.2.26475.136,
.NET 11 RC2 Arm64, BenchmarkDotNet 0.15.8 in-process ShortRun,
three warmups and eight measurement iterations:

| Output | Scenario | Cecil mean +/- SD | Metadata mean +/- SD | Cecil allocation | Metadata allocation |
|---|---|---|---|---|---|
| Library | Generate | 20.817 +/- 0.328 ms | 8.115 +/- 0.182 ms | 25,604.68 KiB | 10,953.32 KiB |
| Library | CompareUnchanged | 20.216 +/- 0.413 ms | 7.558 +/- 0.093 ms | 25,604.36 KiB | 10,953.06 KiB |
| Library | UpToDate | 2.755 +/- 0.076 ms | 0.0174 +/- 0.0002 ms | 6,824.83 KiB | 1.51 KiB |
| Application | Generate | 25.492 +/- 0.689 ms | 8.639 +/- 0.091 ms | 30,497.36 KiB | 12,109.47 KiB |
| Application | CompareUnchanged | 24.312 +/- 0.288 ms | 8.461 +/- 0.039 ms | 30,497.58 KiB | 12,109.44 KiB |
| Application | UpToDate | 2.558 +/- 0.034 ms | 0.0175 +/- 0.0004 ms | 6,823.87 KiB | 1.51 KiB |

The final run shows generation 2.56x faster for libraries and 2.95x faster for
applications, with 57% and 60% less managed allocation respectively. An earlier
run measured 2.50x and 2.74x respectively; these are short exploratory runs on a
shared machine, not universal performance guarantees. Output size
is 507,392 versus 505,856 bytes for libraries and 660,992 versus 644,608 bytes
for applications. These results do not establish an end-to-end build speedup.
The PE layouts, MVIDs, assembly reference scopes, and emitter version attribute
differ between implementations; identical inputs must be byte-identical within
each deterministic implementation, not between emitters.

For application build comparisons, use builds from before and after this change
and clean, separate intermediate directories, including for design-time builds.
Capture separate binlogs for equivalent fresh-output, changed-resource, and
unchanged builds.
Restore inputs first, keep runtime/configuration/ABI and instrumentation fixed,
and repeat runs. Report both whole-build wall time and this task's execution
time; a task improvement may be lost in other Android build work.

## Validation

Validate the replacement, including its default task registrations, with:

```sh
make prepare
make all
dotnet build src/Microsoft.Android.Build.Tasks/Tests/Microsoft.Android.Build.Tasks.Tests/Microsoft.Android.Build.Tasks.Tests.csproj -v quiet
dotnet test src/Microsoft.Android.Build.Tasks/Tests/Microsoft.Android.Build.Tasks.Tests/Microsoft.Android.Build.Tasks.Tests.csproj --no-build --filter 'Name~GenerateResourceDesignerAssemblyTests|Name~ResourcesTests' -v quiet
./dotnet-local.sh test bin/TestDebug/net10.0/Xamarin.Android.Build.Tests.dll --filter 'Name~RTxtParserTest|Name~CheckDontUpdateResourceIfNotNeeded|Name~BuildAppWithManagedResourceParserAndLibraries|Name~BuildApplicationCheckThatAddStaticResourcesTargetDoesNotRerun|(Name~BuildInDesignTimeMode&Name~CoreCLR)' -v quiet
```

After removing the Cecil task, `make all` and the modern test project build
passed with no warnings or errors. All 15 modern task/resource cases and all
11 parser, application/library, incremental, and design-time cases passed without
an opt-in property. The Release benchmark build, `--verify`, and all six
metadata-only benchmark combinations also completed successfully.

The deterministic timestamp/MVID tests now live in the modern task test suite;
the removed Cecil test cases are covered there for both application and library
output. The initial SDK setup lacked API 37.1/37.2 reference assemblies;
building those additional `Mono.Android.csproj` API configurations allowed
`make all` to pass.
