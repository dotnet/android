# Debugging JNI Object Reference Crashes

.NET for Android uses [JNI](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
local, global, and weak-global references to connect managed objects to Java
peers. Invalid references can cause either an unhandled managed exception or an
Android Runtime (ART) abort.

Reference diagnostics on CoreCLR and NativeAOT use the
`Microsoft.Android.Runtime` EventSource. The runtime no longer writes
`grefs.txt`/`lrefs.txt` or reference messages to logcat, and the former
`debug.dotnet.log` `gref`/`lref` options no longer enable reference diagnostics.

## Identify the crash

### Unhandled exception

A failure to activate a Java peer can look like:

```text
System.NotSupportedException: Unable to activate instance of type Java.Lang.Runnable
from native handle 0x7ff1f3b468 (key_handle 0x466b26f).
 ---> System.MissingMethodException: No constructor found for Java.Lang.Runnable::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)
```

Record both the JNI handle and `key_handle` (the Java identity hash). A
`MissingMethodException` for this constructor can result from a disposed or
collected managed peer being used again from Java. Other inner exceptions can
have unrelated causes and should be investigated separately.

For example, deliberately disposing a peer while keeping another global
reference to its Java object destroys the managed association:

```csharp
var runnable = new Java.Lang.Runnable (() => { });
var reference = runnable.PeerReference.NewGlobalRef ();
runnable.Dispose ();
// Invoking Java code through reference may attempt to reactivate the disposed peer.
```

### Android Runtime abort

An ART abort terminates the application without a managed exception:

```text
JNI DETECTED ERROR IN APPLICATION: JNI ERROR (app bug): jobject is an invalid local reference: 0x75 (deleted reference at index 7 in a table of size 7)
```

Record the handle and its reference kind. An invalid *local* reference usually
requires local-reference events, not just global-reference events.

<a name="collect-logs"></a>

## Collect reference events

Build the application with diagnostics and EventSource support enabled:

```xml
<PropertyGroup>
  <EnableDiagnostics>true</EnableDiagnostics>
  <EventSourceSupport>true</EventSourceSupport>
  <DiagnosticSuspend>true</DiagnosticSuspend>
</PropertyGroup>
```

Optimized applications disable EventSource support by default. Enabling it is a
build-time decision; setting an Android system property cannot restore code
that was trimmed away. `DiagnosticSuspend=true` lets collection start before
the application creates the references being investigated.

Start collection before launching the application. For an emulator:

```sh
# Global and weak-global references
dotnet-trace collect --dsrouter android-emu \
    --providers Microsoft.Android.Runtime:0x10:5 -o references.nettrace
```

Use `0x30` instead of `0x10` to include local references. Local-reference events
are very numerous, so enable them only when necessary. For a physical device,
follow the transport setup in the [tracing guide](../tracing.md).

NativeAOT EventPipe does not currently supply reference-event call stacks.
Use `0x50` for global/weak-global operations with opt-in managed stack events,
or `0x70` to include locals and stacks. This adds stack-string allocation cost;
it is not enabled by ordinary reference collection. Keep `StackTraceSupport`
enabled in the NativeAOT application.

Launch the application, reproduce the problem, and stop collection. Preserve
the `.nettrace` file; Speedscope and the `dotnet-trace` Chromium conversion omit
the reference-event payloads.

Inspect the `Microsoft.Android.Runtime` events, their payloads, and available
stacks with [PerfView](https://github.com/microsoft/perfview) on Windows or a
cross-platform analyzer using `Microsoft.Diagnostics.Tracing.TraceEvent`.
A repository skill for interpreting these events is planned in
[#13000](https://github.com/dotnet/android/issues/13000); it is not yet available.

EventPipe can drop events when its buffers overflow. Increase `--buffersize` or
reduce enabled keywords when necessary, and check the analyzer's event-loss
diagnostics. References created before collection and
events still buffered at an abrupt process exit may also be missing. Do not
interpret the absence of an event as proof that the operation did not occur.

## Understand the events

| Event | Meaning |
|---|---|
| `GlobalReferenceCreated` | A new global `handle` was created from `sourceHandle`. |
| `GlobalReferenceDeleted` | The global reference in `sourceHandle` was deleted. |
| `WeakGlobalReferenceCreated` | A new weak-global `handle` was created from `sourceHandle`. |
| `WeakGlobalReferenceDeleted` | The weak-global reference in `sourceHandle` was deleted. |
| `WeakGlobalReferenceCollected` | Java GC collected a weak reference that could not be promoted to a global reference. |
| `LocalReferenceCreated` | A local reference was created or adopted from JNI. |
| `LocalReferenceDeleted` | JNI deleted the local `handle`. |
| `LocalReferenceReleased` | Managed ownership of the local `handle` was transferred; JNI did not delete it. |
| `GlobalReferenceDiagnostic` | Peer creation, disposal, finalization, identity, type, or activation diagnostics. |
| `LocalReferenceDiagnostic` | Additional local-reference diagnostics from Java.Interop. |
| `ReferenceStackTrace` | Opt-in NativeAOT managed stack for the preceding reference operation, correlated by event ID, handle, and managed thread ID. |

Handles are unsigned 64-bit payloads and are printed in hexadecimal.
`sourceType`/`referenceType` values are `0` (invalid), `1` (local), `2` (global),
and `3` (weak-global). A global deletion has a zero destination `handle`.

`globalCount` and `weakCount` are concurrent snapshots of managed global and
weak-global reference accounting. `localCount` belongs to the supplied JNI
environment's managed accounting; it is not a process-wide count of JNI locals.
Event order and counts across threads must not be treated as one atomic history.

`managedThreadId` identifies the managed thread; EventPipe's event-header thread
ID is the native thread ID. `bridgeOperation` identifies
ordinary operations (`0`), bridge initialization (`1`), global-to-weak
transitions (`2`), and weak-to-global transitions (`3`).

For the full event IDs, keywords, and payload contract, see
[JNI reference events](../tracing.md#jni-reference-events).

## Interpret the history

Search backward for the handle from the crash. If you exported the events to
searchable text, for example:

```sh
grep -n '0x75' references.txt
```

For an invalid local reference, find its latest `LocalReferenceCreated` and
`LocalReferenceDeleted` events on the relevant thread. Inspect the deletion's
stack to find the code that disposed the reference. A later use of that handle
is invalid. In contrast, `LocalReferenceReleased` can be a legitimate transfer
back to Java at a JNI boundary.

For an activation failure, search for both the JNI handle and the Java identity
hash in `GlobalReferenceDiagnostic` messages. Peer creation messages connect
the handle to its managed object and Java/managed types. Disposal and
finalization messages show when the managed association was removed. Follow
`GlobalReferenceCreated` events to any copied handles, and use their call stacks
to locate the code retaining the Java object after managed disposal.

During bridge processing, follow global-to-weak and weak-to-global events.
`WeakGlobalReferenceCollected` marks a peer that Java GC did not retain;
successful promotion produces a new `GlobalReferenceCreated` event instead.
The explicit bridge-operation payload replaces the former synthetic
`[[clr-gc:...]]` stack-trace markers.

JNI can reuse handle values. Correlate the handle with its thread, peer identity,
creation/deletion sequence, and bridge context rather than assuming the same
numeric handle always identifies the same lifetime.
