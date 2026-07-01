# FastDeploy2

`FastDeploy2` is the fast-deployment strategy used by `Install` builds (it is the
default; the legacy strategy is still available as `FastDeploy`). Fast deployment
keeps the installed `.apk` small and avoids a full re-install on every `F5`: the
application assemblies (and, optionally, environment files) are pushed to the
device separately and surfaced to the app through an *override directory*, so an
inner-loop change only re-transfers the files that actually changed.

This document describes how the [`FastDeploy2`][task] MSBuild task works: the
stages it runs, the `adb` commands it issues, and the properties that control it.

[task]: ../../src/Xamarin.Android.Build.Debugging.Tasks/Tasks/FastDeploy2.cs

## MSBuild properties

The task is invoked from `Xamarin.Android.Common.Debugging.targets`. The
properties intended for end users are:

| Property | Default | Description |
| --- | --- | --- |
| `$(_AndroidFastDevStrategy)` | `FastDeploy2` | `FastDeploy` or `FastDeploy2`. Set to `FastDeploy` to fall back to the legacy strategy. |
| `$(_AndroidFastDeployAppFileTransferMode)` | `Symlink` (for `FastDeploy2`) | How staged files are surfaced in the override directory: `Symlink` or `Copy`. |
| `$(AndroidFastDeploymentAdbCompressionAlgorithm)` | `any` | The `adb push -z` compression algorithm. `FastDeploy2` relies on a modern Android SDK Platform-Tools `adb` for multi-file `push -z` support. |

The following internal/unsupported properties tune batching. They exist mainly so
the batching paths can be exercised with smaller batches while testing; their
defaults match the matching task properties:

| Property | Default | Description |
| --- | --- | --- |
| `$(_AndroidFastDeployStaleFileRemovalBatchSize)` | `100` | Number of stale override files deleted per `rm` invocation. |
| `$(_AndroidFastDeployCopyBatchSize)` | `25` | Number of files copied per batch when staging fast-deployment files. |
| `$(_AndroidFastDeployMaxShellCommandLength)` | `900` | Maximum length of a single `adb shell` command line before it is split. |
| `$(_AndroidFastDeployMaxAdbCommandLength)` | `4096` | Maximum length of a single `adb` command line before it is split. |

## On-device layout

* **Staging directory:** `/data/local/tmp/fastdeploy2/<package-name>/<user-id>`.
  Files are pushed here first (this location is writable by `adb` without
  `run-as`).
* **Override directory:** `files/.__override__` inside the application's private
  data directory (resolved with `run-as`). The runtime loads assemblies from here
  in preference to the ones embedded in the `.apk`.
* **Manifest markers:** a `.fastdeploy2-manifest-hash` file is written to both the
  staging and override directories. It records the hash of the last successfully
  deployed manifest so the next build can detect whether the device is already up
  to date and skip redundant work.

## Stages

### 1. Resolve the device

The target device is resolved from `$(AdbTarget)` via `AndroidHelper.ParseTarget`
(which lists devices with `adb devices`). Only the resolved device id is kept; it
is passed to every subsequent command as `adb -s <id> …`.

### 2. Validate device state

Two system properties are read and the deployment is aborted with a coded error
if either makes fast deployment unsafe:

```
adb shell getprop log.redirect-stdio   # XA0128 if "true"
adb shell getprop ro.boot.disable_runas # XA0131 if "true"
```

### 3. Inspect the installed app

`CheckAppInstalledAndDebuggable` discovers the application's private data
directory and current process id, and detects whether the package is installed,
debuggable, or a system application. It runs (via `run-as`, falling back to `su`
for system apps):

```
adb shell run-as <package> sh -c 'pwd; pidof <package> 2>/dev/null || true'
```

Depending on the output it may force a re-install (package not debuggable) or
treat the package as not installed.

### 4. (Re)install the `.apk` when needed

The `.apk` is (re)installed when it is out of date, when `ReInstall` is set, or
when the app is not yet installed. Installation uses `adb install`:

```
adb install -r -d [-t] [--user <id>] <path-to-apk>
```

* `-r` is added when reinstalling, `-d` always allows a version downgrade
  (matching the legacy behavior on API 19+), and `-t` allows test packages.
* On an `INSTALL_FAILED_ALREADY_EXISTS` failure the package is uninstalled
  preserving data (`pm uninstall -k`) and the install is retried.
* On an "incompatible/requires uninstall" failure
  (`INSTALL_FAILED_UPDATE_INCOMPATIBLE`, `INSTALL_PARSE_FAILED_INCONSISTENT_CERTIFICATES`,
  `INSTALL_FAILED_VERSION_DOWNGRADE`, …) the package is fully uninstalled and the
  install is retried.
* Other failures are reported with an `ADB####` error code (for example,
  `ADB0020` for an incompatible ABI or `ADB0060` for insufficient storage).

If `$(EmbedAssembliesIntoApk)` is `true`, the override directory is removed and
deployment stops here — there are no separate files to push.

### 5. Terminate the running app

Before swapping files, the app is stopped so it reloads them on next launch:

```
adb shell pidof <package>        # only for system apps; otherwise the pid from stage 3 is used
adb shell am force-stop <package>
```

### 6. Deploy the fast-deployment files

This is the incremental core (`DeployFastDevFilesWithAdbPush`):

1. **Build the current manifest.** Each file to deploy is hashed; the set of
   `{ relative-path → hash }` forms the manifest.
2. **Compare against the device.** The previous manifest is read from `obj`, and
   the on-device `.fastdeploy2-manifest-hash` markers are read to confirm the
   device still matches it. If the staging directory is not in the expected
   state it is reset:
   ```
   adb shell rm -rf <staging-dir>
   ```
3. **Create staging directories** for the files being deployed:
   ```
   adb shell mkdir -p <dir> [<dir> …]   # batched up to MaxShellCommandLength
   ```
4. **Remove stale files** that are no longer part of the app:
   ```
   adb shell rm -f <file> [<file> …]    # batched up to StaleFileRemovalBatchSize / MaxAdbCommandLength
   ```
5. **Upload changed files** (only files whose hash changed), grouped by
   directory and batched up to `MaxAdbCommandLength`:
   ```
   adb push -z <algorithm> <local-file> [<local-file> …] <remote-dir>
   ```
6. **Update the override directory** so it points at the freshly staged files,
   using one of two modes:
   * **`Symlink` (default):** for each directory, symlink the staged files into
     the override directory, leaving subdirectories untouched. Roughly:
     ```
     adb shell run-as <package> sh -c \
       'd=<override-dir>;s=<staging-dir>;mkdir -p "$d"&&cd "$d"&& \
        for e in ./*;do [ -d "$e" ]||rm -f "$e";done&& \
        for f in "$s"/*;do [ -d "$f" ]||ln -sf "$f" .;done'
     ```
     If the device does not support symlinking into the override directory, the
     task automatically falls back to `Copy`.
   * **`Copy`:** the staged files are copied into the override directory instead
     of symlinked.
7. **Mark success.** When the override directory is up to date, the current
   manifest hash is written to the staging and override markers, and the manifest
   is saved to `obj` for the next incremental build.

## Error codes

Install failures are reported with `ADB####` codes; fast-deployment shell
failures (`mkdir`/`rm`/`push`/`ln`) are reported with `XA0129`. `run-as`
diagnostics map to `XA0131`–`XA0137`. See the
[build/deploy message docs](../docs-mobile/messages/index.md) for details.
