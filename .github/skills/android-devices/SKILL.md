---
name: android-devices
description: >-
  Starts, targets, shares, and stops Android emulators and devices for
  dotnet/android device tests and runtime experiments. Use before running
  MSBuildDeviceIntegration or Mono.Android.NET-Tests on a device, when several
  sessions need devices on one host, or when an on-device run crashes or goes
  silent.
---

# Android devices

Device tests need a local SDK (`local-build` skill) and the commands in the `tests` skill.

## Emulators

Use the `android` CLI from the repo-pinned cmdline-tools. Resolve its directory through MSBuild, which honors SDK overrides such as `ANDROID_SDK_ROOT` or `/mnt/android-toolchain`. Other `android` binaries on `PATH` may be different versions.

```bash
B=$(dotnet msbuild src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Emulator.csproj -getProperty:CommandLineToolsBinPath)
$B/android emulator list --long             # AVDs, API level, status, serial
$B/android emulator start --headless <avd>  # waits for full boot, prints the serial
$B/android emulator stop <serial>           # stops only this emulator
$B/android emulator remove <avd>
```

1. Reuse an idle online device, or start an existing suitable AVD.
2. Otherwise, create an AVD with a unique name. (`android emulator create` can't choose the API level, ABI, or name.) Use x86_64 images on Intel hosts.

   ```bash
   $B/android sdk install "system-images;android-35;google_apis;arm64-v8a"
   $B/avdmanager create avd -n <unique-name> -k "system-images;android-35;google_apis;arm64-v8a" -d pixel_9_pro
   ```

3. When done, stop it and remove AVDs you created. `~/.android/avd` is shared, and each AVD can run only once at a time.

Use CI's `Emulator.csproj` targets (`build-tools/scripts/TestApks.targets`) only to reproduce a CI lane's exact emulator:

```bash
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/Emulator.csproj \
    -t:"InstallAvdImage;AcquireAndroidTarget" -p:TestAvdApiLevel=29 -p:TestAvdType=default
```

Be aware of how these targets behave:

- They reuse any attached device unless you pass `-p:RequireNewEmulator=True`.
- They recreate the AVD every time.
- They always use port 5570.
- **`ReleaseAndroidTarget` runs `adb kill-server`**, which disconnects every session on the host. Use `android emulator stop` instead.

## Targeting

With more than one device attached, always target one:

| Command | Target with |
|---|---|
| App build/install/run, on-device `dotnet test` | `-p:Device=<serial>`, on both `build -t:Install` and `dotnet test --no-build` |
| `MSBuildDeviceIntegration.dll` | `ADB_TARGET='-s <serial>'` |
| Raw `adb` | `adb -s <serial>` or `ANDROID_SERIAL=<serial>` |

## Sharing

- Run one instrumentation per device at a time; two sessions installing the same test APK overwrite each other.
- Use the default ADB server with distinct emulators, not private ADB ports.
- Never stop emulators or `adb` you didn't start. On Windows, `BaseTest.AfterAllTests` kills every `adb.exe`.
- Report your serial in the handoff.

## Diagnosing

- `-p:IncludeCategories=A%2CB` (escape the comma) limits `Mono.Android.NET-Tests` to those categories (the value goes into `runtimeconfig.json`). Pass it to both the install and test commands.
- `PublishAot=true` in Debug still runs CoreCLR unless `Optimize=true` or publishing. Check `_AndroidRuntime` before claiming NativeAOT coverage.
- A silent instrumentation run means the app crashed: run `adb -s <serial> logcat -d | grep -E 'FATAL|tombstone|signal'`.
