### Breaking change: JNI delegate wrapper API removed

.NET for Android 12 removes `Android.Runtime.JNINativeWrapper` and its
`CreateDelegate (Delegate)` method, including its built-in wrappers and runtime
code-generation fallback. This is an intentional source and binary compatibility
break for existing callers; no compatibility stub is provided.

Rebuild older binding libraries with the current .NET for Android binding
generator. Remove direct calls to `JNINativeWrapper.CreateDelegate` from custom
binding code and use supported generated JNI callbacks and Java callable wrappers.
Simply constructing a delegate is not a replacement for the removed wrapper's
exception handling and GC bridge processing.

The shared `_JniMarshal_*` delegate types and supported generated callback
infrastructure remain. This change does not alter their exception handling,
registration, or reference ownership.
