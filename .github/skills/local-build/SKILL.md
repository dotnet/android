---
name: local-build
description: >-
  Builds the local dotnet/android SDK and fixes local build problems. Use
  before full-build or device tests, when `bin/<Configuration>/dotnet` is
  missing or stale, when `make` or `dotnet-local.sh` builds fail, stall, or run
  out of disk, and when several sessions build on one host.
---

# Local SDK build

## Build

```bash
make prepare CONFIGURATION=Debug && make leeroy CONFIGURATION=Debug   # = make jenkins, as CI does
```

On Windows, run `build.cmd`.

- In a new worktree, run `git submodule update --init` first; otherwise `make leeroy` fails late with `FileNotFoundException: .../Mono.Android.xml` (missing `external/android-api-docs`).
- Don't use `make all`. It skips `BuildExtraApiLevels`, so `ConfigureLocalWorkload` fails.
- Pass extra flags via `MSBUILD_ARGS='...'`. Overriding `MSBUILD_FLAGS` drops `-p:Configuration`.
- On failure, read `bin/Build<cfg>/msbuild-*.binlog` (for example `binlogtool search bin/Build<cfg>/msbuild-*.binlog "error"`; see "Analysis commands" in `.github/skills/ci-status/references/binlog-analysis.md`) rather than re-running verbosely.
- If `ConfigureLocalWorkload` cannot find `Mono.Android.xml`, check `git submodule status -- external/android-api-docs`; if uninitialized, run `git submodule update --init external/android-api-docs` and rebuild the affected outputs.
- `dotnet-local.sh` runs `bin/Release` whenever it exists, regardless of `CONFIGURATION`. To select another configuration, call `bin/<cfg>/dotnet/dotnet` directly with `PATH="$PWD/bin/<cfg>/dotnet:$PATH"` and the same workload roots the wrapper sets: `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="$PWD/bin/<cfg>/lib/sdk-manifests" DOTNETSDK_WORKLOAD_PACK_ROOTS="$PWD/bin/<cfg>/lib"`.
- An existing `bin/<cfg>/dotnet` only means `make prepare` ran, not that the SDK was built.

## Rebuild only what changed

| Change in | Command |
|---|---|
| `src/Xamarin.Android.Build.Tasks/` (tasks, `.targets`) | `./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -c <cfg>`, where `<cfg>` matches the SDK and tests (it defaults to Debug and outputs into that configuration's local SDK) |
| Standalone libraries (`Microsoft.Android.Build.BaseTasks`, `Xamarin.Android.Tools.AndroidSdk`, `Microsoft.Android.Build.Tasks`, Java.Interop tooling, `generator`) | `dotnet build` / `dotnet test` the project; no local SDK needed |
| Bindings, native runtime, packaging, or unsure | `make leeroy CONFIGURATION=<cfg>` (incremental; `CONFIGURATION` defaults to Debug) |

## Known issues

| Symptom | Fix |
|---|---|
| No output for 10+ minutes, idle MSBuild nodes | Stop only your build, then run `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 make leeroy CONFIGURATION=<cfg> MSBUILD_ARGS='-nr:false -m:1 -p:UseSharedCompilation=false'` |
| SourceLink `Repository has no commit` in a fresh worktree | Commit on the branch, or for local-only checks pass `-p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false`. Don't change this in source. |
| Suspected stale outputs | Inspect binlogs and resolved SDK paths first; clean only affected, owned outputs when stale state is established. |
| `No space left on device` | Inspect disk usage and clean only regenerable outputs you own; don't delete other worktrees or shared toolchains/caches. |

## Parallel sessions on one host

- Build the full SDK only when the change requires it (see the validation table in the `tests` skill).
- Run at most one or two full builds at once; cap others with `MSBUILD_ARGS='-m:4'`.
- Queue heavy builds and device runs; don't invent lease protocols between sessions.
- Never kill processes you didn't start. Before parallel provisioning or `Clean`/`Rebuild`, verify resolved SDK/JDK/NDK, archive-cache, and `XAPackagesDir` paths; a private `HOME` alone doesn't isolate them. Use owned paths or coordinate one provisioner. `java-runtime` Clean can delete a shared JDK.
- Before fanning out, check that no existing PR, branch, or session already covers the work.
