### Breaking change: JNI delegate wrapping is no longer supported

.NET for Android 12 retains `Android.Runtime.JNINativeWrapper` and its
`CreateDelegate (Delegate)` method so that existing binding assemblies containing
legacy connector methods can still resolve their references. The method is marked
`[Obsolete (..., error: true)]` and throws `NotSupportedException` if called.
Its built-in wrappers and runtime code-generation fallback are removed.

Trimmable typemaps read connector metadata but do not invoke connector methods.
They register the marshal methods directly, so existing generated connectors
that reference `CreateDelegate` do not need to execute.

New direct calls are compilation errors, and callers that execute the method
must migrate to supported generated JNI callbacks and Java callable wrappers.
Rebuilding bindings with the current .NET for Android binding generator avoids
the obsolete call. Simply constructing a delegate is not a replacement for
the old wrapper's exception handling and GC bridge processing.

The shared `_JniMarshal_*` delegate types and supported generated callback
infrastructure remain. Their exception handling, registration, and reference
ownership are unchanged.
