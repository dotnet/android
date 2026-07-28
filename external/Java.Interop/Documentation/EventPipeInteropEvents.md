# Java.Interop EventPipe interop events

The .NET ↔ Java interop layer emits EventPipe events through a single provider:

- **Provider name:** `Java.Interop`

## Event catalog

| Event ID | Event name | Meaning |
|---|---|---|
| 1 | `DotNetWrapperCreated` | A managed wrapper for a Java object was created. |
| 2 | `JavaWrapperCreated` | A Java wrapper for a managed object was created. |
| 3 | `DotNetWrapperReleasedJavaReference` | A managed wrapper released its Java reference. |
| 4 | `JavaWrapperReleasedDotNetReference` | A Java wrapper released its managed reference. |
| 5 | `DotNetObjectOnlyReachableFromJava` | A managed object is only reachable from Java during bridge processing. |
| 6 | `JavaObjectOnlyReachableFromDotNet` | A Java object is only reachable from .NET during bridge processing. |

## Payload schema

All events contain:

- `managedType` (`string`)
- `javaType` (`string`)
- `jniIdentityHashCode` (`int`)
- `managedObjectHashCode` (`int`)
- `runtimeMode` (`string`)

Reachability events (`5`, `6`) additionally contain:

- `componentIndex` (`int`)
- `contextIndex` (`int`)
- `contextPointer` (`long`)

## Collecting events

Use `dotnet-trace` to capture the provider:

```bash
dotnet-trace collect --process-id <pid> --providers Java.Interop:0x3:4
```

`0x3` enables both wrapper lifecycle and reachability keywords, and `4` enables informational-level events.
