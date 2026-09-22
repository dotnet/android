# Profiling .NET for Android

This guide covers startup, runtime, native, and build profiling for current
.NET for Android applications. For managed CPU traces, GC dumps, EventPipe,
and diagnostic-port setup, see
[Tracing .NET for Android applications](tracing.md).

This document explains many uses of `adb` at the command line. On Windows,
you will need either `adb.exe` in your `%PATH%` or a
[PowerShell alias][set_alias] for `adb`.

[set_alias]: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/set-alias

## Overall Startup

The easiest way to time the complete startup is to launch the app by tapping
the icon, and run:

    # Windows / PowerShell
    > adb logcat -d | Select-String Displayed

    # macOS / Unix
    > adb logcat -d | grep Displayed

Both platforms will output something such as:

    09-04 14:33:32.466  1169  1192 I ActivityManager: Displayed com.xamarin.android.helloworld/example.MainActivity: +1s3ms

The `ActivityManager` system process logs how long it takes for any Android
activity to start. This is the best measure of the overall time experienced by
a user when launching the app.

You can also run `adb logcat -c` to clear the log at any point.

## Native runtime timing

Enable native .NET for Android timing messages with `debug.dotnet.log`:

```sh
adb shell setprop debug.dotnet.log timing=bare
```

Launch the application and inspect the timing category:

```sh
adb logcat -d | grep monodroid-timing
```

For buffered timing with lower measurement overhead, use `timing=fast-bare`
and request the results after startup:

```sh
adb shell setprop debug.dotnet.log timing=fast-bare
adb shell am start -S -W PACKAGE_NAME/ACTIVITY_NAME
adb shell am broadcast -a mono.android.app.DUMP_TIMING_DATA PACKAGE_NAME
adb logcat -d | grep monodroid-timing
```

The optional `debug.dotnet.timing` property controls fast-timing file output:

```sh
adb shell setprop debug.dotnet.timing to-file,filename=fast-timing.txt
```

Clear the settings when finished:

```sh
adb shell setprop debug.dotnet.log "''"
adb shell setprop debug.dotnet.timing "''"
```

## Managed runtime profiling

Use CoreCLR EventPipe tools for managed profiling:

* `dotnet-trace` for CPU sampling and provider events.
* `dotnet-gcdump` for managed heap dumps.
* `dotnet-dsrouter` to connect those tools to an Android device or emulator.

The [tracing guide](tracing.md) documents `EnableDiagnostics`,
`DiagnosticAddress`, `DiagnosticPort`, `DiagnosticSuspend`, and
`DiagnosticListenMode`. These properties generate `DOTNET_DiagnosticPorts`
without requiring a runtime-specific profiler component.

## Native code profiling

Use Android's [`simpleperf`](https://developer.android.com/ndk/guides/simpleperf)
for native CPU profiling. Build a debuggable application, install it, and use
the `app_profiler.py` script from the Android NDK:

```sh
python $ANDROID_NDK/simpleperf/app_profiler.py \
  -p PACKAGE_NAME \
  -lib APP_OUTPUT_DIRECTORY \
  -r "-g --duration 10"
```

Generate a report with:

```sh
python $ANDROID_NDK/simpleperf/report_html.py
```

## MSBuild profiling

Capture a binary log for build analysis:

```sh
dotnet build -bl
```

Open the resulting `msbuild.binlog` with
[MSBuild Structured Log Viewer](https://msbuildlog.com/). Binary logs contain
the evaluated project, imported files, task inputs, and build output, so review
them for sensitive information before sharing.
