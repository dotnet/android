# Java and managed interoperability (trimmable TypeMap)

This guide describes the **current** .NET for Android application interop
path for CoreCLR and NativeAOT. `AndroidTypeMapImplementation=trimmable` is the
only supported application TypeMap implementation. The Java VM (ART) and the
managed runtime communicate through JNI; the build supplies generated Java
callable wrappers (JCWs) and managed TypeMap assemblies, while handwritten
Java and managed runtime code connects those generated pieces. This is not a
description of the former MonoVM/native LLVM TypeMap or assembly-rewriting
pipeline.

## What is generated

| Piece | Purpose and source |
| --- | --- |
| Bound managed API types | Managed callable wrappers for Android/Java APIs use `JniPeerMembers` to invoke Java methods. These are distinct from the app's Java callable wrappers; see [`Activity.ReportFullyDrawn()`](../../../src/Mono.Android/Android.App/Activity.cs) for an invocation. |
| JCW `.java` files | Java classes for managed Java peers, with constructors and `n_*` native callbacks for Java-to-managed calls. [`JcwJavaSourceGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/JcwJavaSourceGenerator.cs) produces them from the scanned peer model. |
| TypeMap DLLs | `_{AssemblyName}.TypeMap.dll` files hold mappings, proxy types, and native registration methods; `_Microsoft.Android.TypeMaps.dll` is the root that connects them. [`TypeMapAssemblyGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyGenerator.cs), [`TypeMapAssemblyEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs), and [`RootTypeMapAssemblyGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/RootTypeMapAssemblyGenerator.cs) emit these managed assemblies. |
| Registration and manifest | The task generates `ApplicationRegistration.java` from the scanned peers (not from a Java template); it also writes the ACW map (`acw-map.txt`) and merged `AndroidManifest.xml`. |

The [scanner](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Scanner/JavaPeerScanner.cs)
reads assembly metadata to discover Java peers and their Java-visible
constructors and methods. The
[`GenerateTrimmableTypeMap` task](../../../src/Microsoft.Android.Build.Tasks/Tasks/GenerateTrimmableTypeMap.cs)
drives the scan, validates it, emits the managed and Java outputs, and removes
stale JCW sources. The
[shared MSBuild targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.targets)
run `_GenerateTrimmableTypeMap` after `CoreCompile`; their output lives under
`obj/.../typemap/` (not in handwritten source). Inspect
`typemap/typemap-assemblies.txt` and `typemap/java-files.txt` to see what
that build actually generated. The
[TypeMap build-pipeline guide](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/README.md)
documents target ordering, output stamps, and incremental regeneration.

The root TypeMap assembly anchors the per-assembly TypeMaps and initializes
the lookup dictionaries. Mapping attributes in those per-assembly DLLs
associate Java names, managed types, and generated proxy types. ACW proxies
implement `IAndroidCallableWrapper` for native registration; other proxies
do not need that interface. These are **managed metadata**, not a lookup
table in a native binary. The runtime
[`TrimmableTypeMap`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMap.cs)
uses the mappings to resolve Java objects and managed peer types.

## Managed calls Java

A bound C# method uses its `JniPeerMembers` and a JNI member signature to
call Java through Java.Interop. For example,
`Android.App.Activity.ReportFullyDrawn()` invokes
`_members.InstanceMethods.InvokeVirtualVoidMethod(...)` with
`"reportFullyDrawn.()V"` and the current peer. Static calls use the
corresponding `StaticMethods` APIs. Arguments and return values cross JNI
with the appropriate Java object references and value conversions. This
direction does **not** call a JCW's `n_*` callback: the bound managed API
is the caller, and the Java implementation is the callee.

When a JNI object arrives in managed code,
[`TrimmableTypeMapTypeManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapTypeManager.cs)
resolves its Java type to a managed target/proxy using the generated map;
[`TrimmableTypeMapValueManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapValueManager.cs)
reuses or constructs a managed peer for its JNI reference. Conversely, the
type manager resolves managed Java-peer types to JNI type signatures when
needed. The map is about **type and peer resolution**; it does not replace
normal Java.Interop method invocation.

## Java calls managed code

The JCW is a real Java class generated for a managed peer. Its Java-visible
methods forward to `native n_*` methods, and its constructors use generated
`nctor_*` callbacks. The generator normally emits a per-class static
initializer like:

```java
static {
    mono.android.Runtime.registerNatives (MyActivity.class);
}
```

This is a representative *generated* shape, not a source file to edit.
Application and instrumentation wrappers defer registration through a
generated guarded `__md_registerNatives()` method where static initialization
would be too early. The `registerNatives` declaration itself is **handwritten**
in [`mono.android.Runtime`](../../../src/java-runtime/java/mono/android/Runtime.java).

At startup,
[`JNIEnvInit`](../../../src/Mono.Android/Android.Runtime/JNIEnvInit.cs)
installs the trimmable type and value managers and calls
`TrimmableTypeMap.RegisterNativeMethods()`. That binds
`mono/android/Runtime.registerNatives` to a managed callback using JNI
`RegisterNatives`. When a generated JCW requests registration for its
class, `TrimmableTypeMap.OnRegisterNatives` looks up that Java class in the
generated map and asks its generated `IAndroidCallableWrapper` registration
code to bind the class's `n_*` methods. ART can then call those registered
methods, which transition through generated managed JNI-ABI entrypoints to
the managed implementation.

The [generator model](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ModelBuilder.cs)
records method name, JNI signature, and callback target for each native
registration. For exported or directly callable managed methods,
[`ExportMethodDispatchEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ExportMethodDispatchEmitter.cs)
emits an `UnmanagedCallersOnly` entrypoint that converts JNI arguments, calls
the managed method, converts its result, and handles exceptions. Other
callbacks use the generated registration's corresponding marshal target.
Do not confuse these **generated managed** entrypoints with the handwritten
Java declarations or the old native registration table.

## Startup and peer lifetime

CoreCLR's [native host](../../../src/native/clr/host/host.cc) captures the
Java VM at `JNI_OnLoad`, starts the managed runtime from
`Runtime.initInternal`, and enters `JNIEnvInit.Initialize`. The native
`Runtime.register` entrypoint remains a compatibility stub; TypeMap-based
native registration is handled on the managed side.

NativeAOT instead enters
[`JavaInteropRuntime.JNI_OnLoad` / `init`](../../../src/Microsoft.Android.Runtime.NativeAOT/Android.Runtime.NativeAOT/JavaInteropRuntime.cs),
constructs a
[`JreRuntime`](../../../src/Microsoft.Android.Runtime.NativeAOT/Java.Interop/JreRuntime.cs),
and calls `JNIEnvInit.InitializeNativeAotRuntime`. Both runtime paths
register the same `TrimmableTypeMap` JNI callback, but NativeAOT cannot
depend on CoreCLR's reflection-based fallback for missing mappings.
[`JavaPeerProxy`](../../../src/Mono.Android/Java.Interop/JavaPeerProxy.cs)
provides the generated proxy's AOT-safe peer creation hook.

A managed `Java.Lang.Object` and its Java object are peers, not one object
shared by two garbage collectors. Generated JCWs implement the Java-side
[`IGCUserPeer`](../../../src/java-runtime/java/mono/android/IGCUserPeer.java)
reference hooks; the runtime's
[`JavaMarshalRegisteredPeers`](../../../src/Mono.Android/Microsoft.Android.Runtime/JavaMarshalRegisteredPeers.cs)
and `TrimmableTypeMapValueManager` register, find, replace, and finalize
managed peers while respecting JNI reference ownership. Disposing a managed
peer or letting collection retire a pair can remove the association; keeping
a raw JNI handle after that does not keep the old managed instance alive.

## Trimming, AOT, and diagnosing failures

The [CoreCLR targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.CoreCLR.targets)
feed the generated TypeMap assemblies to ILLink as trimmable inputs. With
`PublishTrimmed=true`, they regenerate JCWs into `typemap/linked-java/` from
the **linked** assemblies, so compiled Java matches the surviving managed
callbacks. Check `typemap/linked-java-files.txt` rather than only the
pre-trim `java-files.txt` in such a build. The
[NativeAOT targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.NativeAOT.targets)
feed TypeMaps to ILC and use its output plus the ACW map for Java shrinker
configuration. Both pipelines depend on reachable managed types and their
generated registrations; a method or type missing after trimming is not
fixed by restoring a historical native TypeMap path.

When investigating:

1. Check the build's generated TypeMap DLL list, JCW list, `acw-map.txt`,
   and compiled Java sources (post-trim `linked-java/` if applicable).
   Compare the actual Java class/name/signature with the generated native
   registration and its managed target.
2. Check `adb logcat` for a Java `UnsatisfiedLinkError`, JNI registration
   failure, or managed exception during activation; distinguish a missing
   callback from a missing managed peer mapping. Inspect the runtime's
   `TrimmableTypeMap` and generated proxy paths for the latter.
3. For invalid or prematurely disposed JNI object references, use
   [Debugging JNI Object Reference Crashes](debug-jni-objrefs.md). For build
   errors about unsupported `AndroidTypeMapImplementation` values, see
   [the build property](../../docs-mobile/building-apps/build-properties.md);
   duplicate Java-visible constructor signatures are reported as
   [XA4259](../../docs-mobile/messages/xa4259.md).

The [trimmable TypeMap build tests](../../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/TrimmableTypeMapBuildTests.cs)
cover generation, trimming, NativeAOT and CoreCLR packaging, and incremental
Java output. Start there when changing generated interop behavior.
