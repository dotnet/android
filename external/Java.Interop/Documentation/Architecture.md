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

`JniType.RegisterNativeMethods` retains the original array and its owner only after
successful registration. Callers must not modify the array or its entries through
any alias while its callbacks can be invoked. Only the latest successfully registered
array is retained, so callers must not start another JNI registration on the same
instance before successful unregistration. This safety requirement is not checked
at runtime. Failed or partial JNI registration does not retain the supplied array;
the caller must keep its delegates rooted until successful unregistration and
completion of all callbacks. Registration errors propagate as exceptions without
attempting to recover from or roll back partial native registration.

Disposal unregisters callbacks before removing the runtime's owner root or releasing
the JNI reference. Failed unregistration preserves those roots and the reference.
Prevent new calls and wait for callbacks already running to finish before
unregistering or disposing the owner.

Direct `JniEnvironment.Types.RegisterNatives` calls do not retain callback roots
after returning. Their callers must keep delegates or raw callback storage callable
and rooted for as long as JNI can invoke them, including after failed or partial
registration. Prevent new calls to the registered native methods before
unregistration. Release callback storage only after unregistration succeeds and
all callbacks already running have finished.

`JniNativeMethod` name/signature storage must remain readable and NUL-terminated
through registration. Its raw callback must match the JNI descriptor's ABI,
including the environment and receiver/class arguments.

## Borrowed UTF-8 metadata

Raw UTF-8 names and signatures are stored as `byte*`, not opaque `IntPtr`
values. The `ReplacementMethodInfo.TargetJni*Utf8Pointer` properties and the
`JniTypeManager.GetReplacementTypeInfoUnsafe` and
`GetReplacementMethodInfoUnsafe` hooks expose these borrowed buffers explicitly.
Their `<safety>` documentation specifies readable NUL-terminated storage and its
lifetime. Retained names must remain alive and unchanged for the runtime's
lifetime and every subsequent cached metadata read. Java.Interop does not own
or free them. Managed-string alternatives remain safe to construct and use;
non-null pointers take precedence over managed replacement names.

Peer members cache the borrowed type name and lazily cache its managed
conversion. In Debug, `JniMethodInfo` also reads borrowed names and signatures
lazily. Its raw constructors require an unsafe context, while its public
diagnostic getters use the lifetime established at construction. Remapping
equality, hashing, and `ToString` compare or format addresses without reading
their contents. JNI object handles and method/field IDs remain opaque handles.

The native-int `TargetJni*Utf8` properties and
`GetReplacementTypeInfoCore`/`GetReplacementMethodInfoCore` virtual hooks shipped
in .NET 11 RC2. They retain all six accessors and both virtual slots with their
original signatures and safe-call behavior as warning-obsolete compatibility
APIs. These are explicit exceptions to caller-unsafe enforcement, not proof
that an address is valid; their backing-memory obligations are unchanged.
Typed base hooks dispatch through the old virtual slots so existing custom
overrides and their base calls continue to run. The typed type-name producer
has a distinct name to avoid ambiguity in existing `out var` calls.

This compatibility preparation does not enable the updated C# memory-safety
rules. Under the existing rules, even reading a typed pointer address requires
an unsafe context; the API baseline records those intermediate compilation
semantics without `RequiresUnsafe` metadata. Global caller enforcement and
address-only safe getters are a separate activation change.

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
