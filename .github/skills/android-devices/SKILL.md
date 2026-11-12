---
name: android-devices
description: >-
  Acquires, targets, shares, and releases Android emulators/devices for
  dotnet/android device tests and runtime experiments. Use before running
  MSBuildDeviceIntegration or Mono.Android.NET-Tests on a device, when starting
  or stopping an emulator, when several sessions need devices on one host, or
  when an on-device run crashes or goes silent. Prefer this over hand-rolled
  avdmanager/emulator/adb scripts.
---

# Android devices

Device tests need a complete local SDK first (see the `local-build` skill) and the commands in the `tests` skill.

## 1. Reuse before creating

```bash
adb devices -l
```

If a suitable emulator or device is already online and no other session is using it, target it (section 3) instead of creating another one. In the GitHub Copilot app, the Mobile Device canvas/tools can also list, create, and boot emulators.

## 2. Start an emulator the same way CI does

CI uses `Emulator.csproj` (`build-tools/automation/yaml-templates/start-stop-emulator.yaml`), which imports `build-tools/scripts/TestApks.targets`:

```bash
# Optional: download a specific system image first
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Emulator.csproj \
    -t:"InstallAvdImage;AcquireAndroidTarget" -p:TestAvdApiLevel=35 -p:TestAvdType=google_apis

# Default image
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Emulator.csproj \
    -t:AcquireAndroidTarget
```

Behavior to know (`TestApks.targets`):

- `AcquireAndroidTarget` first checks `$(AdbTarget)` (or the single attached device) and **reuses it if it is valid**. Pass `-p:RequireNewEmulator=True` to always start a new one.
- The defaults are API 29, the `default` image type, arm64-v8a on Apple Silicon (otherwise x86_64), the `pixel_4` profile, and the AVD name `XamarinAndroidTestRunner<api>-<abi>`.
- `TestAvdForceCreation` defaults to `true`, so the AVD is recreated on each acquire. Pass `-p:TestAvdForceCreation=false` to keep an existing AVD.
- The emulator always listens on **port 5570 (`emulator-5570`)**, so these targets run only one emulator per host. For a second concurrent emulator, boot another AVD on a different even port (`$ANDROID_HOME/emulator/emulator -avd <name> -port 5580 -no-window -no-boot-anim`) or use the Mobile Device tools.
- **`ReleaseAndroidTarget` also runs `adb kill-server`**, which disconnects every other session on the host. When other sessions may be using devices, stop only your own emulator: `adb -s emulator-5570 emu kill`.

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
- When done, stop only your own emulator and report its serial in the handoff.

## 5. Narrowing and diagnosing on-device runs

- Restrict `Mono.Android.NET-Tests` to NUnit categories at **build** time with `-p:IncludeCategories=Cat1,Cat2`. The value is written to `runtimeconfig.json` and read on device via `AppContext` (`Mono.Android.NET-Tests.csproj`, `TestInstrumentation`). Pass it to both the install and test commands.
- `PublishAot=true` in Debug still runs CoreCLR unless `Optimize=true` or you are publishing (`Microsoft.Android.Sdk.DefaultProperties.targets`). Check `_AndroidRuntime` in the binlog or the native libraries in the APK before claiming NativeAOT coverage.
- If an instrumentation run goes silent, it crashed; it did not hang. Check `adb -s <serial> logcat -d | grep -E 'FATAL|tombstone|signal'` instead of waiting for a timeout.
