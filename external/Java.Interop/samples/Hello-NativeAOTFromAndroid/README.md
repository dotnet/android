# Hello From Android

[Hello-NativeAOTFromJNI](../Hello-NativeAOTFromJNI) demonstrated how
to use [NativeAOT][0] to create a native library which could be loaded
by a Java Virtual Machine (JVM).

Extend this idea for Android!

## Building

Building a native library with NativeAOT requires a Release configuration build.
For in-repo use, that means that xamarin/Java.Interop itself needs to be built in
Release configuration:

```sh
% dotnet build -c Release -t:Prepare
% dotnet build -c Release
```

Once Java.Interop itself is built, you can *publish* the sample:

```sh
% cd samples/Hello-NativeAOTFromAndroid

# set the ANDROID_NDK_HOME environment variable or set the AndroidNdkDirectory property
# set the ANDROID_HOME environment variable or set the AndroidSdkDirectory property
# values here are valid if you have a xamarin/xamarin-android build environment.
% dotnet publish -c Release -p:AndroidNdkDirectory=$HOME/android-toolchain/ndk \
    -p:AndroidSdkDirectory=$HOME/android-toolchain/sdk
```

The resulting native library contains various import symbols:

```sh
% nm -D bin/Release/linux-bionic-arm64/native/Hello-NativeAOTFromAndroid.so | grep ' T '
0000000000240950 T JNI_OnLoad@@V1.0
0000000000240ab0 T JNI_OnUnload@@V1.0
0000000000240b30 T Java_net_dot_jni_nativeaot_JavaInteropRuntime_init@@V1.0
00000000002392e0 T __start___managedcode
00000000004394d0 T __start___unbox
00000000004394d0 T __stop___managedcode
000000000043a720 T __stop___unbox
```

The build system also produces a `net.dot.jni.helloandroid-Signed.apk`,
which can be installed and launched:

```sh
% adb install bin/Release/linux-bionic-arm64/net.dot.jni.helloandroid-Signed.apk
% adb shell am start net.dot.jni.helloandroid/my.MainActivity

# Only-java codepath for testing; doesn't use NativeAOT:
% adb shell am start net.dot.jni.helloandroid/net.dot.jni.nativeaot.JavaMainActivity
```

## Logging

By default this sample writes quite a bit to `adb logcat`, including:

  * Initialization messages

    ```
    D NativeAotRuntimeProvider: NativeAotRuntimeProvider()
    D NativeAotRuntimeProvider: NativeAotRuntimeProvider.attachInfo(): calling JavaInteropRuntime.init()…
    D JavaInteropRuntime: Loading libHello-NativeAOTFromAndroid.so…
    I JavaInteropRuntime: JNI_OnLoad()
    I NativeAotFromAndroid: C# init()
    D NativeAotRuntimeProvider: NativeAotRuntimeProvider.onCreate()
    ```

  * JNI Global Reference and Local Reference messages

    ```
    D NativeAot:LREF: +l+ lrefc 1 handle 0x7eb64ae01d/L from thread ''(1)
    D NativeAot:GREF: +g+ grefc 1 obj-handle 0x7eb64ae01d/L -> new-handle 0x2af2/G from thread ''(1)
    ```

  * `MainActivity` messages

    ```
    I NativeAotFromAndroid: MainActivity..ctor()
    I NativeAotFromAndroid: MainActivity.OnCreate(): savedInstanceState? False
    ```

Additionally, the end of `MainActivity.OnCreate()` will print out how many
GREFs have been created, and information about the created "surfaced peers":

```
I NativeAotFromAndroid: Created 6 GREFs; Surfaced 1 peers
I NativeAotFromAndroid:   SurfacedPeers[  0] = JniSurfacedPeerInfo(PeerReference=0x2bc6/G IdentityHashCode=0x1d64f40 Instance.Type=Java.Interop.Samples.NativeAotFromAndroid.MainActivity)
```

The (very!) extensive logging around JNI Global and Local references mean that
this sample should *not* be used as-is for startup timing comparison.
That said, on my Pixel 6, we get:

```
I ActivityTaskManager: Displayed net.dot.jni.helloandroid/my.MainActivity for user 0: +282ms
```

## What does this mean for .NET for Android?

Short-term?  Nothing.  Long-term?  *Maybe* something.

While .NET for Android uses Java.Interop, it uses a different *style* of Java.Interop.
.NET for Android *could* be updated to support NativeAot, but it would not be as simple
as this sample may suggest.  Difficulties will include:

  * [GC](#gc)
  * [Marshal Methods](#marshal-methods)
  * [Process Startup miscellany, including the important question "what is an Assembly?"](#miscellany)

### GC

.NET for Android relies on .NET's MonoVM, which provides a
[GC bridge](https://github.com/dotnet/runtime/blob/c5c7f0d3d11cc82eddf1747fbdcaec9cb850c3aa/src/native/public/mono/metadata/details/sgen-bridge-types.h),
which is used to support cross-VM object references.  This allows an object
reference within a Java VM to keep an object instance within the .NET VM alive.

Neither CoreCLR nor NativeAot runtimes support such a GC bridge, and without
something like it, developers would need to take *significantly* more care in
object lifetimes and cleanup.

Until a cross-VM GC solution is found, .NET for Android must remain on MonoVM.

### Marshal Methods

"Marshal Methods" are methods that are:

  * Invoked by the Java Virtual Machine when a `native` Java method is invoked.
  * Responsible for parameter marshaling, invoking C# method overrides, and
marshaling the return type back to Java.

.NET for Android uses `generator --codegen-target=XAJavaInterop1` for binding
assemblies, which "bakes in" marshal methods.  There is an implicit ABI for
marshal methods, and part of that ABI is that they don't catch exceptions:

```csharp
partial class Activity {
    protected virtual unsafe void OnCreate (Android.OS.Bundle? savedInstanceState) => …

    static Delegate? cb_onCreate_Landroid_os_Bundle_;
    static Delegate GetOnCreate_Landroid_os_Bundle_Handler ()
    {
        if (cb_onCreate_Landroid_os_Bundle_ == null)
            cb_onCreate_Landroid_os_Bundle_ = JNINativeWrapper.CreateDelegate (new _JniMarshal_PPL_V (n_OnCreate_Landroid_os_Bundle_));
        return cb_onCreate_Landroid_os_Bundle_;
    }

    static void n_OnCreate_Landroid_os_Bundle_ (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
    {
        // Note: no try/catch block!  If `__this.OnCreate()` throws, Bad Things™ will happen.
        var __this = global::Java.Lang.Object.GetObject<Android.App.Activity> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
        var savedInstanceState = global::Java.Lang.Object.GetObject<Android.OS.Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
        __this.OnCreate (savedInstanceState);
    }
}
```

`Activity.n_OnCreate_Landroid_os_Bundle_()` is the marshal method responsible for
invoking `Activity.OnCreate()`.  It does not catch exceptions, and if an exception
*were* thrown from `Activity.OnCreate()`, the entire app could exit.  Consequently,
every such marshal method is wrapped in `JNINativeWrapper.CreateDelegate()`, which
uses `DynamicMethod` to wrap the marshal method in a `try`/`catch` block, which
is responsible for notifying the debugger and exception marshaling.

As-is, none of this can work with NativeAot.

Updating .NET for Android to *not* use `DynamicMethod` has both known and unknown
issues (what new pattern do we use?  What about compatibility with existing
binding assemblies?).

This sample uses `generator --codegen-target=JavaInterop1` for binding assemblies,
which *skips* the emission of marshal methods *entirely*.  As Marshal Methods are
*required*, `jnimarshalmethod-gen` is invoked as a post-build step to insert
Marshal Methods into the assemblies, and these marshal methods appropriately
marshal exceptions.

## Miscellany

.NET for Android deals with assemblies: they can be side-loaded (for Fast Deployment),
packaged trimmed or untrimmed.  Bidirectional mapping between JNI type names and
`System.Type` instances makes extensive use of MonoVM's embedding API.

None of the above exists in NativeAot: there are no separate assembly files,
"assembly identity" is a nebulous concept, and there is no equivalent to the MonoVm
embedding API.

Large portions of .NET for Android would need to be rewritten to support NativeAot,
and NativeAot would actively prevent features such as Fast Deployment, meaning *both*
MonoVM and NativeAot would need to be supported.

## Notes

As with `Hello-NativeAOTFromJNI`, the project needs to be built with
`$(PlatformTarget)`=AnyCPU, so that `jnimarshalmethod-gen` can be used
to generate JNI Marshal Methods as a post-build step.

This project contains a *tiny* `android.xml` API description for Android.
This is used to generate a binding, allowing (nominally) intuitive:

```csharp
[JniTypeSignature ("my/MainActivity")]
partial class MainActivity : Android.App.Activity {
    protected override void OnCreate (Android.OS.Bundle? savedInstanceState) => …
}
```

This project follows what .NET for Android does to initialize things:
provide a custom [`ContentProvider`][1] which contains Java "bootstrap"
code to initialize the runtime.

### GC

As with [Hello-NativeAOTFromJNI](../Hello-NativeAOTFromJNI), NativeAOT does not
provide a GC bridge that we can rely on.  Consequently, every "surfaced peer" will
*never be collected by default*.

This is a *sample*, not a product, and not even the *inkling* of a product.

For exploratory purposes only.

[0]: https://github.com/dotnet/samples/blob/main/core/nativeaot/NativeLibrary/README.md
[1]: https://developer.android.com/reference/android/content/ContentProvider
