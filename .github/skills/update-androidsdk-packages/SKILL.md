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

## Seven hard rules — read these before touching anything

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

**3. Preserve the sources package for every shipped stable platform.** Source archives are additive,
not a single "latest" package: `sources;android-37.0` and `sources;android-37.1` install into distinct
SDK directories and both are needed when both platform levels are marked `IsLatestStable`. Never
replace or remove an existing `source-NN.N_rMM.zip` entry when a new stable platform source appears.
Keep one `_AndroidSdkPackage` entry and one version-specific hash property per stable platform level,
and preserve the full API level in `Destination` (`sources\android-37.0` uses the historical
`sources\android-37` directory; `sources\android-37.1` uses `sources\android-37.1`).

**4. Stable means Google's stable channel.** Never select an emulator or other tool from a
development, canary, beta, or preview channel merely because its revision sorts higher. Require
`channel-0` for emulator updates. If a package's channel metadata and release labeling disagree,
do not update it unattended; report the ambiguity instead.

**5. Command-line tools are a coordinated product dependency.** Do not update only the bootstrap
pins. A command-line-tools bump must also update
`src/Xamarin.Installer.Build.Tasks/Xamarin.Installer.Common.props` and the matching latest entry in
`src/Xamarin.Installer.AndroidSDK/Feeds/AndroidManifestFeed_d18.0.xml`, including every published
host/architecture archive. Confirm `CodeGenerator.targets` tracks the property file that supplies
`AndroidCommandLineToolsVersion`. If the automated workflow is not authorized to change every
required file, stop and report the coordinated update instead of opening a partial PR.

**6. Never execute an unverified downloader to accept licenses.** License acceptance must not run
the `android` bootstrapper, `sdkmanager`, or any payload fetched at execution time. Preserve all
existing valid fingerprints, add the pinned expected fingerprint under a cross-process lock, write
atomically, validate every line as a 40-character SHA-1 fingerprint, and create the acceptance
marker only after validation succeeds.

**7. Extraction outputs identify the exact archive.** Packages for different hosts or architectures
may share a destination. Their incremental output stamp must include both archive identity and
expected SHA-256; `source.properties` alone is not a safe extraction sentinel.

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

The script sorts matches by revision (newest first) and reports each package's channel. Require
`channel-0` for emulator updates and reject version/display names containing `alpha`, `beta`,
`canary`, `dev`, `preview`, or `rc`. For other package families, a non-zero channel or conflicting
metadata is ambiguous: keep the previous confirmed-stable revision and report it rather than
guessing.

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
dotnet run .github/skills/update-androidsdk-packages/scripts/sha256_of_url.cs -- https://dl.google.com/android/repository/build-tools_r37.0.0_linux.zip --sha1 <manifest-sha1> --size <manifest-size>
```

This script downloads into a scratch temp file (not `$(AndroidToolchainCacheDirectory)`,
which defaults to `$HOME/android-archives` and is the repo's real download cache) and deletes the
file once hashed. It validates the downloaded bytes against the manifest's SHA-1 and size before
printing the SHA-256, so it never trusts a successful HTTP response for the wrong archive.
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
- Keep a `source-NN.N_r0M.zip` package for every API level marked `IsLatestStable` in
  `_PlatformPackage`. Treat a newly published stable source as an addition, not a replacement.
  Give each archive a version-specific hash property such as `XAAndroidSourcesHash37_0`, and use
  the platform's distinct SDK directory as `Destination` (`37.0` historically maps to
  `\sources\android-37`; `37.1` maps to `\sources\android-37.1`). Update an existing entry in place
  only when Google publishes a newer revision for that same API level.
- When command-line tools changes, update the shipped product version and feed entry described in
  hard rule 5 in the same change. Do not leave bootstrap and product dependency versions split.
- Keep archive/hash-specific extraction stamps intact when adding host or architecture variants.

### 5. Validate before finishing

Run these in order — do not skip the BootstrapTasks build; `androidsdk.csproj` uses
`UnzipDirectoryChildren`, a task defined in that assembly, and fails at evaluation time without it:

```bash
dotnet build build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks.csproj -v:minimal
dotnet restore src/androidsdk/androidsdk.csproj
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
- **Diff cleanliness** — no unrelated files or stray temp downloads. Routine families remain scoped to `Configuration.props` and `src/androidsdk/androidsdk.targets`; command-line-tools updates additionally require the two shipped-product files in hard rule 5. Never edit `package.xml.in` during a routine refresh.
- **The seven hard rules above** — diff the NDK properties and the `_PlatformPackage` item count/API-level set, verify every `IsLatestStable` platform has its own sources package, verify selected releases are stable, and verify extraction outputs remain archive/hash-specific.
- **Command-line tools compatibility** — test with `licenses/android-sdk-license` and `.licenses-accepted` absent, with a pre-existing unrelated valid fingerprint, and with malformed content. Confirm the expected pinned fingerprint is created, the unrelated fingerprint is preserved, malformed content fails, writes are atomic/locked, and Gradle recognizes the Build Tools and platform licenses. No license-acceptance path may execute a network-capable Android CLI.
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
