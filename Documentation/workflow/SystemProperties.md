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

## Bundled NativeAOT startup data

NativeAOT builds write environment variables and bundled system properties to a
read-only ELF data object, linked into the application's existing native library.
The fixed SDK native host reads the offset-based UTF-8 data directly, without
generated Java configuration arrays or JNI string copies. Optional JNI remapping
is linked as another data object; empty remapping does not produce an object or
a separate `libbinary_blobs.so`. CoreCLR continues to use its binary data library.
Static NativeAOT payload and size symbols use the `dotnet_android_nativeaot_`
prefix to avoid collisions with application or dependency data objects.
Bootstrap regeneration also removes the obsolete generated Java configuration
source from existing intermediate directories.
NativeAOT configuration and remapping objects are always uncompressed. Startup
reads their mapped bytes directly, without allocating decoded buffers; CoreCLR's
private binary-blob compression opt-in does not apply to NativeAOT.
Each payload in CoreCLR's data library starts at a 4-byte-aligned offset, including
compressed payloads, without changing the individual payload bytes or envelope version.

The application linker selects the fixed native environment initializer as
`DT_INIT`, before the application's initialization array and embedded NativeAOT
runtime initialization. It does **not** run before constructors of `DT_NEEDED`
dependencies: Android initializes those libraries first. A dependency constructor
must not depend on an environment variable supplied only by this app's bundled
configuration. Environment values use standard UTF-8, including supplementary
characters, rather than the modified UTF-8 previously passed through Java.

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
