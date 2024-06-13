# Hello From JNI

[JNI][0] supports *two* modes of operation:

 1. Native code creates the JVM, e.g. via [`JNI_CreateJavaVM()`][1], or
 2. The JVM already exists, and calls [`JNI_OnLoad()`][2] when loading a native library.

Java.Interop samples and unit tests rely on the first approach.

.NET Android / neé Xamarin.Android is the second approach.

Bring an example of the latter into a Java.Interop sample, using [NativeAOT][3].

## Building

Building a native library with NativeAOT requires a Release configuration build.
For in-repo use, that means that dotnet/java-interop itself needs to be built in
Release configuration:

```sh
% dotnet build -c Release -t:Prepare
% dotnet build -c Release
```

Once Java.Interop itself is built, you can *publish* the sample:

```sh
% cd samples/Hello-NativeAOTFromJNI
% dotnet publish -c Release -r osx-x64
```

The resulting native library contains the desired symbols:

```sh
% nm bin/Release/osx-x64/publish/Hello-NativeAOTFromJNI.dylib | grep ' S ' 
00000000000ef880 S _JNI_OnLoad
00000000000ef8b0 S _JNI_OnUnload
00000000000ef5d0 S _Java_net_dot_jni_hello_App_sayHello
00000000000ef900 S _Java_net_dot_jni_hello_JavaInteropRuntime_init
```

Use the `RunJavaSample` target to run Java, which will run
`System.loadLibrary("Hello-NativeAOTFromJNI")`, which will cause the
NativeAOT-generated `libHello-NativeAOTFromJNI.dylib` to be run:

```sh
% dotnet build -c Release -r osx-x64 -t:RunJavaSample  -v m --nologo --no-restore
  Hello from Java!
  C# init()
  Hello from .NET NativeAOT!
  String returned to Java: Hello from .NET NativeAOT!
  # jonp: called `Example.ManagedType/__<$>_jni_marshal_methods.__RegisterNativeMembers()` w/ 1 methods to register.
  mt.getString()=Hello from C#, via Java.Interop! Value=42

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.04

% (cd bin/Release/osx-x64/publish ; java -cp hello-from-java.jar:java-interop.jar net/dot/jni/hello/App)
Hello from Java!
C# init()
Hello from .NET NativeAOT!
String returned to Java: Hello from .NET NativeAOT!
# jonp: called `Example.ManagedType/__<$>_jni_marshal_methods.__RegisterNativeMembers()` w/ 1 methods to register.
mt.getString()=Hello from C#, via Java.Interop! Value=42
```

Note the use of `(cd …; java …)` so that `libHello-NativeAOTFromJNI.dylib` is
in the current working directory, so that it can be found.

# Notes

To support cross-compilation, the project should set
`$(PlatformTarget)`=AnyCPU.

# Known Knowns?

With this sample "done" (-ish), there are some
"potentially solved, if not ideally" used to make NativeAOT + Java *viable*.

## `Type.GetType()`

Commit 
[dotnet/java-interop@005c9141](https://github.com/dotnet/java-interop/commit/005c914170a0af9069ff18fd4dd9d45463dd5dc6)
uses JNI Type Signatures to avoid `Type.GetType()` invocations, which continue
to be used in .NET Android.

```Java
/* partial */ class JavaCallableWrapper
{
  public static final String __md_methods;
  static {
    __md_methods =
      "n_GetString:()Ljava/lang/String;:__export__\n" +
      "";
    net.dot.jni.ManagedPeer.registerNativeMembers (
        /* nativeClass */             ManagedType.class,
        /* methods */                 __md_methods);
  }

  public ManagedType (int p0)
  {
    super ();
    if (getClass () == ManagedType.class) {
      net.dot.jni.ManagedPeer.construct (
          /* self */                  this,
          /* constructorSignature */  "(I)V",
          /* arguments */             new java.lang.Object[] { p0 });
    }
  }
}
```

This requires the use of JNI method signatures within the constructor
to lookup the corresponding managed constructor to invoke.  While this
works, it requires additional work to lookup the constructor, as there
may not be a 1:1 relation between types within the JNI method signature
and managed code.  In particular, Java *arrays* may have multiple types
which can be used from managed code.


# Known Unknowns

With this sample "done" (-ish), there are several "future research directions" to
make NativeAOT + Java *viable*.

## GC

Firstly, there's the open GC question: NativeAOT doesn't provide a "GC Bridge"
like MonoVM does, so how do we support cross-VM object references?

  * [Collecting Cyclic Garbage across Foreign Function Interfaces: Who Takes the Last Piece of Cake?](https://pldi23.sigplan.org/details/pldi-2023-pldi/25/Collecting-Cyclic-Garbage-across-Foreign-Function-Interfaces-Who-Takes-the-Last-Piec)
  * [`JavaScope`?](https://github.com/jonpryor/java.interop/commits/jonp-registration-scope)
    (Less a "solution" and more a "Glorious Workaround".)


## Type Maps

A "derivative" of the `Type.GetType()` problem is that Java.Interop needs a way
to associate a Java type to a .NET `System.Type` instance, for all manner of
reasons.  (One such reason: `JniRuntime.JniValueManager.GetValue()` needs to
know the associated type so that it can create a "peer wrapper", if needed.)

Java.Interop unit tests "hack" around this by using a dictionary in TestJVM,
and `Hello-NativeAOTFromJNI` follows suite.  This isn't a "real" answer, though.

.NET Android has a very complicated typemap mechanism that involves a table
between the Java JNI name and an { assembly name, type token } pair, along with
copious use of MonoVM embedding API such as `mono_class_get()`.  ***A Lot***
of effort has gone into making type maps performant.

How do we "do" type maps in NativeAOT?  We may need to consider some equivalent
to the iOS "static registrar", and this also needs to support getting `Type`
instances for non-`public` types.  There are also concerns about initialization
overhead; a `Dictionary<string, Type>` will require loading and resolving
*all* the `Type` instances as part of startup, which *can't* be good for
reducing startup time.  What other data structure could be used?

[0]: https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html
[1]: https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/invocation.html#creating_the_vm
[2]: https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/invocation.html#JNJI_OnLoad
[3]: https://github.com/dotnet/samples/blob/main/core/nativeaot/NativeLibrary/README.md