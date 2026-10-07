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
* `gc` logs garbage-collection messages.
* `network` logs native network activity.
* `timing` enables the `monodroid-timing` category used by the obsolete
  managed `Android.Runtime.TimingLogger` API.

JNI global, weak-global, and local reference diagnostics now use the
`Microsoft.Android.Runtime` EventSource on CoreCLR and NativeAOT. The former
`gref`, `gref=FILE`, `gref+`, `gref-`, and corresponding `lref` categories
are no longer supported. See [collecting reference events](../guides/internals/debug-jni-objrefs.md#collect-logs).

Example:

```sh
adb shell setprop debug.dotnet.log default,assembly
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

The native runtime no longer emits timing events of its own. Runtime, JIT, and
loader timing data is available through EventPipe, and Android-specific GC
bridge and type-map timings are published by the `Microsoft.Android.Runtime`
EventSource provider.
