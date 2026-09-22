# Custom Android system properties used by .NET for Android

.NET for Android uses `debug.dotnet.*` Android system properties for runtime
logging and diagnostics. Android limits each property value to 91 characters.
Set a property with:

```sh
adb shell setprop property_name property_value
```

Clear a property with:

```sh
adb shell setprop property_name "''"
```

## debug.dotnet.log

Configures native .NET for Android runtime logging. The value is a
comma-separated list of `NAME[=VALUE]` categories:

* `all` enables all categories.
* `assembly` logs assembly lookup and loading.
* `default` enables general runtime messages.
* `debugger` logs debugger-related native runtime messages.
* `gc` logs garbage-collection and JNI reference messages.
* `gref`, `gref=FILE`, and `gref+` enable global JNI reference logging.
* `lref`, `lref=FILE`, and `lref+` enable local JNI reference logging.
* `network` and `netlink` log native network activity.
* `timing=bare` logs native timing events immediately.
* `timing=fast-bare` buffers native timing events until the
  `mono.android.app.DUMP_TIMING_DATA` broadcast is received.

NativeAOT supports `gref`, `gref=FILE`, `lref`, `lref=FILE`, and `all` for
managed JNI reference logging.

The `gref` and `lref` forms write complete logs to `grefs.txt` and
`lrefs.txt` under the application's `files/.__override__` directory. The `+`
forms write best-effort output to logcat and can affect application
performance.

Example:

```sh
adb shell setprop debug.dotnet.log default,assembly,gref
```

## debug.dotnet.max_grefc

Overrides the maximum number of JNI global references. The default is `2000`
on an emulator and `51200` on a device. Suffix a value with `k` or `m` for
thousands or millions:

```sh
adb shell setprop debug.dotnet.max_grefc 100k
```

## debug.dotnet.profile

CoreCLR uses the presence of this property when deciding whether runtime
diagnostic output directories are required. It does not configure EventPipe
diagnostic ports.

Configure CoreCLR diagnostic ports with the `DiagnosticConfiguration`,
`DiagnosticAddress`, `DiagnosticPort`, `DiagnosticSuspend`, and
`DiagnosticListenMode` MSBuild properties. These settings generate the
standard `DOTNET_DiagnosticPorts` environment variable.

## debug.dotnet.timing

Configures fast timing output for CoreCLR and supported NativeAOT/shared
runtime paths. Supported comma-separated options are:

* `to-file`
* `filename=FILE`
* `duration=MILLISECONDS`

Example:

```sh
adb shell setprop debug.dotnet.log timing=fast-bare
adb shell setprop debug.dotnet.timing to-file,filename=fast-timing.txt
```

After the application starts, request buffered timing output:

```sh
adb shell am broadcast -a mono.android.app.DUMP_TIMING_DATA PACKAGE_NAME
```
