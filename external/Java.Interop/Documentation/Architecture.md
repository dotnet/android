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

### Generated managed binding safety

The binding generator keeps managed method, constructor, and property contracts
safe. JNI argument storage, invocation, string conversion, array copy-back, and
reference cleanup live in local `unsafe` implementation regions. SourceWriter's
`HasUnsafeBody` and accessor-body flags are independent of `IsUnsafe`, which
continues to describe a caller obligation. Abstract and interface declarations
are not made caller-unsafe to accommodate their implementations.

These blocks use existing C# syntax so generated bindings remain usable with
legacy compilers. Generator tests retain the legacy Roslyn harness and also use
the build SDK's actual compiler with updated memory-safety rules, including
implementation/reference metadata and negative callsite checks. This preparation
does not enable those rules on Mono.Android or customer projects.

Throwable-derived bindings have a separate deferred-peer constructor taking
`JniObjectReferenceOptions.None`. The protected forwarding chain has no Java
constructor registration and creates no Java peer; the managed binding
constructor still performs the original JNI construction. Existing peer-constructor
partial hooks are preserved in the forwarding path. Raw-handle constructors
retain their caller obligations. For a referenced intermediate base, the generator
checks whether the actual assembly exposes this constructor. Older bases without
it keep legacy zero-handle initialization rather than receiving an unresolvable
constructor call. If an updated base marks its raw constructor requires-unsafe
but lacks the deferred constructor, a safe generated constructor fails the
updated-rules compilation; regenerating the base supplies the safe path. Legacy
reference metadata does not enforce the new raw caller contract.

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
