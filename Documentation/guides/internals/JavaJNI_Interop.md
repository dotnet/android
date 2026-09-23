# Java and managed interoperability with the trimmable TypeMap

This is an internal guide to the interop path used by **CoreCLR and NativeAOT Android applications**. The Java VM (ART) and the managed runtime are separate environments in one process. JNI is the boundary in both directions: bound C# types call Java methods through Java.Interop, and Java calls managed methods through generated Java callable wrappers (JCWs) with registered JNI entrypoints.

`AndroidTypeMapImplementation=trimmable` is the only supported application TypeMap implementation. Earlier versions of this guide described a choice between reflection/delegate-based "dynamic" registration and native LLVM-generated marshal methods. Neither describes this path. Here, the build generates **managed** TypeMap assemblies (including proxy and marshal code) and Java sources; the managed runtime registers their native callbacks with ART. There is no application-level native TypeMap lookup table, generated `Java_...` symbol for each callback, or marshal-method callback rewriting.

| Earlier guide | Trimmable TypeMap path |
| --- | --- |
| JCW static initializer built `__md_methods` and called `Runtime.register` with an assembly-qualified type name. | A generated JCW calls `Runtime.registerNatives(Class)`; the generated managed proxy supplies the registrations for that Java class. |
| "Dynamic" registration used connector delegates and reflection/`Reflection.Emit` at runtime. | The scanner reads connector metadata to locate existing callbacks, but generated `UnmanagedCallersOnly` forwarders register their function pointers without making per-method delegates. |
| "Marshal methods" relied on JNI `Java_...` native exports, native LLVM code, and a callback assembly rewriter. | The TypeMap emitter writes managed proxy/entrypoint IL; `RegisterNatives` binds method name/signature pairs to those entrypoints. |
| JNI symbol mangling selected the native implementation; `_mm_wrapper` adapted booleans and exceptions. | The generated registration table uses NUL-terminated UTF-8 names/signatures; generated managed entrypoints perform JNI ABI conversion and exception handoff. |

## Terms and a running example

| Term | Meaning |
| --- | --- |
| Bound managed type (MCW) | A C# binding for an existing Java class, such as `Android.App.Activity`. Its `JniPeerMembers` invokes Java methods. |
| Java callable wrapper (JCW, or ACW) | A *generated Java class* for a managed subclass or Java-visible implementation. Java overrides call `native n_*` methods, which lead back to managed code. |
| TypeMap assembly | A *generated managed DLL* containing Java-name/managed-type associations, proxy types, and (for JCWs) registration and JNI entrypoint code. |
| Java peer / managed peer | Two objects representing one interop instance, with separate runtimes and garbage collectors. JNI references and peer bookkeeping link their lifetimes. |

For example, an application can subclass a bound `Activity`:

```csharp
using Android.App;
using Android.OS;

namespace MyApp
{
	[Activity (Name = "my.app.MainActivity", MainLauncher = true)]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			// Application work here.
		}
	}
}
```

The binding for `Activity.OnCreate` carries the Java name `onCreate` and JNI signature `(Landroid/os/Bundle;)V`. The app override need not repeat `[Register]`: the scanner follows the registered base method through the override. The [scanner test fixtures](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/TestFixtures/TestTypes.cs) include both an explicitly registered `MainActivity` and an unannotated `UserActivity` override.

The corresponding JCW is roughly the following Java. This excerpt omits constructor and peer-reference code; its class name, registration call, override, and native callback name match the [JCW generator tests](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Generator/JcwJavaSourceGeneratorTests.cs):

```java
package my.app;

public class MainActivity extends android.app.Activity
        implements mono.android.IGCUserPeer
{
    static {
        mono.android.Runtime.registerNatives (MainActivity.class);
    }

    @Override
    public void onCreate (android.os.Bundle p0)
    {
        n_OnCreate_Landroid_os_Bundle_ (p0);
    }

    public native void n_OnCreate_Landroid_os_Bundle_ (android.os.Bundle p0);
}
```

The Java name is `my/app/MainActivity`, not the assembly-qualified C# type name. The method descriptor means one `android.os.Bundle` argument and a `void` return; the `L...;` denotes an object reference. The generated callback name is derived from binding metadata. `OnCreate` can call `base.OnCreate` back into Java: that is a **new managed-to-Java invocation**, not a second JCW registration.

## Build-time representation

[`GenerateTrimmableTypeMap`](../../../src/Microsoft.Android.Build.Tasks/Tasks/GenerateTrimmableTypeMap.cs) invokes the [`JavaPeerScanner`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Scanner/JavaPeerScanner.cs) on the application, references, and SDK/framework inputs. The scanner reads managed assembly metadata, including Java type names, `[Register]` methods, overrides of registered base/interface methods, constructors, `[Export]` members, and manifest components. The [`TrimmableTypeMapGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/TrimmableTypeMapGenerator.cs) validates the resulting model and feeds both the Java and managed emitters. The app's generated JCWs do not come from the old `JavaCallableWrapperGenerator` / `MarshalMethodsClassifier` pipeline.

The main outputs are:

| Output | Role |
| --- | --- |
| `typemap/java/**/*.java` | Generated JCWs; [`JcwJavaSourceGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/JcwJavaSourceGenerator.cs) emits class declarations, constructors, overrides, native declarations, registration, and `IGCUserPeer` hooks. |
| `typemap/_*.TypeMap.dll` | Per-input-assembly managed mapping attributes, proxy types, and JNI registration methods emitted by [`TypeMapAssemblyEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs). |
| `typemap/_Microsoft.Android.TypeMaps.dll` | Root assembly anchoring the per-assembly TypeMaps; its generated `TypeMapLoader.Initialize()` wires the mapping dictionaries ([`RootTypeMapAssemblyGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/RootTypeMapAssemblyGenerator.cs)). |
| `typemap/typemap-assemblies.txt`, `java-files.txt`, `acw-map.txt` | Lists of actual generated assemblies/Java sources and Java-to-managed class mappings for the build. |
| `android/src/net/dot/android/ApplicationRegistration.java`, merged `AndroidManifest.xml` | Generated application/instrumentation registration and manifest output. The former is built from scan results, **not** copied from a handwritten template. |

Each per-assembly TypeMap contains assembly-level mapping attributes: a key such as `my/app/MainActivity`, a proxy type, and, for a trimmable mapping, the managed target type. The root references the per-assembly maps via `TypeMapAssemblyTargetAttribute<T>`. The emitter writes **IL and metadata**, not `.cs` files; the C# in the sections below is an explanatory translation of emitted code, not a file that a developer should edit. See [`TypeMapAssemblyGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyGenerator.cs), [`TypeMapAssemblyEmitter.EmitTypeMapAttribute`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs), and the [build-pipeline guide](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/README.md).

For the example Activity, think of a mapping like this **conceptual C#**:

```csharp
// Assembly metadata, emitted as IL rather than source:
[assembly: TypeMapAttribute ("my/app/MainActivity",
			     typeof (MainActivityProxy), typeof (MyApp.MainActivity))]

// The emitted ACW proxy derives from JavaPeerProxy<T> and implements
// IAndroidCallableWrapper. Proxy names are chosen by the emitter.
```

There is no source-level `TypeMap` attribute for application code to write. The real emitted `TypeMapAttribute` takes the Java key and proxy reference; its trimmable three-argument form also takes the target reference. An unconditional mapping has just the key and proxy reference, whereas the target reference on a conditional mapping lets the trimming pipeline discard entries for unused managed types. ACW proxies implement `IAndroidCallableWrapper` to register callbacks; other proxies need not. [`JavaPeerProxy`](../../../src/Mono.Android/Java.Interop/JavaPeerProxy.cs) provides peer creation without `Activator.CreateInstance`.

The [shared TypeMap targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.targets) run `_GenerateTrimmableTypeMap` after `CoreCompile`, compile the generated Java, and feed TypeMap DLLs to later steps. They track generated files and remove stale JCWs when the managed model changes. The target's stamp and output lists matter on incremental builds: a stale compiled Java class can otherwise outlive its managed callback.

CoreCLR's [runtime-specific targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.CoreCLR.targets) give the TypeMap DLLs to ILLink as trimmable inputs, with `_Microsoft.Android.TypeMaps` as the entry assembly. For `PublishTrimmed=true`, a **second scan of linked assemblies** regenerates JCWs in `typemap/linked-java/` and records `linked-java-files.txt`; the pre-trim Java set is not necessarily the packaged set. NativeAOT's [targets](../../../src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/Microsoft.Android.Sdk.TypeMap.Trimmable.NativeAOT.targets) feed the generated DLLs to ILC and use its graph plus the ACW map when producing Java shrinker configuration. Both modes need reachable managed mapping/proxy/entrypoint code; they do not resurrect the native LLVM TypeMap.

## Managed code calls Java

Android bindings are managed wrappers for *existing* Java APIs. For example, [`Activity.ReportFullyDrawn()`](../../../src/Mono.Android/Android.App/Activity.cs) uses its bound member descriptor `"reportFullyDrawn.()V"` and calls `_members.InstanceMethods.InvokeVirtualVoidMethod (...)` on `this`. The essential C# shape (simplified) is:

```csharp
const string id = "reportFullyDrawn.()V";
_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, null);
```

Java.Interop resolves the class and method, invokes JNI with the peer's reference, and converts values/references according to the binding. Other bound methods use `JniArgumentValue` arrays and the appropriate virtual, nonvirtual, or static `JniPeerMembers` invocation. The TypeMap is not a per-call method dispatch table: it is involved when Java classes and managed peers must be mapped or created, not in place of this invocation.

When JNI returns an object or Java passes one into managed code, [`TrimmableTypeMapTypeManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapTypeManager.cs) uses the Java name and generated mappings to select a managed target and proxy. [`TrimmableTypeMapValueManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapValueManager.cs) looks for an existing managed peer or constructs one from the JNI handle. In the reverse direction, the type manager can obtain a JNI type signature for a managed peer type. Do not confuse a bound `Activity` MCW with the `MainActivity` JCW that implements Java-to-managed overrides.

## Java code calls managed code

### JCW declarations and per-class registration

The generated Java `onCreate` calls its native `n_OnCreate_Landroid_os_Bundle_`; constructors similarly declare generated `nctor_*` native callbacks, and instantiate/activate the managed peer when appropriate. The class's static block calls `mono.android.Runtime.registerNatives (MainActivity.class)` when the class is first initialized. This is **per Java class**. Application and instrumentation JCWs instead use a guarded `__md_registerNatives()` helper called from their generated wrappers: registration in their static initializer can be too early in startup. The declaration of `registerNatives(Class)` in [`mono.android.Runtime`](../../../src/java-runtime/java/mono/android/Runtime.java) is handwritten Java; individual JCWs and their registration calls are generated.

There is no concatenated `__md_methods` string and no `Runtime.register("managed.Type, Assembly", ...)` registration in this path. The old JNI native-symbol mangling rules (`Java_package_Class_method...`) matter for symbol-lookup-based JNI code, but generated app JCW callbacks here are associated with their Java declarations through [`RegisterNatives`](https://docs.oracle.com/en/java/javase/17/docs/specs/jni/functions.html#registernatives): the Java method **name** and **signature** must match; no `Java_...` export is required for each method.

### Runtime startup and registration handshake

On CoreCLR, the [native host](../../../src/native/clr/host/host.cc) captures the VM at `JNI_OnLoad`, initializes the managed runtime from `Runtime.initInternal`, and enters [`JNIEnvInit.Initialize`](../../../src/Mono.Android/Android.Runtime/JNIEnvInit.cs). That installs `TrimmableTypeMapTypeManager` and `TrimmableTypeMapValueManager` in `AndroidRuntime`, then calls `TrimmableTypeMap.RegisterNativeMethods()`. The old native `Runtime.register` entrypoint remains a compatibility stub, **not** the mechanism that registers every JCW.

NativeAOT has its own entry: [`JavaInteropRuntime.JNI_OnLoad` and `init`](../../../src/Microsoft.Android.Runtime.NativeAOT/Android.Runtime.NativeAOT/JavaInteropRuntime.cs) initialize the native host and a [`JreRuntime`](../../../src/Microsoft.Android.Runtime.NativeAOT/Java.Interop/JreRuntime.cs), then call `JNIEnvInit.InitializeNativeAotRuntime()`. That path also registers the `TrimmableTypeMap` callback. NativeAOT cannot rely on CoreCLR's optional reflection-based peer construction path when a generated mapping is absent. Its generated `ApplicationRegistration.registerApplications()` calls `Runtime.registerNatives` for deferred startup types; the [`NativeAotRuntimeProvider`](../../../src/Xamarin.Android.Build.Tasks/Resources/NativeAotRuntimeProvider.java) invokes it after `JavaInteropRuntime.init(...)`.

The first native binding is for `mono.android.Runtime.registerNatives` itself. [`TrimmableTypeMap.RegisterNativeMethods()`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMap.cs) uses actual C# UTF-8 string literals for its JNI name and signature:

```csharp
fixed (byte* name = "registerNatives"u8,
	     sig = "(Ljava/lang/Class;)V"u8) {
	var method = new JniNativeMethod (name, sig, onRegisterNatives);
	JniEnvironment.Types.RegisterNatives (runtimeClass.PeerReference, [method]);
}
```

When a JCW's static block (or deferred helper) calls this Java native method, `TrimmableTypeMap.OnRegisterNatives` obtains the class's JNI name, finds its mapping/proxy, and calls the proxy's `IAndroidCallableWrapper.RegisterNatives(JniType)`. This registers the class's `n_*` and `nctor_*` methods with ART. The handwritten [`TrimmableTypeMap`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMap.cs) does the lookup and handoff; the *per-class* method table is in a **generated managed TypeMap proxy**. The CoreCLR host does not supply a table of exported native functions for these app methods.

### Generated registration in C# terms, including UTF-8

The [`TypeMapAssemblyEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs) emits sealed `JavaPeerProxy<T>` types for peers that need proxies. A JCW proxy also implements `IAndroidCallableWrapper`. For each native method it emits a `JniNativeMethod` containing a **pointer to a name**, a **pointer to a JNI signature**, and a **callable entrypoint pointer**. Its `RegisterNatives` method stack-allocates the entries and passes a `ReadOnlySpan<JniNativeMethod>` to `JniEnvironment.Types.RegisterNatives`. In **conceptual C#** (field and callback identifiers are illustrative, and the emitter writes IL):

```csharp
public unsafe void RegisterNatives (JniType jniType)
{
	JniNativeMethod* entries = stackalloc JniNativeMethod [1];
	delegate* unmanaged<IntPtr, IntPtr, IntPtr, void> callback = &OnCreateEntry;
	entries [0] = new JniNativeMethod (
		&MethodNameUtf8, &MethodSignatureUtf8, (IntPtr) callback);
	JniEnvironment.Types.RegisterNatives (
		jniType.PeerReference,
		new ReadOnlySpan<JniNativeMethod> (entries, 1));
}
```

The [`PEAssemblyBuilder`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/PEAssemblyBuilder.cs) emits the actual name/signature fields as **NUL-terminated UTF-8 bytes in RVA data**. It can reuse identical signature data. A representative registration pair for the Activity example is `n_OnCreate_Landroid_os_Bundle_` and `(Landroid/os/Bundle;)V`; these identify the *native* Java declaration, not the C# method's `GetOnCreate...Handler` connector. There is no runtime decoding of `__md_methods` or per-callback `Java_...` export lookup. The handwritten `registerNatives` binding above likewise supplies `byte*` name/signature data, via C# `u8` literals.

These name/signature bytes are not Java `String` argument marshalling. JNI's `GetStringUTFChars`/`NewStringUTF` APIs use [modified UTF-8](https://docs.oracle.com/en/java/javase/17/docs/specs/jni/types.html#modified-utf-8-strings); the emitter stores the registration identifiers/descriptors as UTF-8 with explicit NUL terminators (the ASCII names/signatures above have the same bytes in either encoding). For example, the CoreCLR [class-name lookup](../../../src/native/clr/host/host-shared.cc) obtains a Java class name via `GetStringUTFChars`, then converts its dots to slashes for a TypeMap key. Separately, [`JniRemappingLookup`](../../../src/Mono.Android/Microsoft.Android.Runtime/JniRemappingLookup.cs) encodes method names/signatures as UTF-8 with an explicit trailing NUL for native remapping lookup. Neither path is a substitute for marshalling Java `String` values across JNI.

### From JNI ABI to a C# method

JNI passes a `JNIEnv*`, the instance (`jobject`) or class (`jclass`), then the declared arguments. Java object handles appear as pointer-sized parameters; the generated managed entrypoints have JNI-ABI-compatible signatures and are marked `UnmanagedCallersOnly`. For the example callback, the ABI parameters correspond to `IntPtr env`, `IntPtr self`, and `IntPtr savedInstanceState`. This does **not** mean the app override accepts `IntPtr`: its bound callback/proxy converts the `Bundle` reference and dispatches the virtual C# `OnCreate(Bundle?)`.

The scanner reads `[Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]` on the bound method (or its base). The third string is **connector metadata**, not the Java name of the native callback. For a normal registered binding, the scanner uses it to find the callback's declaring type and name (such as `n_OnCreate_Landroid_os_Bundle_`) and, when available, its *real* CLR signature. The [`TypeMapAssemblyEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs) emits an `UnmanagedCallersOnly` forwarder to that existing callback, which then calls the virtual managed method. The old path made a delegate using `GetOnCreate...Handler` and `JNINativeWrapper.CreateDelegate`; the trimmable path derives callback metadata from the connector without creating a delegate per registration.

`[Export]` (and methods classified as directly callable) take a different branch. The [`ModelBuilder`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ModelBuilder.cs) marks them for a generated direct dispatcher. The [`ExportMethodDispatchEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ExportMethodDispatchEmitter.cs) emits the JNI entrypoint and argument conversions, calls the managed target, converts the return value, and copies back applicable array outputs; it does not invoke a connector callback. For example, a Java-visible C# method can be written as:

```csharp
[Java.Interop.Export ("handleClick")]
public bool HandleClick (Android.Views.View view, int action)
{
	return action != 0;
}
```

This is the shape exercised by the [export fixtures](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/TestFixtures/TestTypes.cs). The Java wrapper has a `native` declaration with its calculated JNI signature; the generated managed dispatcher, not the developer's C# method directly, is the unmanaged entrypoint.

JNI signatures are **not** CLR method signatures. JNI `Z` is an eight-bit `jboolean`, `C` a 16-bit `jchar`, `I` a 32-bit `jint`, and `Landroid/os/Bundle;` a Java object reference. The emitted entrypoint uses JNI ABI types (a `byte` for a boolean); conversions such as `jniValue != 0` and `managedResult ? (byte) 1 : (byte) 0` bridge C# `bool`. For normal registered callbacks the generator uses the captured original callback signature where available: older bindings may declare `bool`/`char` while newer ones can use `sbyte`/`ushort`. It must not assume that the callback signature is identical to the Java descriptor; see the scanner's [callback signature handling](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Scanner/JavaPeerScanner.cs) and [test fixtures](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/TestFixtures/TestTypes.cs). No assembly rewriter is needed to insert separate `_mm_wrapper` methods.

Both generated forwarders and direct dispatchers use a marshal-method frame: `BeginMarshalMethod`, a `try` around the managed call, exception handoff to `OnUserUnhandledException`, and `EndMarshalMethod` in `finally`. An arbitrary managed exception is not simply allowed to escape across an unmanaged JNI boundary. Compare [`EmitUcoForwarderBody`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs) with the [`ExportMethodDispatchEmitter`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ExportMethodDispatchEmitter.cs).

The call sequence for a Java `onCreate(Bundle)` is therefore:

1. ART enters the generated `MainActivity.onCreate`.
2. That method calls its registered `n_OnCreate_Landroid_os_Bundle_`.
3. JNI invokes the generated `UnmanagedCallersOnly` entrypoint for the registered name/signature. For a bound override, it forwards to the connector-derived bound callback; for a direct export it marshals and invokes the target itself.
4. Peer lookup/activation and argument conversion supply the managed `MainActivity` and `Bundle`; virtual dispatch runs the app override. A call to `base.OnCreate` then goes **managed-to-Java** through `JniPeerMembers`.

## Peer activation and lifetime

A Java-side constructor and a managed-side constructor do not allocate one shared object. Generated JCW constructors call `super(...)` and, when the actual Java class matches the wrapper, a generated `nctor_*` callback to activate the matching managed peer. This class check matters when a JCW is further subclassed. For some startup types registration is deferred as described above. The generator produces the appropriate constructor shape from its scan; compare [`JcwJavaSourceGenerator`](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/JcwJavaSourceGenerator.cs) and its [constructor tests](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Generator/JcwJavaSourceGeneratorTests.cs).

On the managed side, [`TrimmableTypeMap`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMap.cs) uses generated mapping attributes to resolve a Java class and target type. Its [`JavaPeerProxy`](../../../src/Mono.Android/Java.Interop/JavaPeerProxy.cs) can create the managed object from a handle without late-bound `Activator.CreateInstance`; activation also accounts for an existing replaceable peer. [`TrimmableTypeMapTypeManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapTypeManager.cs) resolves names/types, while [`TrimmableTypeMapValueManager`](../../../src/Mono.Android/Microsoft.Android.Runtime/TrimmableTypeMapValueManager.cs) checks for registered peers and handles construction/ownership.

Every generated JCW implements the Java [`IGCUserPeer`](../../../src/java-runtime/java/mono/android/IGCUserPeer.java) hooks `monodroidAddReference` and `monodroidClearReferences`. Managed [`JavaMarshalRegisteredPeers`](../../../src/Mono.Android/Microsoft.Android.Runtime/JavaMarshalRegisteredPeers.cs) tracks add/peek/remove/finalize operations, and the GC bridge coordinates the two collectors. Local and global JNI references have different lifetimes: a managed wrapper may need a global reference to keep its peer accessible after a JNI call returns. `JniHandleOwnership` records whether an incoming reference is transferred. Calling `Dispose()` can remove the association; a raw `IntPtr` retained afterward is not a safe substitute for a live managed peer. For invalid reference diagnostics, see [Debugging JNI Object Reference Crashes](debug-jni-objrefs.md).

## Diagnosing a broken interop path

Start at the boundary where the failure occurs; these files distinguish build output, registration, and peer activation:

1. **Missing or wrong Java class:** inspect the merged manifest, `typemap/java/` (or CoreCLR's `typemap/linked-java/` after trimming), `java-files.txt` / `linked-java-files.txt`, and `acw-map.txt`. Confirm the expected Java name and base/override signatures. A stale `.class` is not proof that a corresponding TypeMap entry survived.
2. **Unbound native method / `UnsatisfiedLinkError`:** compare the generated JCW's `native` method **name and JNI descriptor** with its generated proxy registration. Check whether `mono.android.Runtime.registerNatives` ran for the class (or its deferred helper did), and inspect `adb logcat` for the registration exception. Do not search for a missing `Java_...` export in this pipeline.
3. **Wrong managed type or activation failure:** check `typemap-assemblies.txt`, the root `_Microsoft.Android.TypeMaps` assembly, `TrimmableTypeMap` lookup, proxy construction, and `JavaMarshalRegisteredPeers`. Verify whether a Java callback came from a managed-created peer or needs activation from a Java-created peer. On NativeAOT a missing generated mapping cannot be rescued by CoreCLR-only reflection.
4. **Release/trim-only failure:** inspect the *linked* managed assemblies and CoreCLR post-trim JCWs, or NativeAOT ILC inputs and shrinker rules. The pre-trim generated Java is not necessarily packaged Java. The [build-pipeline guide](../../../src/Microsoft.Android.Sdk.TrimmableTypeMap/README.md) explains target stamps and stale-file handling.
5. **JNI reference or marshal failure:** use `adb logcat` and [the JNI reference guide](debug-jni-objrefs.md) for ownership and collection problems. For unsupported TypeMap settings see [the build property](../../docs-mobile/building-apps/build-properties.md); duplicate Java-visible constructor signatures produce [XA4259](../../docs-mobile/messages/xa4259.md).

The [TypeMap generator tests](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Generator/TypeMapAssemblyGeneratorTests.cs), [JCW tests](../../../tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Generator/JcwJavaSourceGeneratorTests.cs), and [application build tests](../../../src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/TrimmableTypeMapBuildTests.cs) exercise these paths. Compare their expected names/signatures against the generated DLL and Java files before changing runtime registration.
