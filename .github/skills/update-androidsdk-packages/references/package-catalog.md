# Android SDK package catalog reference

Snapshot of the package families this skill manages, as of the update performed in PR #12371.
Use this as a map, not gospel — always re-check `Configuration.props` and
`src/androidsdk/androidsdk.targets` for the current values before editing, since this file will
drift as the skill is used.

| Family | Manifest `path` prefix | Version/revision property | Hash properties | `androidsdk.targets` location |
|---|---|---|---|---|
| build-tools | `build-tools` | `XABuildToolsVersion`, `XABuildToolsFolder` | `XABuildToolsHashMacOS/Linux/Windows` | `build-tools_r$(XABuildToolsVersion)_{macosx,linux,windows}.zip` |
| platform-tools | `platform-tools` | `XAPlatformToolsVersion` | `XAPlatformToolsHashMacOS/Linux/Windows` | `platform-tools_r$(XAPlatformToolsVersion)-{darwin,linux,win}.zip` |
| cmdline-tools | `cmdline-tools` | `CommandLineToolsFolder`, `CommandLineToolsVersion` | `XACmdlineToolsHashMacOS`, `XACmdlineToolsHashMacOSArm64`, `XACmdlineToolsHashLinux/Windows` | `commandlinetools-{mac_x86_64,mac_arm64,linux,win}-$(CommandLineToolsVersion).zip` — macOS is arch-split; other hosts are one zip |
| cmake | `cmake;` | `AndroidCmakeVersion` | `XACmakeHashMacOS/Linux/Windows` | `cmake-$(AndroidCmakeVersion)-{darwin,linux,windows}.zip` |
| emulator | `emulator` | `EmulatorVersion`, `EmulatorPkgRevision` | `XAEmulatorHashMacOSx64`, `XAEmulatorHashMacOSArm64`, `XAEmulatorHashLinux/Windows` | `emulator-{darwin_x64,darwin_aarch64,linux_x64,windows_x64}-$(EmulatorVersion).zip`; also drives a synthesized `package.xml` via `package.xml.in` |
| API 29 system image | `sys-img/android` manifest, `path="system-images;android-29;default;{x86_64,arm64-v8a}"` | (fixed `x86_64-29_r08*`/`arm64-v8a-29_r08` filenames — check manifest for a newer `rNN` if refreshing) | `XASystemImageHashMacOSx64/MacOSArm64/Linux/Windows` | `{x86_64,arm64-v8a}-29_r08{-darwin,-linux,-windows,}.zip` under `sys-img/android/` |
| m2repository | `extras;android;m2repository` | (embedded in filename, e.g. `_r47`) | `XAAndroidM2RepositoryHash` | `android_m2repository_r47.zip`, host-agnostic |
| docs | `docs` | (embedded in filename, e.g. `-24_r01`) | `XAAndroidDocsHash` | `docs-24_r01.zip`, host-agnostic |
| sources | `sources;android-NN` (tracks the latest stable platform) | (embedded in filename) | `XAAndroidSourcesHash` | `source-<latest-stable-api>_r0M.zip`, `Destination` embeds the API level too |
| platform APIs | `platforms;android-NN` | n/a — `_PlatformPackage` item's `Include` *is* the version string | `Hash` metadata per `_PlatformPackage` item | `_PlatformPackage` item group near the top of the file; one `IsLatestStable="true"` entry drives default install + the sources package above |
| **Android NDK — OUT OF SCOPE** | `ndk` | `_XAAndroidNdkRelease`, `_XAAndroidNdkPkgRevision` | `XAAndroidNdkHashMacOS/Linux/Windows` | `android-ndk-r$(_XAAndroidNdkRelease)-$(_NdkHostTag).zip` — **never edit as part of this skill** |

## Notes on Apple Silicon archives

`_IsArm64Apple` (computed in `Configuration.props` from `RuntimeInformation.ProcessArchitecture`
on a Darwin host) is the existing gate used to pick an arm64-specific archive when one exists
(emulator, system image, and — as of PR #12371 — cmdline-tools). When Google adds an arm64-specific
archive for a package family that didn't have one before, mirror that same conditional pattern:
an `'$(_IsArm64Apple)' != 'true'` item for the existing x86_64/generic macOS archive, and a new
`'$(_IsArm64Apple)' == 'true'` item pointing at the arm64 archive and a new `*HashMacOSArm64`
property. Don't add an arm64-specific branch speculatively for families where Google still ships
one universal/x86_64-only macOS archive.

## Notes on platform extension levels

Some platform API levels ship as a numbered "extension" (e.g. `platform-34-ext12_r01`) rather than
a bare `platform-NN_rMM`. The extension number tracks Google's Extension SDK program, independent
of the base API level's own revision counter. When refreshing an API level that already uses an
extension suffix, look up the current extension level and revision for that exact API in the
manifest (`dotnet run .github/skills/update-androidsdk-packages/scripts/fetch_repo_package.cs -- --path "platforms;android-34"`) rather than assuming the extension
number increments in lockstep with anything else in the catalog.
