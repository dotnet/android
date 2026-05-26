# Typemap OTEL Aspire sample

This AppHost launches `samples/HelloWorld` as a Release/CoreCLR/trimmable typemap Android app and sends typemap telemetry to the Aspire dashboard over OTLP/HTTP.

## What it measures

The runtime emits spans and metrics from the `Microsoft.Android.Runtime.TrimmableTypeMap` instrumentation source.

Important startup spans:

- `jnienv.initialize`: managed `JNIEnvInit.Initialize` startup boundary.
- `typemap.data.initialize`: generated trimmable typemap data load.
- `typemap.initialize`: runtime typemap initialization.
- `typemap.register_native_methods`: native registration setup.
- `typemap.buffered_events`: flush marker for operations buffered before the app configured OpenTelemetry.

Lookup and steady-state spans:

- `typemap.llvm.lookup.jni_name`
- `typemap.llvm.lookup.jni_name.uncached`
- `typemap.llvm.peer.create`
- `typemap.llvm.proxy.create`
- `typemap.llvm.activation`
- `typemap.lookup.jni_name`
- `typemap.lookup.jni_name.uncached`
- `typemap.lookup.managed_type`
- `typemap.lookup.managed_type.uncached`
- `typemap.lookup.java_object`
- `typemap.lookup.java_interfaces`
- `typemap.lookup.java_interfaces.uncached`
- `typemap.type_manager.get_simple_reference`
- `typemap.type_manager.get_simple_reference.uncached`
- `typemap.type_manager.get_invoker_type`
- `typemap.peer.create`
- `typemap.on_register_natives`

Each span also has a `duration.us` attribute for microsecond precision, because the Aspire CLI and dashboard duration fields are rounded to milliseconds.

## Prerequisites

- Build the local Android SDK first:

  ```bash
  make all
  ```

- Connect exactly one Android device, or set `ANDROID_SERIAL`.
- Trust Aspire HTTPS dev certificates if needed:

  ```bash
  dotnet dev-certs https --trust
  aspire certs trust
  ```

## Run

Run from this directory:

```bash
export PATH="$HOME/.dotnet/tools:$PATH"
ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL=http://localhost:4318 \
aspire run --detach --non-interactive
```

By default the AppHost launches Release/CoreCLR/trimmable. Override the type map to compare against LLVM-IR:

```bash
ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL=http://localhost:4318 \
HELLOWORLD_ANDROID_TYPEMAP=llvm-ir \
aspire run --detach --non-interactive
```

The `helloworld-android` resource builds, installs, and starts the sample app. It also:

- runs `adb reverse tcp:4318 tcp:<dashboard-otlp-http-port>`;
- uninstalls any existing `com.xamarin.android.helloworld` app to avoid signature mismatch errors;
- discovers the dashboard-generated OTLP API key and passes it to the Activity as `OTEL_EXPORTER_OTLP_HEADERS`;
- keeps the Aspire resource alive while the Android app process is running.

Restart just the Android app resource after code changes:

```bash
aspire resource helloworld-android restart
```

## Inspect telemetry

The dashboard URL is printed by `aspire run`. You can also use the CLI:

```bash
aspire describe
aspire otel traces --format Json -n 100
aspire otel spans --format Json -n 500
aspire logs helloworld-android --tail 200
```

If the browser dashboard shows `An unhandled error has occurred` and the CLI still returns telemetry, clear site data/cache for the dashboard URL or open it in a fresh private window. One observed stale-browser failure was:

```text
Microsoft.JSInterop.JSException: The value 'Blazor._internal.Virtualize.setAnchorMode' is not a function.
```

## Notes

- `am start -W` only reports integer millisecond `TotalTime` and `WaitTime`.
- Use span attribute `duration.us` for microsecond duration data.
- Startup spans emitted before app-level OpenTelemetry setup are buffered and replayed as spans once the sample configures its provider.
- Do not leave Android diagnostics startup suspend enabled when using this sample normally:

  ```bash
  adb shell setprop debug.mono.profile none
  ```
