---
name: update-androidsdk-packages
description: >-
  Update stable Android SDK package pins (versions, revisions, URLs, SHA-256 hashes) in
  src/androidsdk/androidsdk.targets and Configuration.props for androidsdk.csproj. Use for
  requests to update/refresh Android SDK packages, tools, or archive hashes. Never for the
  Android NDK (out of scope) or unrelated NuGet/MSBuild SDK versions.
---

# Update Android SDK packages

`src/androidsdk/androidsdk.csproj` installs the Android SDK by downloading a fixed catalog of
Google-published zips and verifying each one's SHA-256 against a hardcoded hash. The catalog
lives in two files:

- **`Configuration.props`** — version numbers, `PkgRevision`s, and every `XA*Hash*`/`CommandLineTools*` property.
- **`src/androidsdk/androidsdk.targets`** — the `_AndroidSdkPackage` item list (one item per zip a host downloads) and the `_PlatformPackage` catalog (one item per Android platform API already shipped).

Google republishes tool revisions on their own cadence; this skill brings those two files back in
sync with Google's *current stable* releases with a minimal, reviewable diff — matching the shape of PR #12371, which did exactly this (build-tools/platform-tools/cmdline-tools/cmake/emulator/sources/platform revisions all bumped, hashes recomputed, and per-arch macOS cmdline-tools support added when Apple Silicon archives showed up).

## Two hard rules — read these before touching anything

**1. Never touch the Android NDK.** `_XAAndroidNdkRelease`, `_XAAndroidNdkPkgRevision`, and every
`XAAndroidNdkHash*` property in `Configuration.props`, plus the `android-ndk-r$(_XAAndroidNdkRelease)-*`
package entry in `androidsdk.targets`, are intentionally out of scope for this skill. The NDK has
its own release cadence and compatibility constraints that this workflow doesn't manage — leave
every NDK-related line exactly as you found it, even if Google has published a newer NDK.

**2. Never add a new Android platform API level to `_PlatformPackage` — but ALWAYS report when one exists.**
This item group in `androidsdk.targets` is the list of Android platform SDKs this repo ships
against; adding an entry is a deliberate, separate decision (usually tied to a new Android OS
release and API surface changes elsewhere in the repo), not something to do as a side effect of a
routine package refresh. If Google has published a newer *revision* of a platform API level that's
already in the catalog (e.g. `platform-37.0_r01` → `platform-37.0_r02`, or a new extension level
like `_ext19`), update that existing entry's `Include`, `Hash`, and any `IsLatestStable`/extension
revision in place. But if Google has published an entirely new API level not yet in the catalog
(e.g. the catalog tops out at `37.0` but the manifest also lists a stable `37.1`), do not add it —
that decision is out of scope here. **Every time this happens, you must still surface it**: always
check the manifest for any stable platform level newer than the highest one already in
`_PlatformPackage` (not just when the user happens to ask), and call it out explicitly in your
final summary (step 6 below) — even if the user didn't ask about platform levels at all and even if
nothing else in the catalog needed updating this run. Silence here is a bug: the whole point of this
rule is that a human decides whether/when to onboard a new API level, and they can't decide on
something they were never told about.

## Workflow

### 1. Read the current catalog

Read `Configuration.props` (the `XA*`/`CommandLineTools*`/`Emulator*` properties, roughly lines
100-190) and `src/androidsdk/androidsdk.targets` (`_PlatformPackage` item group near the top, and
the `_AndroidSdkPackage` item group with per-host `Include`s) so you know exactly which package
families and API levels already exist. You are only ever refreshing what's already there.

### 2. Look up what Google currently publishes

Google's canonical SDK manifest is `https://dl.google.com/android/repository/repository2-3.xml`
(historically referenced via `dl-ssl.google.com` — same content, prefer `dl.google.com`). System
images live in a separate manifest per API level tree, e.g.
`https://dl.google.com/android/repository/sys-img/android/sys-img2-3.xml`. Use the bundled helper
to query it instead of hand-parsing XML in your head:

```bash
dotnet run .github/skills/update-androidsdk-packages/scripts/fetch_repo_package.cs -- --path build-tools
dotnet run .github/skills/update-androidsdk-packages/scripts/fetch_repo_package.cs -- --path "platforms;android-37" --archives
dotnet run .github/skills/update-androidsdk-packages/scripts/fetch_repo_package.cs -- --path emulator --archives
```

(These are C# file-based apps, matching the `ci_failures.cs` convention used by the `ci-status` skill — first run restores/builds, so allow a few extra seconds.)

The script sorts matches by revision (newest first) and flags anything whose path/display-name
looks like a preview build. **Treat that flag as a hint, not ground truth** — Google's
`channelRef` metadata is not a reliable stable/preview signal by itself (some genuinely-stable
packages carry a non-zero channel id, and freshly-promoted stable packages can briefly still show
old channel numbers). Cross-check the display name and version string yourself: a real stable
release reads like `36.0.1` or `28c`, not `37.0.0-rc1`, `2025.09.15-alpha01`, or anything with
`beta`/`canary`/`preview` in it. When genuinely unsure whether a release is stable, prefer the
previous confirmed-stable revision over guessing.

Reference `references/package-catalog.md` for the mapping between each `androidsdk.targets` entry,
its manifest `path`, and its `Configuration.props` properties — it documents the current package
families (build-tools, platform-tools, cmdline-tools, cmake, emulator, the API 29 system image,
m2repository, docs, sources, platforms) so you don't have to re-derive the mapping from scratch
each time.

### 3. Get authoritative SHA-256 hashes

Google's manifests only publish SHA-1 checksums, but `Configuration.props` pins SHA-256 (MSBuild's
`GetFileHash` task requires SHA-256+, see the comment atop `androidsdk.targets`). **Never invent or
guess a SHA-256** — either:

- Find it already published somewhere authoritative Google links to alongside the release (rare
  for these particular archives), or
- Compute it yourself by downloading the exact archive URL and hashing it:

```bash
dotnet run .github/skills/update-androidsdk-packages/scripts/sha256_of_url.cs -- https://dl.google.com/android/repository/build-tools_r37.0.0_linux.zip
```

This script downloads into a scratch temp file (not `$(AndroidToolchainCacheDirectory)`,
which defaults to `$HOME/android-archives` and is the repo's real download cache) and deletes the
file once hashed, so it never pollutes the cache or shows up as an untracked file in `git status`.
Do this for every host archive you're updating — Windows, Linux, macOS, and macOS arm64 when Google
publishes a separate Apple Silicon archive (as it started doing for command-line tools; check
whether the manifest/download page now lists a `mac_arm64` alongside `mac_x86_64` before assuming
one shared macOS zip still covers both).

### 4. Update per-host and per-arch hashes and versions

- Bump the shared version/revision property once (e.g. `XABuildToolsVersion`, `XAPlatformToolsVersion`,
  `CommandLineToolsFolder`/`CommandLineToolsVersion`, `AndroidCmakeVersion`, `EmulatorVersion` +
  `EmulatorPkgRevision`).
- Update every `*HashMacOS`/`*HashLinux`/`*HashWindows` (and `*HashMacOSArm64`/`*HashMacOSx64` where
  they exist) for that package family with the freshly computed SHA-256.
- If Google has started publishing an architecture-specific macOS archive for a package that
  previously had one shared macOS zip (as happened for command-line tools in PR #12371), add the
  arm64 variant the same way that PR did: a second `_AndroidSdkPackage` item gated on
  `'$(_IsArm64Apple)' == 'true'` alongside the existing x86_64 item gated on
  `'$(_IsArm64Apple)' != 'true'`, plus a new `*HashMacOSArm64` property. Follow the existing
  `emulator`/system-image entries in `androidsdk.targets` as the pattern for per-arch conditions —
  `_IsArm64Apple` is already computed in `Configuration.props`.
- For platform packages already in `_PlatformPackage`, update the `Include` (new package/revision
  string, e.g. `platform-37.0_r01` → `platform-37.0_r02`) and `Hash` in place. Update the
  extension-level suffix too when Google has published one for an API level that already uses it
  (e.g. `platform-34-ext7_r02` → `platform-34-ext12_r01`) — do not introduce an extension suffix for
  an API level that never had one, or vice versa, without a clear reason from the manifest.
- The `source-NN_r0M.zip` sources package and `XAAndroidSourcesHash` should track whichever API level
  is `IsLatestStable="true"` in `_PlatformPackage` (see the existing `<Destination>` path — it embeds
  the API level, e.g. `\sources\android-37.0`). Update both the zip name/Destination and the hash
  together if the latest stable API level's source archive changed.

### 5. Validate before finishing

Run these in order — do not skip the BootstrapTasks build; `androidsdk.csproj` uses
`UnzipDirectoryChildren`, a task defined in that assembly, and fails at evaluation time without it:

```bash
dotnet build build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks.csproj -v:minimal
dotnet build src/androidsdk/androidsdk.csproj --no-restore -v:minimal
```

The second build downloads and extracts the whole SDK for the current host — expect it to take a
few minutes and use real disk space/network. If that's not acceptable in your environment, at
least confirm MSBuild evaluation succeeds without downloading anything:

```bash
dotnet build src/androidsdk/androidsdk.csproj --no-restore -v:minimal -t:_AddPlatformPackagesToInstall
```

Also check:
- **XML validity** — both edited files still parse (`dotnet build` will fail loudly on malformed XML, but a quick sanity check like `powershell -Command "[xml](Get-Content src/androidsdk/androidsdk.targets)"` catches issues faster).
- **Diff cleanliness** — `git status` and `git diff` should show changes *only* in `Configuration.props` and `src/androidsdk/androidsdk.targets` (plus `package.xml.in` only if you deliberately changed the generated-package-xml template, which is rare). No stray temp files from hashing (the `sha256_of_url.cs` script cleans up after itself; double check if you downloaded anything manually instead).
- **The two hard rules above** — diff the NDK properties and the `_PlatformPackage` item count/API-level set against `git diff` to confirm neither was touched/expanded.
- **Formatting** — match the existing tab indentation and column alignment in both files (several `_PlatformPackage`/`_AndroidSdkPackage` lines are hand-aligned with extra spaces before `<ApiLevel>`/`<Hash>` — preserve that style rather than reformatting the whole block).

### 6. Summarize what changed

When done, report which package families were bumped (old → new version/revision), which hosts/
archs had hashes recomputed, whether a new macOS-arm64-specific archive was added, and confirm the
NDK was left untouched. **Always** include a line about the platform catalog check from rule 2
above — either "no newer stable platform level exists upstream" or, if one does, name it explicitly
(e.g. "note: platform 37.1 is published upstream but was intentionally not added — the catalog
still tops out at 37.0; add it in a separate change if desired"). Report this every time, regardless
of whether the user's request mentioned platform levels at all.

## Reference

- `references/package-catalog.md` — the current package families, their manifest paths, and their
  `Configuration.props`/`androidsdk.targets` locations.
