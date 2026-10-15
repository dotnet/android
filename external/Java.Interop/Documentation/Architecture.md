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

`JniType.RegisterNativeMethods` snapshots and retains delegates from every attempted
registration, including repeated, failed, and partial registrations. The runtime
retains each registration owner until disposal. Successful unregistration releases
the owner's callback roots; failed unregistration preserves them. Callers must
quiesce native invocations and wait for all in-flight callbacks to complete before
unregistering or disposing the owner.

Direct `JniEnvironment.Types.RegisterNatives` calls do not retain callback roots
after returning. Their callers must keep delegates or raw callback storage callable
and rooted for as long as JNI can invoke them, including after failed or partial
registration. Quiesce native invocations before unregistration and release callback
storage only after successful unregistration and completion of all in-flight calls.

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

## Owned JNI implementation memory

Managed alternatives such as `JniType.DefineClass` with a `byte[]` and
`JniEnvironment.Strings.NewString` with a `string` own their pinning. Unsafe
operations remain inside their implementations; local scopes do not transfer
buffer ownership to callers or change JNI reference cleanup.

`JniNativeMethod` preserves the sequential layout of JNI's `JNINativeMethod`.
Its callback is stored as `void*`, since JNI callbacks have heterogeneous
signatures. Name and signature storage must survive registration. The callback
must match its JNI descriptor and remain callable and rooted until successful
unregistration **and completion of all in-flight callbacks**.

The project uses preview syntax for pointer declarations outside unsafe type
contexts and safe explicit-layout scalar fields. It does **not** opt into the
updated C# memory-safety rules. Object-reference control blocks and JavaVM
arguments retain their allocation, encoding, and release behavior. JNI object
handles and method/field IDs remain opaque `IntPtr` values.

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
