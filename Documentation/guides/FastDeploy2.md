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
| `$(AndroidFastDeploymentAdbCompressionAlgorithm)` | `any` | The `adb push -z` compression algorithm. `FastDeploy2` relies on a modern Android SDK Platform-Tools `adb` for multi-file `push -z` support. |

The following internal/unsupported properties tune or disable implementation
details:

| Property | Default | Description |
| --- | --- | --- |
| `$(_AndroidFastDeployStaleFileRemovalBatchSize)` | `100` | Number of stale override files deleted per `rm` invocation. |
| `$(_AndroidFastDeployCopyBatchSize)` | `25` | Number of files copied per batch when staging fast-deployment files. |
| `$(_AndroidFastDeployMaxShellCommandLength)` | `900` | Maximum length of a single `adb shell` command line before it is split. |
| `$(_AndroidFastDeployMaxAdbCommandLength)` | `4096` | Maximum length of a single `adb` command line before it is split. |

## On-device layout

* **Staging directory:** `/data/local/tmp/fastdeploy2/<package-name>/<user-id>`.
  Files are pushed here first (this location is writable by `adb` without
  `run-as`) and the directory is removed after each deployment attempt.
* **Override directory:** `files/.__override__` inside the application's private
  data directory (resolved with `run-as`). The runtime loads assemblies from here
  in preference to the ones embedded in the `.apk`.
* **Manifest marker:** a `.fastdeploy2-manifest-hash` file is written to the
  override directory. It records the hash of the last successfully deployed
  manifest so the next build can detect whether the private files match the
  local manifest and skip redundant work.

Changing `$(_AndroidFastDevStrategy)` invalidates the deployment configuration.
Switching between `FastDeploy2` and legacy `FastDeploy` removes the managed
override tree before the selected strategy runs. The installed package and
unrelated application data are preserved. The first deployment after upgrading
from a symlink-based FastDeploy2 version also clears the old override tree so
all files are recreated as private regular files.

## Stages

### 1. Resolve the device

The target device is resolved from `$(AdbTarget)` via `AndroidHelper.ParseTarget`
(which lists devices with `adb devices`). Only the resolved device id is kept; it
is passed to every subsequent command as `adb -s <id> …`.

### 2. Validate warm device state

When the APK and local FastDeploy2 manifest are current, one tagged
`adb shell` probe performs the warm-path validation. It reads both compatibility
properties, the app-private path and override marker through `run-as`, and the
current process id. If the app is running, the same shell invocation force-stops
it after `run-as` succeeds.

The deployment is aborted with a coded error if either compatibility property
makes fast deployment unsafe:

```
adb shell getprop log.redirect-stdio   # XA0128 if "true"
adb shell getprop ro.boot.disable_runas # XA0131 if "true"
```

If the compound probe is incomplete or cannot prove that `run-as` works, the
task falls back to the individual property and package checks below. This keeps
the detailed `XA0128` and `XA0131`–`XA0137` diagnostics while avoiding their
separate `adb` startup cost on a healthy incremental deployment.

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

On the warm path this work is included in the compound probe from stage 2, so
there is no separate process-id or force-stop `adb` invocation.

### 6. Deploy the fast-deployment files

This is the incremental core (`DeployFastDevFilesWithAdbPush`):

1. **Build the current manifest.** Each file to deploy is recorded with its
   size and last-write time; the set of
   `{ relative-path → (size, mtime) }` forms the manifest. A single SHA256 hash
   over the whole manifest is used as the device readiness marker (see below).
2. **Compare against private device state.** The previous manifest is read from
   `obj`, and the app-private `.fastdeploy2-manifest-hash` marker is read through
   `run-as`. If it does not match, the override directory is cleared and all
   current files are treated as changed.
3. **Reset and create transient staging.** Any staging left by an interrupted
   deployment is removed, then directories are created for changed files:
   ```
   adb shell rm -rf <staging-dir>
   adb shell mkdir -p <dir> [<dir> …]   # batched up to MaxShellCommandLength
   ```
4. **Upload changed files** (only files whose size or last-write time changed),
   grouped by
   directory and batched up to `MaxAdbCommandLength`:
   ```
   adb push -z <algorithm> <local-file> [<local-file> …] <remote-dir>
   ```
5. **Update private storage.** Files removed from the manifest are deleted from
   the override directory. Changed files are copied from staging with
   `run-as cp -p`; existing destinations are removed first so this also replaces
   override symlinks created by an older FastDeploy2 version:
   ```
   adb shell run-as <package> rm -f <removed-or-changed-file> [...]
   adb shell run-as <package> cp -p <staged-file> [...] <override-dir>
   ```
6. **Mark success and remove staging.** The current manifest hash is written to
   the private override marker, the manifest is saved to `obj`, and the staging
   directory is removed. Staging removal also runs when upload or copy fails.

## Error codes

Install failures are reported with `ADB####` codes; fast-deployment shell
failures (`mkdir`/`rm`/`push`/`cp`) are reported with `XA0129`. `run-as`
diagnostics map to `XA0131`–`XA0137`. See the
[build/deploy message docs](../docs-mobile/messages/index.md) for details.

## Command Compatibility

.NET for Android supports Android 7.0 (API level 24) and later. FastDeploy2's
device-side commands are available by Android 6.0 (API level 23), before the
supported device floor. The API levels below are approximate because shell
utilities are not Android SDK APIs.

Host-side `adb` commands depend on the installed Android SDK Platform-Tools
version rather than the device API level:

| Command | FastDeploy2 use | Compatibility |
| --- | --- | --- |
| `adb devices`, `adb -s <device> shell ...` | Device selection and all device-side operations | Standard Platform-Tools commands |
| `adb install -r -d [-t] [--user <id>]` | APK installation and replacement | Standard Platform-Tools command; `--user` corresponds to Android multi-user support introduced in API 17 |
| `adb push -z <algorithm>` | Batched compressed staging-file upload | Modern Platform-Tools capability; not controlled by the device application API level |

FastDeploy2 uses these device-side commands and shell features:

| Command or shell feature | FastDeploy2 use | Approximate availability |
| --- | --- | --- |
| `sh`/mksh syntax, `[ ... ]`, `test`, `pwd`, `echo`, command substitution, and redirection | Combined checks, private marker reads/writes, and warm-state validation | API 14; Android has used mksh since Android 4.0 |
| `getprop` | Validate `run-as` compatibility properties | API 1 |
| `run-as <package>` | Access the private data directory of a debuggable app | Early Android; availability alone is insufficient because the package must be debuggable and the device must permit `run-as` |
| `su <user>` | Access files for a system application when adbd is not root | Not guaranteed on production devices; used only for the system-app fallback |
| `cat`, `true`, `rm -f`, `rm -rf`, `mkdir -p`, and `cp -p` | Read markers and manage transient staging and private override files | API 21 or earlier; supplied by toolbox/BSD utilities before toybox |
| `readlink -f`, `whoami` | Resolve system-app paths and determine whether adbd is root | Reliably available by API 23 |
| `pidof` | Find the running application process | Reliably available by API 23 |
| `printf %s` | Write manifest hash markers without a trailing newline | API 23; supplied by toybox starting in Android 6.0 |
| `pm uninstall [-k] [--user <id>]` | Remove an incompatible package before retrying installation | Base command predates the supported floor; `--user` requires API 17 multi-user support |
| `am force-stop <package>` | Stop the app before replacing fast-deployment files | API 8 or earlier |
| `am start-user -w <id>` | Ensure a secondary Android user is running before `run-as` | API 17 multi-user support |

These estimates are based on the
[AOSP shell and utility inventories][aosp-shell-utilities], the
[Android 6.0 toybox build][aosp-marshmallow-toybox], and the
[Android Debug Bridge documentation][adb-docs].

[aosp-shell-utilities]: https://android.googlesource.com/platform/system/core/+/refs/heads/main/shell_and_utilities/README.md
[aosp-marshmallow-toybox]: https://android.googlesource.com/platform/external/toybox/+/android-6.0.1_r81/Android.mk
[adb-docs]: https://developer.android.com/tools/adb
