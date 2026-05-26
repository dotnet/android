# Trimmable typemap OTEL data

Captured from the Aspire dashboard telemetry API with `aspire describe --format Json`, `aspire otel traces --format Json`, `aspire otel spans --format Json`, and `aspire otel logs helloworld-android --format Json`.

## Capture context

- Resource: `helloworld-android` / `helloworld-android-sekjyauw`
- Type: `Executable`
- State: `Running`
- Health: `Healthy`
- Dashboard resource URL: https://localhost:17193/?resource=helloworld-android-sekjyauw
- OTLP endpoint in resource environment: `http://localhost:4318`
- Runtime: `CoreCLR`
- Type map: `trimmable`
- Configuration: `Release`

## Dataset summary

- Traces returned: **8**
- Spans returned: **31**
- Structured logs returned: **0**
- Span duration range: **0-1 ms**
- Span sources: `helloworld-android` (31)

> Note: the Aspire CLI exposes traces, spans, and structured logs. Metrics were exported by the app, but no `aspire otel metrics` command is available in the current CLI, so this file records the available OTEL trace/span/log data.

## Operation counts

| Operation | Span count | Duration ms | Notable attributes |
| --- | ---: | --- | --- |
| `typemap.activation.should_skip` | 1 | 0-0 | reason=existing-peer |
| `typemap.buffered_events` | 1 | 0-0 | event.count=102 |
| `typemap.lookup.java_object` | 1 | 1-1 | target.type=Android.Views.View |
| `typemap.lookup.jni_name` | 2 | 0-1 | jni.name=android/widget/Button; jni.name=mono/android/view/View_OnClickListenerImplementor |
| `typemap.lookup.jni_name.uncached` | 2 | 0-1 | jni.name=android/widget/Button; jni.name=mono/android/view/View_OnClickListenerImplementor |
| `typemap.lookup.managed_type` | 4 | 0-1 | managed.type=Java.Lang.Object; managed.type=Android.Views.View+IOnClickListenerImplementor; managed.type=Android.Views.View; managed.type=Android.Widget.Button |
| `typemap.lookup.managed_type.uncached` | 4 | 0-1 | managed.type=Java.Lang.Object; managed.type=Android.Views.View+IOnClickListenerImplementor; managed.type=Android.Views.View; managed.type=Android.Widget.Button |
| `typemap.on_register_natives` | 1 | 1-1 | jni.name=mono/android/view/View_OnClickListenerImplementor; proxy.count=1 |
| `typemap.peer.create` | 1 | 1-1 | target.type=Android.Views.View; proxy.target.type=Android.Widget.Button; created=true |
| `typemap.type_manager.get_simple_reference` | 4 | 0-1 | managed.type=Java.Lang.Object; managed.type=Android.Views.View+IOnClickListenerImplementor; managed.type=Android.Views.View; managed.type=Android.Widget.Button |
| `typemap.type_manager.get_simple_reference.uncached` | 4 | 0-1 | managed.type=Java.Lang.Object; managed.type=Android.Views.View+IOnClickListenerImplementor; managed.type=Android.Views.View; managed.type=Android.Widget.Button |
| `typemap.universe.get_proxy_types` | 2 | 0-0 | jni.name=android/widget/Button; jni.name=mono/android/view/View_OnClickListenerImplementor |
| `typemap.universe.try_get_proxy_type` | 4 | 0-1 | managed.type=Java.Lang.Object; managed.type=Android.Views.View+IOnClickListenerImplementor; managed.type=Android.Views.View; managed.type=Android.Widget.Button |

## Traces

| Trace | Title | Duration ms | Span count | Status |
| --- | --- | ---: | ---: | --- |
| [75d71f2](https://localhost:17193/traces/detail/75d71f22a3ceab38f9728c03c55ba270) | `typemap.buffered_events` | 0 | 1 | ok |
| [bded42c](https://localhost:17193/traces/detail/bded42cf4e95abec392a14caaacd7398) | `typemap.peer.create` | 1 | 5 | ok |
| [4fdffdb](https://localhost:17193/traces/detail/4fdffdbf23c3cc6d28ffed79a456dd67) | `typemap.type_manager.get_simple_reference` | 0 | 5 | ok |
| [a168e27](https://localhost:17193/traces/detail/a168e2707a279e8d6bf68da7f4f0bb64) | `typemap.type_manager.get_simple_reference` | 1 | 5 | ok |
| [09c3f3a](https://localhost:17193/traces/detail/09c3f3a8a236dc5d76006ff7d5f555cd) | `typemap.on_register_natives` | 1 | 4 | ok |
| [ae0c3c2](https://localhost:17193/traces/detail/ae0c3c26dd40e6a48c9effb852a78a25) | `typemap.activation.should_skip` | 0 | 1 | ok |
| [621e741](https://localhost:17193/traces/detail/621e74123f37e044bf481dc3085558cf) | `typemap.type_manager.get_simple_reference` | 0 | 5 | ok |
| [c7dacaa](https://localhost:17193/traces/detail/c7dacaa8b3ec898b5aac5ee3baac83c5) | `typemap.type_manager.get_simple_reference` | 0 | 5 | ok |

## Representative span trees

### `typemap.buffered_events` (`75d71f2`)

Dashboard: https://localhost:17193/traces/detail/75d71f22a3ceab38f9728c03c55ba270

- `typemap.buffered_events` span `059abc7` duration `0 ms` (event.count=102)

### `typemap.peer.create` (`bded42c`)

Dashboard: https://localhost:17193/traces/detail/bded42cf4e95abec392a14caaacd7398

- `typemap.peer.create` span `a5748d8` duration `1 ms` (target.type=Android.Views.View, resolved.target.type=Android.Views.View, proxy.target.type=Android.Widget.Button, created=true)
  - `typemap.lookup.java_object` span `1143e29` duration `1 ms` (target.type=Android.Views.View)
  - `typemap.lookup.jni_name` span `1717b09` duration `1 ms` (jni.name=android/widget/Button)
  - `typemap.lookup.jni_name.uncached` span `4bc9bcb` duration `1 ms` (jni.name=android/widget/Button)
  - `typemap.universe.get_proxy_types` span `c0a7632` duration `0 ms` (jni.name=android/widget/Button)

### `typemap.type_manager.get_simple_reference` (`4fdffdb`)

Dashboard: https://localhost:17193/traces/detail/4fdffdbf23c3cc6d28ffed79a456dd67

- `typemap.type_manager.get_simple_reference` span `5296e93` duration `0 ms` (managed.type=Java.Lang.Object)
  - `typemap.type_manager.get_simple_reference.uncached` span `10f6da6` duration `0 ms` (managed.type=Java.Lang.Object)
  - `typemap.lookup.managed_type` span `f2042a9` duration `0 ms` (managed.type=Java.Lang.Object)
  - `typemap.lookup.managed_type.uncached` span `5f89088` duration `0 ms` (managed.type=Java.Lang.Object)
  - `typemap.universe.try_get_proxy_type` span `441d427` duration `0 ms` (managed.type=Java.Lang.Object)

### `typemap.type_manager.get_simple_reference` (`a168e27`)

Dashboard: https://localhost:17193/traces/detail/a168e2707a279e8d6bf68da7f4f0bb64

- `typemap.type_manager.get_simple_reference` span `f084d36` duration `1 ms` (managed.type=Android.Views.View+IOnClickListenerImplementor)
  - `typemap.type_manager.get_simple_reference.uncached` span `149e0a2` duration `1 ms` (managed.type=Android.Views.View+IOnClickListenerImplementor)
  - `typemap.lookup.managed_type` span `ab0764b` duration `1 ms` (managed.type=Android.Views.View+IOnClickListenerImplementor)
  - `typemap.lookup.managed_type.uncached` span `7e47917` duration `1 ms` (managed.type=Android.Views.View+IOnClickListenerImplementor)
  - `typemap.universe.try_get_proxy_type` span `a500d0b` duration `1 ms` (managed.type=Android.Views.View+IOnClickListenerImplementor)

### `typemap.on_register_natives` (`09c3f3a`)

Dashboard: https://localhost:17193/traces/detail/09c3f3a8a236dc5d76006ff7d5f555cd

- `typemap.on_register_natives` span `c7a582a` duration `1 ms` (jni.name=mono/android/view/View_OnClickListenerImplementor, proxy.count=1)
  - `typemap.lookup.jni_name` span `35b71d2` duration `0 ms` (jni.name=mono/android/view/View_OnClickListenerImplementor)
  - `typemap.lookup.jni_name.uncached` span `288bd41` duration `0 ms` (jni.name=mono/android/view/View_OnClickListenerImplementor)
  - `typemap.universe.get_proxy_types` span `5df335d` duration `0 ms` (jni.name=mono/android/view/View_OnClickListenerImplementor)

### `typemap.activation.should_skip` (`ae0c3c2`)

Dashboard: https://localhost:17193/traces/detail/ae0c3c26dd40e6a48c9effb852a78a25

- `typemap.activation.should_skip` span `ddd11bb` duration `0 ms` (skip=true, reason=existing-peer)

### `typemap.type_manager.get_simple_reference` (`621e741`)

Dashboard: https://localhost:17193/traces/detail/621e74123f37e044bf481dc3085558cf

- `typemap.type_manager.get_simple_reference` span `fc174ee` duration `0 ms` (managed.type=Android.Views.View)
  - `typemap.type_manager.get_simple_reference.uncached` span `8157b18` duration `0 ms` (managed.type=Android.Views.View)
  - `typemap.lookup.managed_type` span `91fa5f5` duration `0 ms` (managed.type=Android.Views.View)
  - `typemap.lookup.managed_type.uncached` span `63d6a98` duration `0 ms` (managed.type=Android.Views.View)
  - `typemap.universe.try_get_proxy_type` span `c9e575c` duration `0 ms` (managed.type=Android.Views.View)

### `typemap.type_manager.get_simple_reference` (`c7dacaa`)

Dashboard: https://localhost:17193/traces/detail/c7dacaa8b3ec898b5aac5ee3baac83c5

- `typemap.type_manager.get_simple_reference` span `821c5a0` duration `0 ms` (managed.type=Android.Widget.Button)
  - `typemap.type_manager.get_simple_reference.uncached` span `592e591` duration `0 ms` (managed.type=Android.Widget.Button)
  - `typemap.lookup.managed_type` span `fc4706b` duration `0 ms` (managed.type=Android.Widget.Button)
  - `typemap.lookup.managed_type.uncached` span `547a87a` duration `0 ms` (managed.type=Android.Widget.Button)
  - `typemap.universe.try_get_proxy_type` span `cb68edc` duration `0 ms` (managed.type=Android.Widget.Button)

## Raw-data files used

- Session JSON: `/Users/simonrozsival/.copilot/session-state/334c4935-91fa-4eb9-9b1f-644e1dc6f038/otel/`
- Aspire export bundle captured earlier: `/Users/simonrozsival/Projects/dotnet/android-typemap-otel/samples/TypemapOtelAspire/aspire-export-20260526-111653.zip`

## 10x launch log collection

Collected 10 Release/CoreCLR/trimmable launches of `com.xamarin.android.helloworld/example.MainActivity` after clearing `debug.mono.profile` to `none`.

Raw artifacts:

- Directory: `/Users/simonrozsival/Projects/dotnet/android-typemap-otel/artifacts/trimmable-typemap-otel-runs/20260526-115846`
- Per run: `run-N/am-start.txt`, `run-N/host-timing.txt`, `run-N/pid.txt`, `run-N/logcat-all.txt`, `run-N/logcat-filtered.txt`, `run-N/aspire-traces.json`, `run-N/aspire-spans.json`
- Aggregate files: `summary.csv`, `stats.csv`, `final-aspire-traces.json`, `final-aspire-spans.json`

### Precision notes

- Android `am start -W` reports `TotalTime` and `WaitTime` in integer milliseconds.
- Host-side wall-clock launch timing is stored in microseconds as `host_elapsed_us`.
- Runtime OTEL spans now include a `duration.us` attribute for microsecond precision. This is necessary because Aspire's top-level `durationMs` fields are integer millisecond values.
- Early startup operations that happen before app-level OTEL setup are replayed as buffered spans with original start/end timestamps, so `jnienv.initialize` and `typemap.data.initialize` are visible as timed spans.

### 10x summary

| Run | Total ms | Wait ms | Host elapsed us | PID | Trace count | Span count | `jnienv.initialize` us | `typemap.data.initialize` us |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 557 | 564 | 648734 | 8740 | 134 | 226 | 122651.1 | 82455.6 |
| 2 | 550 | 556 | 652420 | 8836 | 193 | 308 | 99664.9 | 61964.5 |
| 3 | 542 | 545 | 695342 | 8935 | 193 | 308 | 99664.9 | 61964.5 |
| 4 | 539 | 542 | 746665 | 9025 | 200 | 390 | 96694.2 | 60933 |
| 5 | 542 | 545 | 649834 | 9114 | 200 | 472 | 99517.6 | 62330.6 |
| 6 | 528 | 531 | 624504 | 9204 | 200 | 554 | 102958.8 | 62576.6 |
| 7 | 551 | 557 | 670907 | 9293 | 200 | 636 | 98244.1 | 61591.9 |
| 8 | 530 | 538 | 639367 | 9385 | 200 | 718 | 101117 | 63097.5 |
| 9 | 547 | 554 | 678690 | 9501 | 200 | 800 | 99380.3 | 62241.9 |
| 10 | 549 | 556 | 670814 | 9589 | 200 | 882 | 97700.3 | 61176.7 |

### 10x stats

| Metric | Count | Min | Median | Max | Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| `TotalTime` ms | 10 | 528.0 | 544.5 | 557.0 | 543.5 |
| `WaitTime` ms | 10 | 531.0 | 549.5 | 564.0 | 548.8 |
| Host elapsed us | 10 | 624504.0 | 661617.0 | 746665.0 | 667727.7 |
| `jnienv.initialize` us | 10 | 96694.2 | 99591.25 | 122651.1 | 101759.32 |
| `typemap.data.initialize` us | 10 | 60933.0 | 62103.2 | 82455.6 | 64033.28 |

## LLVM-IR comparison smoke data

After adding LLVM-IR lookup instrumentation, the same Aspire sample was relaunched with:

```bash
HELLOWORLD_ANDROID_TYPEMAP=llvm-ir
```

The resource environment confirmed `HELLOWORLD_ANDROID_TYPEMAP=llvm-ir`, and `aspire otel spans --format Json` returned the following comparison spans:

| Span | Duration ms | `duration.us` | Notes |
| --- | ---: | ---: | --- |
| `jnienv.initialize` | 47 | 47354.8 | Buffered startup span |
| `typemap.llvm.activation` | 11 | 10888.9 | Buffered activation path |
| `typemap.llvm.lookup.jni_name` | 1 | 1493.2 | Buffered JNI-name lookup |
| `typemap.llvm.lookup.jni_name.uncached` | 1 | 1487.4 | Buffered native typemap lookup |
| `typemap.llvm.lookup.jni_name` | 0 | 235.3 | Steady-state lookup for `android/widget/Button`, `cache.hit=false` |
| `typemap.llvm.lookup.jni_name.uncached` | 0 | 128.6 | Steady-state CoreCLR lookup for `android/widget/Button` |
| `typemap.llvm.activation` | 0 | 23 | Steady-state activation path |

This gives direct side-by-side OTEL coverage for LLVM-IR typemap lookups and the trimmable typemap lookup paths in the same Aspire workflow.

## Detailed LLVM-IR 10x launch log collection

After adding nested LLVM-IR activation and peer-creation spans, collected 10 Release/CoreCLR/LLVM-IR launches of `com.xamarin.android.helloworld/example.MainActivity`.

Raw artifacts:

- Directory: `/Users/simonrozsival/Projects/dotnet/android-typemap-otel/artifacts/llvm-typemap-otel-runs/20260526-130206`
- Per run: `run-N/am-start.txt`, `run-N/host-timing.txt`, `run-N/pid.txt`, `run-N/logcat-all.txt`, `run-N/logcat-filtered.txt`, `run-N/aspire-traces.json`, `run-N/aspire-spans.json`
- Aggregate files: `summary.csv`, `stats.csv`, `final-aspire-traces.json`, `final-aspire-spans.json`

The detailed LLVM instrumentation now includes spans for activation sub-steps such as `peek_object`, `resolve_type`, `get_parameter_types`, `get_object_array`, `constructor_lookup`, `activate_uninitialized`, `set_peer_reference`, and `invoke_constructor`, plus peer-creation internals such as class lookup, superclass walk, type signature lookup, and assignability checks.

### LLVM-IR 10x summary

| Run | Total ms | Wait ms | Host elapsed us | PID | Trace count | Span count | LLVM span count | `jnienv.initialize` us | `typemap.llvm.activation` us | `typemap.llvm.lookup_jni_name.uncached` us |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 484 | 486 | 757226 | 13942 | 22 | 42 | 36 | 37423.2 | 1096.3 | 111.1 |
| 2 | 492 | 494 | 607194 | 14044 | 37 | 42 | 36 | 37423.2 | 1096.3 | 111.1 |
| 3 | 483 | 489 | 635163 | 14130 | 67 | 76 | 66 | 40533.8 | 1000.2 | 84 |
| 4 | 485 | 487 | 594269 | 14222 | 67 | 93 | 81 | 39809.8 | 1040.6 | 82.1 |
| 5 | 470 | 480 | 604356 | 14313 | 97 | 110 | 96 | 38548.6 | 900.6 | 78.7 |
| 6 | 478 | 484 | 577463 | 14399 | 112 | 127 | 111 | 39392.3 | 912.5 | 83 |
| 7 | 478 | 482 | 747416 | 14487 | 112 | 144 | 126 | 38329.9 | 986.5 | 93.3 |
| 8 | 478 | 483 | 586199 | 14575 | 127 | 144 | 126 | 38329.9 | 986.5 | 93.3 |
| 9 | 471 | 473 | 593723 | 14662 | 127 | 161 | 141 | 40153.6 | 963.9 | 127.9 |
| 10 | 483 | 486 | 653005 | 14749 | 142 | 161 | 141 | 40153.6 | 963.9 | 127.9 |

### LLVM-IR 10x stats

| Metric | Count | Min | Median | Max | Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| `TotalTime` ms | 10 | 470.0 | 480.5 | 492.0 | 480.2 |
| `WaitTime` ms | 10 | 473.0 | 485.0 | 494.0 | 484.4 |
| Host elapsed us | 10 | 577463.0 | 605775.0 | 757226.0 | 635601.4 |
| LLVM span count | 10 | 36.0 | 103.5 | 141.0 | 96.0 |
| `jnienv.initialize` us | 10 | 37423.2 | 38970.45 | 40533.8 | 39009.79 |
| `typemap.llvm.activation` us | 10 | 900.6 | 986.5 | 1096.3 | 994.73 |
| `typemap.llvm.lookup_jni_name.uncached` us | 10 | 78.7 | 93.3 | 127.9 | 99.24 |
