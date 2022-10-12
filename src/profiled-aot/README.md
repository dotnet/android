# Profiled AOT support for Android

This is based on the NuGet package found here:

https://github.com/jonathanpeppers/Mono.Profiler.Android#usage-of-the-aot-profiler

## Updating Profiles

Build Xamarin.Android following our instructions to build on
[Windows][0] or [Unix-like platforms][1]. Make sure to build with
`-p:Configuration=Release` or `CONFIGURATION=Release`.

Run the `Record` target:

```bash
$ ./bin/Release/dotnet/dotnet build src/profiled-aot/build.proj
```

You can also use `-r android-x64`, if you'd prefer an x86_64 emulator.

`dotnet.aotprofile.txt` is a list of method names contained within the
profile. We don't ship these, but we can use them to track changes
over time. Note that they are not always in order, so I opened the
files in VS Code and did Ctrl+Shift+P to get the command palette. Then
`Sort lines ascending` to get them in alphabetical order. If the text
files don't change, it is likely not necessary to update the
`.aotprofile` files.

## Testing Profiles

Once you've updated the profile, always test to make sure the times
you get are either "the same" or slightly better than before.

Build Xamarin.Android again in `Release` mode to get the updated
profiles, or manually copy the new `dotnet.aotprofile` to:

```
./bin/Release/dotnet/packs/Microsoft.Android.Sdk.Windows/*/targets/dotnet.aotprofile
```

Create a new project and run it:

```bash
$ mkdir foo && cd foo
$ ../bin/dotnet/dotnet new android
$ ../bin/dotnet/dotnet build -c Release -t:Run
```

Run the app a few times and make sure you get good launch times:

```bash
$ adb logcat -d | grep Displayed
10-11 10:35:35.178  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +173ms
10-11 10:35:36.540  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +165ms
10-11 10:35:37.865  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +157ms
10-11 10:35:39.201  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +175ms
10-11 10:35:40.568  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +152ms
10-11 10:35:41.920  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +159ms
10-11 10:35:43.261  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +170ms
10-11 10:35:44.641  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +183ms
10-11 10:35:45.986  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +168ms
10-11 10:35:47.333  2092  2370 I ActivityTaskManager: Displayed com.companyname.foo/crc64808a40cc7e533249.MainActivity: +160ms
```

To verify what methods are AOT'd, clear the log and enable AOT logging:

```bash
$ adb logcat -c
$ adb shell setprop debug.mono.log default,timing=bare,assembly,mono_log_level=debug,mono_log_mask=aot
```

Restart the app, and you should be able to see messages like:

```bash
$ adb logcat -d | grep AOT
02-23 09:03:46.327 10401 10401 D Mono    : AOT: FOUND method Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebView:.ctor () [0x6f9efd0150 - 0x6f9efd0340 0x6f9efd260c]
```

Look for any suspicious `AOT NOT FOUND` messages.

Note that it is expected that some methods will say `(wrapper)`:

```bash
02-23 09:03:46.327 10401 10401 D Mono    : AOT NOT FOUND: (wrapper runtime-invoke) object:runtime_invoke_void (object,intptr,intptr,intptr).
02-23 09:03:46.334 10401 10401 D Mono    : AOT NOT FOUND: (wrapper managed-to-native) System.Diagnostics.Debugger:IsAttached_internal ().
02-23 09:03:46.367 10401 10401 D Mono    : AOT NOT FOUND: (wrapper native-to-managed) Android.Runtime.JNINativeWrapper:Wrap_JniMarshal_PPL_V (intptr,intptr,intptr).
```

[0]: ../../Documentation/building/windows/instructions.md
[1]: ../../Documentation/building/unix/instructions.md
