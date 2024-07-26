<!-- markdown-toc start - Don't edit this section. Run M-x markdown-toc-refresh-toc -->
**Table of Contents**

- [Custom Android system properties used by .NET for Android](#custom-android-system-properties-used-by-xamarinandroid)
    - [Introduction](#introduction)
    - [Known properties](#known-properties)
        - [debug.mono.connect](#debugmonoconnect)
        - [debug.mono.debug](#debugmonodebug)
        - [debug.mono.env](#debugmonoenv)
        - [debug.mono.extra](#debugmonoextra)
        - [debug.mono.gc](#debugmonogc)
        - [debug.mono.gdb](#debugmonogdb)
        - [debug.mono.log](#debugmonolog)
        - [debug.mono.max_grefc](#debugmonomax_grefc)
        - [debug.mono.profile](#debugmonoprofile)
        - [debug.mono.runtime_args](#debugmonoruntime_args)
        - [debug.mono.soft_breakpoints](#debugmonosoft_breakpoints)
        - [debug.mono.trace](#debugmonotrace)
        - [debug.mono.wref](#debugmonowref)

<!-- markdown-toc end -->
# Custom Android system properties used by .NET for Android

## Introduction

.NET for Android uses a number of custom Android system properties to
communicate settings to application at the run time.  Each property
value can be no longer than 91 characters (as per Android OS limits)
and can be set from the command line using the following syntax:

```bash
$ adb shell setprop property_name property_value
```

To unset any of the properties, the following syntax needs to be used:

```bash
$ adb shell setprop property_name "''"
```

## Known properties

### debug.mono.connect

Used mostly by the IDEs to set arguments for the remote debugger
connection required to attach the debugger to application running on
device.  Arguments must be specified as a comma-separated list without
any whitespace. Known arguments:

  * `port=PORT`
    Debugger port (a short integer)
  * `timeout=VALUE`
    Debugger session start timeout (in milliseconds)

### debug.mono.debug

Indicates that Mono debug session needs to be initialized.  Property
value doesn't matter, only its presence is checked.

### debug.mono.env

Specifies a list of environment properties to set.  Property is only
used in Debug builds of .NET for Android applications.  Its value
is a list of `NAME=VALUE` pairs separated with the pipe character
(`|`), without any whitespace surrounding each of the pipe separators.

### debug.mono.extra

Contains a whitespace separated list of additional command line
arguments to pass to the Mono runtime initialization function.

### debug.mono.gc

Enable GC logging if set to any non-empty value.  Property is only
used in Debug builds of .NET for Android applications.

### debug.mono.gdb

Set additional parameters when starting a .NET for Android application
under GDB.  Each argument follows the `NAME:VALUE` format.  Supported
arguments:

  * `wait:TIMESTAMP`
   `TIMESTAMP` should be the output of date +%s in the android shell.
   If this property is set, wait for a native debugger to attach by
   spinning in a loop.  If the current time is later than
   `TIMESTAMP` + 10s, the property is ignored.

### debug.mono.log

Configure the .NET for Android runtime categories.  By default only the
`default` category is enabled, which logs a handful of messages during
application startup and operation.  This property enables all the
messages logged below the `info` level for any of the categories.
Value of the property is a comma-separated list of `NAME[=VALUE]`
categories:

  * `assembly`
    Log all messages related to embedded assembly activities
    (lookup, typemaps, loading etc).
  * `bundle`
    Log all messages related to bundled app processing.
  * `debugger-log-level=LEVEL`
    Set the Mono soft debugger logging level.
  * `debugger`
    Log all messages related to setting up the Mono soft debugger.
  * `default`
    Enable messages that don't belong to any of the other, more
    specific, categories.
  * `gc`
    Log garbage collector messages.
  * `gref-`
    Enable global reference logging but without writing the logged
    messages to a file.
  * `gref=FILE`
    Enable global reference logging and write messages to the
    specified `FILE`
  * `gref`
    Enable global reference logging and log messages to the default
    `grefs.txt` file.
  * `gref+`
    Enable global reference logging, writing messages to `adb logcat`.
    ***Note***: this will spam `adb logcat`, possibly impacting app
    performance, and Android might not preserve all messages.
    This is provided as a "last ditch effort", and is not as reliable
    as the normal `gref` or `gref=` options which write to a file.
    Added in Xamarin.Android 12.2.
  * `lref-`
    Enable local reference logging but without writing the logged
    messages to a file.
  * `lref=FILE`
    Enable local reference logging and write messages to the
    specified `FILE`
  * `lref`
    Enable local reference logging and log messages to the default
    `lrefs.txt` file, unless `gref` or `gref=` are also present, in
    which case messages will be logged to the `gref` file.
  * `lref+`
    Enable local reference logging, writing messages to `adb logcat`.
    ***Note***: this will spam `adb logcat`, possibly impacting app
    performance, and Android might not preserve all messages.
    This is provided as a "last ditch effort", and is not as reliable
    as the normal `lref` or `lref=` options which write to a file.
    Added in Xamarin.Android 12.2.
  * `mono_log_level=LEVEL`
    Set Mono runtime log level.  The default value is `error` to log
    only errors, unless `gc` or `assembly` log categories are enabled,
    in which case the default value for this property is `info`.
  * `mono_log_mask=MASK`
    Set Mono runtime log mask.  Value of this argument is a list of
    categories separated with `:`, without any whitespace surrounding
    the separator.  Full list of categories is [available here](https://www.mono-project.com/docs/advanced/runtime/logging-runtime-events/#trace-filters)
  * `netlink`
    Enable logging of low-level Linux `netlink` device used to
    enumerate network interfaces and their associated IP addresses.
  * `network`
    Enable logging for messages related to network activity.
  * `timing=bare`
    Enable logging of native code performance information, without
    logging method execution timing information to a file.  Timed
    events are logged to `logcat` immediately, which affects the
    accuracy of measurements and introduces an element of
    unpredictability to measurements.
  * `timing=fast-bare`
    Similar to `timing=bare` above, but the timings aren't logged
    until the `mono.android.app.DUMP_TIMING_DATA` broadcast is sent to
    the application.  This can be done with `adb shell am broadcast -a
    mono.android.app.DUMP_TIMING_DATA [PACKAGE_NAME]` command.
  * `timing`
    Enable logging of native code performance information, including
    method execution timing which is written to a file named
    `methods.txt`.  `timing=bare` should be used in preference to this
    category.
  * `native-tracing`
    Enable built-in tracing capabilities, using default settings.
    Tracing settings can be tuned using the [debug.mono.native-tracing](#debugmononative-tracing)
    property.

### debug.mono.native-tracing

[Full documentation](../guides/native-tracing.md)

Available options:


#### Timing events format

In any of the timing modes, the logged messages have the following
format:

```
<TAG>: [<S>/<E>] <MESSAGE>; elapsed s:ms::ns
```

Where:

All lower case and punctuation characters are verbatim in every message

`<TAG>` is always `monodroid-timing`

`<S>` denotes a "stage":

  * `0`: events before control is handed over to the managed runtime
    (the "native init" stage)
  * `1`: events after the above "native init" stage
  * `2`: used only in the `timing=fast-bare` mode to mark the summary
    information.

`<E>` denotes an "event":

  * For the `0` and `1` stages it's one of
    * `0`: assembly decompression
    * `1`: assembly load
    * `2`: assembly preload
    * `3`: initialization and start of debugging
    * `4`: timing subsystem initialization
    * `5`: java-to-managed type lookup
    * `6`: managed-to-java type lookup
    * `7`: Mono runtime initialization
    * `8`: native to managed transition (a call to
      `Android.Runtime.JNIEnv.Initialize`)
    * `9`: time spent registering the runtime config blob on NET6+
    * `10`: managed type registration
    * `11`: total time spent initializing the native runtime
    * `12`: unspecified event
  * For the `2` stage it's one of:
    * `1`: mode marker message logged at the very beginning of the app
    * `2`: performance results "heading"
    * `3`: no events logged message
    * `4`: accumulated results "heading"
    * `5`: total time spent loading assemblies
    * `6`: total time spent performing java-to-managed type lookups
    * `7`: total time spent performing managed-to-java type lookups

The format is meant to make it easier for scripts/other software which
look at the log to find timing events without having to rely on the
actual wording of the event message.

#### Reading the timing information

`timing` and `timing=bare` modes write all the event messages to
`logcat` immediately, so it is sufficient to either watch the `logcat`
buffer in real time:

```
adb logcat
```

or dump the buffer it after the timing sequence is entirely recorded:

```
adb logcat -d > logcat.txt
```

The `timing=fast-bare` mode is slightly more involved in that in
requires the broadcast to be sent before data is logged in `logcat`.
The following sequence of commands can be placed in a script and used
to obtain the information:

```shell
# Enable only timing log messages, to minimize impact on application
# performance
$ adb shell setprop debug.mono.log timing=fast-bare

# Build and install the application
$ dotnet build -t:Install -p:Configuration=Release

# Increase logcat buffer size so that we don't miss any messages
$ adb logcat -G 16M

# Clear logcat buffer
$ adb logcat -c

# Start the activity and wait for the startup process to finish. This
# is marked by Android by logging the `Displayed` message in logcat,
# formatted similarly to:
#
#  ActivityTaskManager: Displayed PACKAGE_NAME/ACTIVITY_NAME: +398ms
#
$ adb shell am start -n PACKAGE_NAME/ACTIVITY_NAME -S -W

# Tell the timing infrastructure to log all the gathered events to
# logcat:
$ adb shell am broadcast -a mono.android.app.DUMP_TIMING_DATA PACKAGE_NAME

# Dump logcat contents into a file
adb logcat -d > logcat.txt
```

Technically, the broadcast can be sent to all applications running on
the device, but in event there are more .NET for Android applications
which happen to have timing enabled, the output could be confusing so
it's better to send the broadcast to a specific application/package
only.

### debug.mono.max_grefc

If set, override the number of maximum global references.  The number
defaults to `2000` if the application is running in an emulator and
`51200` otherwise.

### debug.mono.profile

In "legacy" Xamarin.Android applications (that is not NET6+ ones),
value of this property specifies argument to the Mono logging
profiler.

In NET6+ applications, the only accepted value is `[aot:]PORTS` where
`PORTS` becomes value of the `DOTNET_DiagnosticPorts` environment
variable used by the NET6+ profiling infrastructure to configure the
client/server ports.

### debug.mono.runtime_args

Additional arguments passed to the Mono's `mono_jit_parse_options`
function.

### debug.mono.soft_breakpoints

If set to `0`, disable Mono debugger's soft breakpoints.  By default
breakpoints are enabled.

### debug.mono.trace

Set Mono JIT trace options, passed to the Mono's
`mono_jit_set_trace_options` function.

### debug.mono.wref

Configures use of Java (value `java`) or JNI (value `jni`) weak
references.
