---
name: android-devices
description: >-
  Acquires, targets, shares, and releases Android emulators/devices for
  dotnet/android device tests and runtime experiments. Use before running
  MSBuildDeviceIntegration or Mono.Android.NET-Tests on a device, when starting
  or stopping an emulator, when several sessions need devices on one host, or
  when an on-device run crashes or goes silent. Prefers the `android` CLI shipped
  in the pinned cmdline-tools over hand-rolled emulator/adb scripts.
---

# Android devices

Device tests need a complete local SDK first (see the `local-build` skill) and the commands in the `tests` skill.

## 1. Use the `android` CLI

The Android command-line tools pinned by this repo (`$(CommandLineToolsFolder)` = `23.0` in `Configuration.props`) include Google's `android` CLI, so it is available after `make prepare` at `~/android-toolchain/sdk/cmdline-tools/23.0/bin/android` (or under `$(AndroidSdkDirectory)` if overridden). Call that path directly: other `android` binaries on `PATH` (for example, Homebrew's) may be a different version.

```bash
ANDROID_CLI=~/android-toolchain/sdk/cmdline-tools/23.0/bin/android

$ANDROID_CLI info                              # SDK path and connected devices
$ANDROID_CLI emulator list --long              # AVDs with API level, status, and serial
$ANDROID_CLI emulator start --headless <avd>   # blocks until fully booted; prints "started as 'emulator-NNNN'"
$ANDROID_CLI emulator stop emulator-NNNN       # stops only this emulator; the adb server keeps running
```

**Reuse before creating.** Run `emulator list --long` (and `adb devices -l` for physical devices). If a suitable device is already online and no other session is using it, target it (section 3). If a suitable AVD is offline, start it.

**Creating AVDs.**

  * `android emulator create <profile>` (see `create --list-profiles`, for example `medium_phone`) creates an AVD named after the profile with the CLI's default system image. It cannot choose the API level, ABI, or AVD name.
  * When you need a specific API level or ABI, or a second concurrent AVD, create it with `avdmanager`, then start it with the CLI:

    ```bash
    sdk=~/android-toolchain/sdk
    $ANDROID_CLI sdk install "system-images;android-35;google_apis;arm64-v8a"
    $sdk/cmdline-tools/23.0/bin/avdmanager create avd -n <unique-name> \
        -k "system-images;android-35;google_apis;arm64-v8a" -d pixel_9_pro
    $ANDROID_CLI emulator start --headless <unique-name>
    ```

  * Use x86_64 images instead of arm64-v8a on Intel hosts.
  * One AVD can only run once at a time. Concurrent sessions need distinct AVDs; the emulator picks the next free port (`emulator-5554`, `emulator-5556`, …).
  * Delete AVDs you created when you are done (`$ANDROID_CLI emulator remove <name>`); `~/.android/avd` is shared by every session on the host.

In the GitHub Copilot app, the Mobile Device canvas/tools wrap the same operations.

## 2. CI's MSBuild targets (only to match CI exactly)

CI starts emulators with `Emulator.csproj` (`build-tools/automation/yaml-templates/start-stop-emulator.yaml`), which imports `build-tools/scripts/TestApks.targets`. Use it only when reproducing a CI lane's exact emulator configuration:

```bash
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Emulator.csproj \
    -t:"InstallAvdImage;AcquireAndroidTarget" -p:TestAvdApiLevel=29 -p:TestAvdType=default
```

Its hazards on a shared host:

  * `AcquireAndroidTarget` reuses any valid attached device, unless you pass `-p:RequireNewEmulator=True`.
  * It recreates the `XamarinAndroidTestRunner<api>-<abi>` AVD on every acquire (`TestAvdForceCreation=true`).
  * It always uses port 5570, so only one such emulator can run per host.
  * **`ReleaseAndroidTarget` runs `adb kill-server`**, which disconnects every other session. Stop your emulator with `android emulator stop emulator-5570` instead.

## 3. Target a specific device

Whenever more than one device may be attached, always target one explicitly:

| What | How |
|---|---|
| App build/install/run and on-device `dotnet test` | `-p:Device=<serial>`. It sets `$(AdbTarget)` (`Xamarin.Android.Common.targets:270-273`), which `Microsoft.Android.Run` receives as `--adb-target`. Pass the same `-p:Device=` to both `build -t:Install` and `dotnet test --no-build`. |
| Host-side device tests (`MSBuildDeviceIntegration.dll`, `DeviceTest`/`BaseTest` helpers) | `ADB_TARGET='-s <serial>' ./dotnet-local.sh test ...` |
| Raw `adb` commands | `adb -s <serial> ...`, or export `ANDROID_SERIAL=<serial>` |

## 4. Sharing devices between sessions

- Installing the same test APK (for example `Mono.Android.NET-Tests`) from two sessions onto one device overwrites the other run. Run one instrumentation per device at a time, or give each session its own emulator.
- Keep to the default ADB server. Private ADB servers or ports are rarely needed; prefer distinct emulators and explicit serials.
- Never stop emulators or `adb` processes that you did not start. On Windows, `BaseTest.AfterAllTests` kills every `adb.exe` after `adb kill-server` (`BaseTest.cs`, `AfterAllTests`), so host-side test runs there disrupt all device users.
- When done, stop only your own emulator (`android emulator stop <serial>`), remove AVDs you created, and report the serial in the handoff.

## 5. Narrowing and diagnosing on-device runs

- Restrict `Mono.Android.NET-Tests` to NUnit categories at **build** time with `-p:IncludeCategories=Cat1,Cat2`. The value is written to `runtimeconfig.json` and read on device via `AppContext` (`Mono.Android.NET-Tests.csproj`, `TestInstrumentation`). Pass it to both the install and test commands.
- `PublishAot=true` in Debug still runs CoreCLR unless `Optimize=true` or you are publishing (`Microsoft.Android.Sdk.DefaultProperties.targets`). Check `_AndroidRuntime` in the binlog or the native libraries in the APK before claiming NativeAOT coverage.
- If an instrumentation run goes silent, it crashed; it did not hang. Check `adb -s <serial> logcat -d | grep -E 'FATAL|tombstone|signal'` instead of waiting for a timeout.
