# Architecture

## .NET for Android runtime

Java.Interop provides the JNI bridge between managed code and Android's Java VM.
Managed bindings use `JniPeerMembers` to resolve and invoke Java methods and fields.
Java-to-managed calls use generated Java callable wrappers and registered JNI
callbacks. CoreCLR and NativeAOT applications use generated trimmable typemaps for
peer lookup, constructor activation, and callback registration.

See [Java and managed interoperability with the trimmable TypeMap](../../../Documentation/guides/internals/JavaJNI_Interop.md)
for the build pipeline, generated callbacks, runtime startup, and activation flow.

## Values and reference ownership

`JniObjectReference` is an `IntPtr`-backed struct representing a JNI local, global,
or weak global reference. Its `JniObjectReferenceType` identifies the reference
kind. Ownership transfer is explicit; owned references must be released with
`JniObjectReference.Dispose (ref reference)`.

`JniMethodInfo` and `JniFieldInfo` represent method and field IDs, which do not
require reference disposal. `JniPeerMembers` caches these IDs for subsequent
invocations.

`JniValueMarshaler` and `JniValueMarshalerState` provide non-expression value
conversion and argument-state creation/cleanup. Primitive, string, and array
conversion remain supported. `JavaObject` and `JavaException` maintain managed
peers and their JNI reference ownership.

## Native callback ownership

`JniType.RegisterNativeMethods` takes ownership of the supplied registration array
and retains it and its delegates until successful unregistration, including after
failed or partial registration. Callers must not modify the array or its entries
through any alias until successful unregistration and completion of all callbacks.
Another registration attempt throws `InvalidOperationException` until successful
unregistration without taking ownership of the rejected array. The runtime retains
each registration owner until disposal. Successful unregistration releases the
owner's callback roots; failed unregistration preserves them. Callers must prevent
new calls to the registered native methods and wait for callbacks already running
to finish before unregistering or disposing the owner.

Direct `JniEnvironment.Types.RegisterNatives` calls do not retain callback roots
after returning. Their callers must keep delegates or raw callback storage callable
and rooted for as long as JNI can invoke them, including after failed or partial
registration. Prevent new calls to the registered native methods before
unregistration. Release callback storage only after unregistration succeeds and
all callbacks already running have finished.

`JniNativeMethod` name/signature storage must remain readable and NUL-terminated
through registration. Its raw callback must match the JNI descriptor's ABI,
including the environment and receiver/class arguments.

## Naming conventions

Types such as `JavaObject` and `JavaArray<T>` provide managed wrappers for Java
objects. Types such as `JniObjectReference` and `JniArgumentValue` represent the
lower-level JNI references and argument values used by those wrappers.

## .NET 12 API retirement

.NET 12 removes the obsolete runtime expression-marshaling builder, context,
hooks, and the unused `JniValueManager.ActivatePeer` contract. These are not part
of generated callbacks or trimmable typemap activation. See the
[breaking-change and migration notes](../../../Documentation/release-notes/expression-marshalling-retirement.md)
for the removed APIs and migration guidance.
