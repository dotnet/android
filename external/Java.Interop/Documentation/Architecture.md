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

## Owned JNI implementation memory

Managed alternatives such as `JniType.DefineClass` with a `byte[]` and
`JniEnvironment.Strings.NewString` with a `string` own their pinning. Unsafe
operations remain inside their implementations; local scopes do not transfer
buffer ownership to callers or change JNI reference cleanup.

`JniNativeMethod` preserves the sequential layout of JNI's `JNINativeMethod`.
It stores opaque callback addresses as `IntPtr`. Construction only copies addresses;
its legacy `unsafe` modifier is syntax context, not a pointee-lifetime contract.
Registration requires readable NUL-terminated names/signatures and a
descriptor-compatible callback rooted until successful unregistration
**and completion of all in-flight callbacks**.

The project uses preview syntax for pointer declarations outside unsafe type
contexts and safe explicit-layout scalar fields. It does **not** opt into the
updated C# memory-safety rules. Object-reference control blocks and JavaVM
arguments retain their allocation, encoding, and release behavior. JNI object
handles and method/field IDs remain opaque `IntPtr` values.

## Native caller contracts

JNI declarations distinguish caller-owned raw memory from implementation-owned
buffers. Their `<safety>` documentation describes buffer bounds, pinning, and
lifetimes. Managed alternatives such as `JniType.DefineClass` with a `byte[]` and
`JniEnvironment.Strings.NewString` with a `string` own their pinning and keep
unsafe operations inside their implementations.

Caller annotations and implementation regions are audited independently.
JNI descriptors select caller-unsafe declarations independently of their
documentation. Method-level safety contracts are authored in those descriptors,
not inferred from parameter names; raw forwarders reference a shared JNI contract.
Each audited `unsafe` body has a `// SAFETY:` explanation naming the storage,
extent, ABI, or lifetime it relies on. The raw `JniNativeMethods` boundary
requires a live `JNIEnv*` on its attached thread; that obligation is not
implied merely by using opaque object, field, or method handles.

`JniEnvironment.InstanceMethods` and `StaticMethods` retain their existing
caller-unsafe classification. A nonzero method ID does not verify its
descriptor or make a parameterless invocation safe. Managed wrappers with
known descriptors discharge that obligation in local implementation blocks.

For UTF-8 span lookups, callers must provide readable NUL-terminated backing
storage throughout the call. Complete C# UTF-8 literals supply a trailing NUL
outside the span length; copying or slicing a span does not preserve that
guarantee. These APIs do not add terminators or retain their input spans.

Native registration names and signatures must survive registration. Callbacks
must match their JNI descriptors and remain callable and rooted until successful
unregistration **and completion of all in-flight callbacks**, including after
failed or partial registration. Declaration-level contracts do not validate
callback ABIs or change registration ownership.

An address-only `JniNativeMethod` constructor stores descriptor addresses; it
does not read their contents or invoke a callback. Registration, not address
storage, owns the ABI, provenance, and lifetime obligations described above.

The project uses preview syntax for pointer declarations outside unsafe type
contexts. It does **not** opt into the
updated C# memory-safety rules. Caller-unsafe declaration intent is preparation
for that later opt-in; it is not enforced for pointer-free signatures under
legacy rules. Generated JNI wrappers and T4 inputs keep implementation unsafe
blocks local without changing marshaling, dispatch, or cleanup.

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
