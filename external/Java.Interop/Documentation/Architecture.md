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

## Compiler-enforced caller contracts

Java.Interop opts into the preview compiler's `updated-memory-safety-rules`
feature. Its implementation and reference modules carry
`MemorySafetyRulesAttribute (2)`, and caller-unsafe members carry
`RequiresUnsafeAttribute`. Updated-rule consumers must acknowledge those
contracts at their call sites. Legacy consumers retain the existing compiler
behavior; the metadata does not validate native addresses or callback ABIs.

A declaration's `unsafe` modifier describes a caller obligation, not an unsafe
implementation context. Managed alternatives keep pinning, pointer access, and
native invocation inside explicit local `unsafe` blocks without transferring
those obligations to their callers. Mono.Android and customer-project opt-in
remain separate changes.

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

Typed pointer getters remain safe: reading an address does not dereference its
storage. Their setters and the typed raw hooks require unsafe callers, who must
establish the documented storage lifetime. The warning-obsolete native-int
compatibility APIs remain safe-callable under both compiler modes.

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

The updated rules permit pointer declarations outside unsafe type contexts and
safe explicit-layout scalar fields. Object-reference control blocks and JavaVM
arguments retain their allocation, encoding, and release behavior. JNI object
handles and method/field IDs remain opaque `IntPtr` values.

## Native caller contracts

JNI declarations distinguish caller-owned raw memory from implementation-owned
buffers. Their `<safety>` documentation describes buffer bounds, pinning, and
lifetimes. Managed alternatives such as `JniType.DefineClass` with a `byte[]` and
`JniEnvironment.Strings.NewString` with a `string` own their pinning and keep
unsafe operations inside their implementations.

For UTF-8 span lookups, callers must provide readable NUL-terminated backing
storage throughout the call. Complete C# UTF-8 literals supply a trailing NUL
outside the span length; copying or slicing a span does not preserve that
guarantee. These APIs do not add terminators or retain their input spans.

Native registration names and signatures must survive registration. Callbacks
must match their JNI descriptors and remain callable and rooted until successful
unregistration **and completion of all in-flight callbacks**, including after
failed or partial registration. Declaration-level contracts do not validate
callback ABIs or change registration ownership.

Caller-unsafe declarations are enforced for updated-rule consumers, including
pointer-free signatures whose JNI descriptors carry native ABI obligations.
Generated JNI wrappers and T4 inputs keep implementation unsafe blocks local
without changing marshaling, dispatch, or cleanup.

## Generated managed binding safety

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
