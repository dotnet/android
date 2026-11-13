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

- Don't use `make all`. It skips `BuildExtraApiLevels`, so `ConfigureLocalWorkload` fails.
- Pass extra flags via `MSBUILD_ARGS='...'`. Overriding `MSBUILD_FLAGS` drops `-p:Configuration`.
- On failure, read `bin/Build<cfg>/msbuild-*.binlog` (`msbuild` skill) rather than re-running verbosely.
- `dotnet-local.sh` runs `bin/Release` whenever it exists, regardless of `CONFIGURATION`. Delete the configuration you aren't using, or call `bin/<cfg>/dotnet/dotnet` directly with the same workload roots the wrapper sets: `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="$PWD/bin/<cfg>/lib/sdk-manifests" DOTNETSDK_WORKLOAD_PACK_ROOTS="$PWD/bin/<cfg>/lib"`.
- An existing `bin/<cfg>/dotnet` only means `make prepare` ran, not that the SDK was built.

## Rebuild only what changed

| Change in | Command |
|---|---|
| `src/Xamarin.Android.Build.Tasks/` (tasks, `.targets`) | `./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -c <cfg>`, where `<cfg>` matches the SDK and tests (it defaults to Debug and outputs into that configuration's local SDK) |
| Standalone libraries (`Microsoft.Android.Build.BaseTasks`, `Xamarin.Android.Tools.AndroidSdk`, `Microsoft.Android.Build.Tasks`, Java.Interop tooling, `generator`) | `dotnet build` / `dotnet test` the project; no local SDK needed |
| Bindings, native runtime, packaging, or unsure | `make leeroy` (incremental) |

## Known issues

| Symptom | Fix |
|---|---|
| No output for 10+ minutes, idle MSBuild nodes | Stop only your build, then run `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 make leeroy MSBUILD_ARGS='-nr:false -m:1 -p:UseSharedCompilation=false'` |
| SourceLink `Repository has no commit` in a fresh worktree | Commit on the branch, or for local-only checks pass `-p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false`. Don't change this in source. |
| Phantom errors after switching branches | `rm -rf bin obj`, then rebuild |
| `No space left on device` | Delete `bin/` and `obj/` in unused worktrees. `~/android-toolchain` is already shared; don't redirect it. |

## Parallel sessions on one host

- Build the full SDK only when the change requires it (see the validation table in the `tests` skill).
- Run at most one or two full builds at once; cap others with `MSBUILD_ARGS='-m:4'`.
- Queue heavy builds and device runs; don't invent lease protocols between sessions.
- Never kill processes you didn't start.
- Before fanning out, check that no existing PR, branch, or session already covers the work.
