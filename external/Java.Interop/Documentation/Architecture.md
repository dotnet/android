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

## Borrowed native memory

Java.Interop uses the updated C# memory-safety rules. Raw UTF-8 names and
signatures are stored as `byte*`, rather than opaque `IntPtr` values. Constructors
and setters that publish borrowed pointers require an unsafe caller context;
their `<safety>` documentation specifies the backing storage and lifetime.
Pointer declarations and address-only getters do not themselves dereference
memory and can remain safe.

The `ReplacementMethodInfo.TargetJni*Utf8` setters and the raw
`JniTypeManager.GetReplacementTypeInfoCore` producer require unsafe. Their
NUL-terminated backing storage must remain readable and unchanged for the
runtime's lifetime and every subsequent cached metadata read. Managed-string
metadata remains safe to construct and use. Equality, hashing, and remapping
diagnostics compare or format addresses without scanning their contents.
In Debug, borrowed `JniMethodInfo` name/signature constructors also require unsafe
because diagnostics read those strings lazily.

`JniNativeMethod` raw construction requires unsafe. Its name/signature storage
must remain valid through registration, and its callback must have the matching
JNI ABI and remain callable and rooted until successful unregistration.
The callback address is stored as `void*` because JNI methods have heterogeneous
signatures; it is not represented by an incorrect common function-pointer type.

Primitive-array acquisitions retain native element storage as `void*`. Managed
`GetElements` calls remain safe, while raw owner constructors and ref-returning
indexers require unsafe. Acquired elements and escaped references must not be
used after release or disposal. Existing `IntPtr` compatibility APIs are retained;
JNI object handles and method/field IDs remain opaque handles, not byte buffers.

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
