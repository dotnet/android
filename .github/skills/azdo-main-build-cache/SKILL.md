---
name: azdo-main-build-cache
description: >-
  Hydrate a fresh macOS dotnet/android checkout from a scheduled Azure DevOps
  main build and run a warm incremental Release build. Use when asked for a
  main build cache, warm clean build, or cached first build.
---

# Azure DevOps main build cache

This experimental cache currently supports macOS and Release builds only.
A scheduled public main build must publish a compatible cache artifact before
remote hydration can work.

## Prerequisites

- An initialized checkout, including its submodules, and Git history containing
  the snapshot commit. The origin must identify the same GitHub repository.
- A compatible .NET SDK, Azure CLI, and its `azure-devops` extension. Azure CLI
  must be authenticated for the public `dnceng-public` organization.
- A fresh checkout with none of the snapshot's output directories already
  present. `apply` refuses to overwrite existing outputs.
- Use the normal repository toolchain and Release configuration; do not rely on
  a snapshot with custom build environment settings. `Configuration.Override.props`,
  untracked submodule files, deleted inputs, changed build definitions, and most
  added non-C# inputs require a clean build.

## Warm build

From the checkout to hydrate:

```sh
export DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1
make prepare CONFIGURATION=Release USE_MAIN_BUILD_CACHE=1 && make leeroy CONFIGURATION=Release
```

`USE_MAIN_BUILD_CACHE=1` runs `build-tools/scripts/main-build-cache.cs apply`
with the system `dotnet` before `install-dotnet` writes to `bin/`. It prints the
snapshot commit and writes it to `obj/azdo-main-build-cache/commit`; commits
after it are rebuilt incrementally.

`apply` also writes `obj/azdo-main-build-cache/MainBuildCache.targets`.
The `Makefile` and `dotnet-local.sh` pass it as `CustomAfterMicrosoftCommonTargets`
whenever it exists. Without it, every C# project recompiles because the SDK
hashes compiler inputs by absolute path. It also pins `XAVersionBranch`,
`XAVersionHash`, `XAVersionCommitCount`, and `PackVersionCommitCount` to the
snapshot's values: otherwise a different branch or commit count changes the
generated version files, the workload manifest, and the pack versions, which
rebuilds most of the SDK. Builds in a hydrated checkout therefore report the
snapshot's version. Delete `obj/azdo-main-build-cache` to stop pinning; the next
build regenerates version-dependent outputs.

Keep the MSBuild server settings for subsequent `dotnet-local.sh` commands in
this checkout. A shared MSBuild server can retain another checkout's workload
resolver state and fail to recognize the relocated Android SDK.

`apply` verifies the snapshot and commit ancestry, copies outputs while holding the cache lock, and adjusts input
timestamps: unchanged inputs receive their snapshot timestamps, while changed
or added inputs are made newer. This modifies source-file timestamps, not their
contents. Generated JNI source and SDK build definitions are also restored.
Archives preserve subsecond output timestamps so an unchanged input cannot
appear newer merely because its output was rounded down to a whole second.
Generated MSBuild editorconfigs and SourceLink maps are relocated to the
destination checkout without changing their timestamps; other generated state
is allowed to invalidate normally. NuGet-generated props/targets retain their timestamps when
normal restore produces identical imports; changed imports still invalidate
dependent outputs. Native CMake/ninja trees under `src/native/obj` are reused:
absolute paths in their text files and MSBuild `*.inputs` files are relocated
with their timestamps preserved, so ninja only recompiles changed sources.
Gradle user state is not reused. Snapshot format 4 requires newly created
snapshots; earlier prototype snapshots are incompatible.

The compile fingerprint hook re-hashes each project's `CoreCompile` inputs with
the local checkout and NuGet roots mapped back to the snapshot's roots. Only
when that hash equals the snapshot's does it restore the fingerprint file's
original timestamp; any real input change still recompiles that project and
its dependents. Do not backdate outputs by other means to suppress rebuilds.
A newer commit can legitimately change SourceLink metadata.

Measured on macOS (Apple Silicon): a clean Release `prepare` + `leeroy` took
about 279 s; a hydrated checkout at the snapshot commit took about 57 s. Most
of the remainder is MSBuild evaluation and workload installation.

The default cache is `~/.azdo-main-build-cache`; set `AZDO_MAIN_BUILD_CACHE`
to override it. Concurrent calls coordinate per repository, platform, and
configuration, download each artifact once, and prune older entries.

If the artifact is unavailable or the checkout is incompatible, report the
error and use the normal clean-build procedure from the `local-build` skill.
Do not delete or overwrite existing outputs without the user's approval.

## Local experiment

A completed clean Release build can produce an offline snapshot outside its
checkout:

```sh
dotnet build-tools/scripts/main-build-cache.cs create /path/to/snapshot
```

Pass `MAIN_BUILD_CACHE_SNAPSHOT=/path/to/snapshot` to `make prepare` to use that
snapshot instead of Azure DevOps.
