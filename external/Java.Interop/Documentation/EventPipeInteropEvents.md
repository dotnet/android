# Java.Interop EventPipe interop events

The .NET ↔ Java interop layer emits EventPipe events through two providers:

- `Java.Interop` emits events from the shared Java interop layer.
- `Microsoft.Android.Runtime` emits events from the Android runtime layer.

## Event catalog

| Event ID | Event name | Meaning |
|---|---|---|
| 1 | `ManagedPeerCreated` | A managed peer for a Java peer was created. |
| 2 | `JavaPeerCreated` | A Java peer for a managed peer was created. |
| 3 | `ManagedPeerReleasedJavaPeer` | A managed peer released its Java peer. |
| 4 | `JavaPeerReleasedManagedPeer` | A Java peer released its managed peer. |
| 5 | `ManagedPeerOnlyReachableFromJavaPeer` | A managed peer is only reachable from its Java peer during bridge processing. |
| 6 | `JavaPeerOnlyReachableFromManagedPeer` | A Java peer is only reachable from its managed peer during bridge processing. |

## Payload schema

All events contain:

- `managedType` (`string`)
- `javaType` (`string`)
- `jniIdentityHashCode` (`int`)
- `managedObjectHashCode` (`int`)
- `runtimeFlavor` (`string`): `MonoVM`, `CoreCLR`, `NativeAOT`, or `Unknown`

Reachability events (`5`, `6`) additionally contain:

- `componentIndex` (`int`)
- `contextIndex` (`int`)
- `contextPointer` (`long`)

## Enabling events

Interop events are disabled by default so that unused instrumentation can be removed by trimming. Enable them in the application project:

```xml
<PropertyGroup>
  <_AndroidEnableInteropEventSource>true</_AndroidEnableInteropEventSource>
</PropertyGroup>
```

## Collecting events

Use `dotnet-trace` to capture both providers:

```bash
dotnet-trace collect --process-id <pid> --providers Java.Interop:0x3:4,Microsoft.Android.Runtime:0x3:4
```

`0x3` enables both peer lifecycle and reachability keywords, and `4` enables informational-level events.

These structured events supplement the existing text-based global and local JNI reference logs; they do not replace them.
