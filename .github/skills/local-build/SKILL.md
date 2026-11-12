---
name: local-build
description: >-
  Builds the local dotnet/android SDK and diagnoses local build problems. Use
  before running full-build or device tests, when `bin/<Configuration>/dotnet`
  is missing or stale, when `make`/`dotnet-local.sh` builds fail, stall, or run
  out of disk, and when several sessions or worktrees build on the same host.
  Covers entry points, fast incremental loops, known environment issues, and
  host-load rules for parallel sessions.
---

# Local SDK build

## Entry points (macOS/Linux)

| Goal | Command |
|---|---|
| Provision toolchain + bootstrap SDK | `make prepare` |
| Build the complete local SDK + workload | `make leeroy` |
| Both of the above | `make jenkins` (what `copilot-setup-steps.yml` and CI use) |

On Windows, use `build.cmd` (prepare, build, and pack) or `build.cmd Prepare` / `build.cmd Build`.

- **Do not use `make all` to produce a usable SDK.** It skips `BuildExtraApiLevels` (`Makefile:9-11` vs `build-tools/scripts/BuildEverything.mk:7-10`), so `ConfigureLocalWorkload` fails when extra stable API levels exist.
- `./build.sh` with no arguments also runs `make pack-dotnet`, which tests do not need.
- Pass the configuration to every step: `make prepare CONFIGURATION=Release && make leeroy CONFIGURATION=Release`.
- Pass extra MSBuild flags with `MSBUILD_ARGS='...'`. Each `make` step adds them after `-p:Configuration=$(CONFIGURATION)` (`build-tools/scripts/msbuild.mk`). Do not override `MSBUILD_FLAGS` on the command line, because that replaces the Makefile's own value.
- Binlogs for every step are written to `bin/Build$(CONFIGURATION)/msbuild-*.binlog`. Read them (for example with the `msbuild` skill) instead of re-running with more verbosity.

## Which SDK `dotnet-local.sh` uses

`dotnet-local.sh` uses `bin/Release/dotnet/dotnet` whenever it exists, otherwise `bin/Debug/dotnet/dotnet` (`dotnet-local.sh:4-12`). It does not look at `CONFIGURATION` and does not check that the workload is complete. If both configurations exist, a Debug rebuild is **not** what `dotnet-local.sh` runs. Delete the configuration you are not using, or call `bin/<Configuration>/dotnet/dotnet` directly.

`test -x bin/<Configuration>/dotnet/dotnet` only proves provisioning (`make prepare`), not that `make leeroy` completed.

## Fast inner loops after one complete build

A full `make leeroy` is only needed once per worktree and configuration, or after changes to the binding/API surface, native runtime, or packaging. For narrower changes, rebuild just the project:

| Change in | Refresh the local SDK with |
|---|---|
| `src/Xamarin.Android.Build.Tasks/` (tasks, `*.targets`, `Microsoft.Android.Sdk/targets/**`) | `./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj`. Its output path is the SDK pack's `tools/` directory (`Configuration.props:78`), and it copies `Microsoft.Android.Sdk/targets/**` there. |
| Standalone libraries (`src/Microsoft.Android.Build.BaseTasks`, `src/Xamarin.Android.Tools.AndroidSdk`, `src/Microsoft.Android.Build.Tasks`, Java.Interop tooling, `src/generator`) | `dotnet build <project>.csproj` plus its standalone tests. No local SDK is required. |
| Anything else, or if unsure | Re-run `make leeroy` (it is incremental). |

## Known local issues

| Symptom | Cause | Fix |
|---|---|---|
| `make` stops producing output for 10+ minutes with idle MSBuild task-host/worker nodes and no error | Reused MSBuild server/nodes or shared compiler server stuck, usually under heavy host load from parallel builds | Stop only your own build, then retry: `DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 make leeroy CONFIGURATION=<cfg> MSBUILD_ARGS='-nr:false -m:1 -p:UseSharedCompilation=false'` |
| `warning : Repository has no commit` / empty SourceLink metadata, which breaks `-warnaserror` baselines | SourceLink cannot resolve `HEAD` in a fresh worktree whose branch ref exists only in the main checkout's packed refs | Commit on the branch (this writes a loose ref). For local-only validation, `-p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false` is also acceptable. Do not change SourceLink settings in source. |
| `ConfigureLocalWorkload` fails after `make all` | `BuildExtraApiLevels` was skipped | Use `make leeroy` |
| Phantom compile errors or stale outputs after switching branches | Stale incremental output | Delete `bin/` and `obj/`, then `make prepare && make leeroy` |
| `No space left on device` | Each worktree has its own `bin/` (local SDK + build output) | Delete `bin/` and `obj/` in worktrees you no longer need. The Android toolchain (`~/android-toolchain`, `~/android-archives`) is already shared across worktrees (`Configuration.props:82-83`); do not redirect it per worktree. |

## Parallel sessions and host load

A full local SDK build saturates a developer machine. When several sessions or worktrees share a host:

- **Only build the full SDK when the change needs it.** Use the validation table in the `tests` skill. Many changes only need a project build plus standalone tests.
- **Run at most one or two full SDK builds at a time.** Start the next one only after the previous one finishes. When others are running, cap your own parallelism with `MSBUILD_ARGS='-m:4'`.
- **Don't build lease or admission protocols between sessions.** A coordinator that fans out work should simply queue heavy builds and device runs, one after another.
- **Never kill processes you did not start.** Stop only your own shell or PID.
- **Before fanning out, check for overlapping work** (open PRs, existing sessions, or branches on the same topic) so the SDK is not rebuilt to redo existing work.
