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

Retained names/signatures use `byte*`; JNI handles and method/field IDs remain
opaque `IntPtr` values. `ReplacementMethodInfo.TargetJni*Utf8Pointer` and the
`JniTypeManager.GetReplacementTypeInfoUnsafe` / `GetReplacementMethodInfoUnsafe`
hooks expose borrowed storage. Owners must retain readable bytes through the
NUL terminator, unchanged for the runtime and every cached/diagnostic read, even
after a later lookup fails. Read-only aliases may overlap; no concurrent write
or free is allowed. Java.Interop neither roots nor frees these buffers.

Peer names and Debug `JniMethodInfo` strings are converted lazily under that
lifetime contract. Managed-string alternatives remain safe; non-null native
names take precedence. Complete UTF-8 literals retain their zero-copy path.
Remapping equality, hashing and `ToString` inspect addresses, not their contents.

The .NET 11 RC2 native-int `TargetJni*Utf8` accessors and two native-int
`GetReplacementTypeInfoCore` / `GetReplacementMethodInfoCore` virtual slots stay
safe-callable and warning-obsolete. They are explicit compatibility exceptions,
not pointer validation. Typed base hooks preserve old overrides and base calls;
the distinct type-producer name avoids existing `out var` ambiguity.

Caller obligations and native implementation scopes are independent; managed
wrappers remain safe. This preparation does not enable updated memory-safety
rules. Legacy compilation still requires unsafe contexts for typed address
reads; final activation introduces safe address getters and unsafe setters.

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
