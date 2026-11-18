---
name: local-build
description: >-
  Builds the local dotnet/android SDK and fixes local build problems. Use
  before full-build or device tests, when `bin/<Configuration>/workload` is
  missing or stale, when `dotnetup dotnet` or `dotnet-local.sh` builds fail, stall, or run
  out of disk, and when several sessions build on one host.
---

# Local SDK build

## Build

```bash
dotnetup sdk install
dotnetup dotnet build Microsoft.Android.slnx -c Debug -bl:bin/BuildDebug/local-build.binlog
```

Use the same commands on Windows.

- The solution build prepares prerequisites, initializes `external/android-api-docs`, builds extra API levels, and configures the local workload.
- Pass extra MSBuild flags directly to `dotnetup dotnet build`.
- On failure, read the captured `bin/Build<cfg>/local-build.binlog` (see "Analysis commands" in `.github/skills/ci-status/references/binlog-analysis.md`) rather than re-running verbosely.
- `dotnet-local.sh` defaults to Debug workloads; set `CONFIGURATION=Release` for Release workloads and also pass `-c Release` to build/test commands. On Windows, set the environment variable and use `dotnet-local.cmd`.
- An installed shared SDK does not mean the repository's Android packs or test assemblies have been built.

## Rebuild only what changed

| Change in | Command |
|---|---|
| `src/Xamarin.Android.Build.Tasks/` (tasks, `.targets`) | `./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -c <cfg>`, where `<cfg>` matches the SDK and tests (it defaults to Debug and outputs into that configuration's local SDK) |
| Standalone libraries (`Microsoft.Android.Build.BaseTasks`, `Xamarin.Android.Tools.AndroidSdk`, `Microsoft.Android.Build.Tasks`, Java.Interop tooling, `generator`) | `dotnetup dotnet build` / `dotnetup dotnet test` the project; no local Android build needed |
| Bindings, native runtime, packaging, or unsure | `dotnetup dotnet build Microsoft.Android.slnx -c <cfg>` (incremental; configuration defaults to Debug) |

## Known issues

| Symptom | Fix |
|---|---|
| No output for 10+ minutes, idle MSBuild nodes | Stop only your build, then run `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 dotnetup dotnet build Microsoft.Android.slnx -c <cfg> -nr:false -m:1 -p:UseSharedCompilation=false` |
| SourceLink `Repository has no commit` in a fresh worktree | Commit on the branch, or for local-only checks pass `-p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false`. Don't change this in source. |
| Phantom errors after switching branches | `rm -rf bin obj`, then rebuild |
| `No space left on device` | Delete `bin/` and `obj/` in unused worktrees. `~/android-toolchain` is already shared; don't redirect it. |

## Parallel sessions on one host

- Build the full SDK only when the change requires it (see the validation table in the `tests` skill).
- Run at most one or two full builds at once; cap others with `-m:4`.
- Queue heavy builds and device runs; don't invent lease protocols between sessions.
- Never kill processes you didn't start.
- Before fanning out, check that no existing PR, branch, or session already covers the work.
