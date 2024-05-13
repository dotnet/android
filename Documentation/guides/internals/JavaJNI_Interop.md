<!--toc:start-->
- [Introduction](#introduction)
- [Java <-> Managed interoperability overview](#java-managed-interoperability-overview)
  - [Java Callable Wrappers (JCW)](#java-callable-wrappers-jcw)
- [Registration](#registration)
  - [Dynamic registration](#dynamic-registration)
    - [Dynamic Java Callable Wrappers registration code](#dynamic-java-callable-wrappers-registration-code)
    - [Dynamic Registration call sequence](#dynamic-registration-call-sequence)
  - [Marshal methods](#marshal-methods)
    - [Marshal Methods Java Callable Wrappers registration code](#marshal-methods-java-callable-wrappers-registration-code)
    - [Marshal methods C# source code](#marshal-methods-c-source-code)
    - [JNI requirements](#jni-requirements)
    - [LLVM IR code generation](#llvm-ir-code-generation)
    - [Assembly rewriting](#assembly-rewriting)
      - [Wrappers for methods with non-blittable types](#wrappers-for-methods-with-non-blittable-types)
      - [UnmanagedCallersOnly attribute](#unmanagedcallersonly-attribute)
    - [Marshal Methods Registration call sequence](#marshal-methods-registration-call-sequence)
<!--toc:end-->

# Introduction

At the core of `.NET for Android` is its ability to interoperate with
the Java/Kotlin APIs implemented in the Android system.  To make it
work, it is necessary to "bridge" the two separate worlds of Java VM
(`ART` in the Android OS) and the Managed VM (`MonoVM`).  Application
developers expect to be able to call native Android APIs and receive
calls (or react to events) from the Android side using code written in
one of the .NET managed languages.  To make it work, `.NET for Android`
employs a number of techniques, both at build and at run time, which
are described in the sections below.

This guide is meant to explain the technical implementation in a way
that is sufficient to understand the system without having to read the
actual source code.

# Java <-> Managed interoperability overview

Java VM and Managed VM are two entirely separate entities which
co-exist in the same process/application.  Despite sharing the same
process resources, they don't "naturally" communicate with each other.
There is no direct way to call Java/Kotlin from .NET a'la the
`p/invoke` mechanism which allows calling native code APIs.  Nor there
exists a way for Java/Kotlin code to invoke managed methods.  To make
it possible, `.NET for Android` takes advantage of the Java's [JNI](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
(`Java Native Interface`), a mechanism that allows native code
(.NET managed code being "native" in this context) to register
implementations of Java methods, written outside the Java VM and in
languages other than Java/Kotlin (for instance in `C`, `C++` or
`Rust`).

Such methods need to be appropriately declared in the Java code, for
instance:

```java
class MainActivity
  extends androidx.appcompat.app.AppCompatActivity
{
  public void onCreate (android.os.Bundle p0)
  {
    n_onCreate (p0);
  }

  private native void n_onCreate (android.os.Bundle p0);
}
```

Each native method is declared using the `native` keyword, and
whenever it is invoked from other Java code, the Java VM will use the
JNI to invoke the target method.

Native methods can be registered either dynamically (by calling the
[`RegisterNatives`](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/functions.html#RegisterNatives)
JNI function) or "statically", by providing a native shared library
which exports a symbol with appropriate name which points to the
native function implementing the Java method.

Both ways of registration are described in detail in the following
sections.

## Java Callable Wrappers (JCW)

`.NET for Android` wraps the entire Android API by generating
appropriate C# code which mirrors the Java/Kotlin code (classes,
interfaces, methods, properties etc).  Each generated class that
corresponds to a Java/Kotlin type, is derived from the
`Java.Lang.Object` class (implemented in the `Mono.Android` assembly),
which marks it as a "Java interoperable type", meaning that it can
implement or override virtual Java methods.  To make registration and
invoking of such methods possible, it is necessary to generate a Java
class which mirrors the Managed one and provides an entry point to
the Java <-> Managed transition.  The Java classes are generated
during application (as well as `.NET for Android`) build and we call
them **Java Callable Wrappers** (or **JCW** for short).  For instance,
the following managed class:

```csharp
public class MainActivity : AppCompatActivity
{
  public override Android.Views.View? OnCreateView (Android.Views.View? parent, string name, Android.Content.Context context, Android.Util.IAttributeSet attrs)
  {
    return base.OnCreateView (parent, name, context, attrs);
  }

  protected override void OnCreate (Bundle savedInstanceState)
  {
     base.OnCreate(savedInstanceState);
     DoSomething (savedInstanceState);
  }

  void DoSomething (Bundle bundle)
  {
     // do something with the bundle
  }
}
```

overrides two Java virtual methods found in the `AppCompatActivity`
type: `OnCreateView` and `OnCreate`.  The `DoSomething` method does
not correspond to any method found in the base Java type, and thus it
won't be included in the JCW.

The Java Callable Wrapper generated for the above class would look as
follows (a few generated methods not relevant to the discussion have
been omitted for brevity):

```java
public class MainActivity
        extends androidx.appcompat.app.AppCompatActivity
{
  public android.view.View onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3)
  {
    return n_onCreateView (p0, p1, p2, p3);
  }
  private native android.view.View n_onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3);

  public void onCreate (android.os.Bundle p0)
  {
    n_onCreate (p0);
  }
  private native void n_onCreate (android.os.Bundle p0);
}
```

Understanding the connection between Managed methods and their Java
counterparts is required in order to understand the registration
mechanisms described in sections found later in this document.  The
[Dynamic registration](#dynamic-registration) section will expand on
this example in order to explain the details of how the Managed type
and its methods are registered with the Java VM.

# Registration

Both mechanisms of method registration rely on generation of [Java
Callable Wrappers](#java-callable-wrappers-jcw), with [Dynamic
registration](#dynamic-registration) requiring more code to be
generated so that the registration can be performed at the runtime.

JCW are generated only for types that derive from
the `Java.Lang.Object` type.  Finding such types is the task of the
Java.Interop's [`JavaTypeScanner`](../../external/Java.Interop/src/Java.Interop.Tools.JavaCallableWrappers/Java.Interop.Tools.JavaCallableWrappers/JavaTypeScanner.cs),
which uses `Mono.Cecil` to read all the assemblies referenced by the
application and its libraries.  The returned list of assemblies is
then used by a variety of tasks, JCW being only one
of them.

After all types are found,
[`JavaCallableWrapperGenerator`](../../external/Java.Interop/src/Java.Interop.Tools.JavaCallableWrappers/Java.Interop.Tools.JavaCallableWrappers/JavaCallableWrapperGenerator.cs)
is invoked in order to analyze each method in each type, looking for
those which override a virtual Java method and, thus, need to be
included in the wrapper class code. The generator optionally (if
[marshal methods](#marshal-methods) are enabled) passes each method to
an implementation of the
[`Java.Interop.Tools.JavaCallableWrappers.JavaCallableMethodClassifier`](../../external/Java.Interop/src/Java.Interop.Tools.JavaCallableWrappers/Java.Interop.Tools.JavaCallableWrappers/JavaCallableWrapperGenerator.cs)
abstract class (which is
[`MarshalMethodsClassifier`](../../src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsClassifier.cs)
in our case), to check whether the given method can be registered
statically.

`JavaCallableWrapperGenerator` looks for methods decorated with the
`[Register]` attribute, which most frequently is created by invoking
its constructor with three parameters:

  1. Java method name
  2. JNI method signature
  3. "Connector" method name

The "connector" is a static method which creates a delegate that
subsequently allows calling of the native callback method:

```csharp
public class MainActivity : AppCompatActivity
{
  // Connector backing field
  static Delegate? cb_onCreate_Landroid_os_Bundle_;

  // Connector method
  static Delegate GetOnCreate_Landroid_os_Bundle_Handler ()
  {
    if (cb_onCreate_Landroid_os_Bundle_ == null)
      cb_onCreate_Landroid_os_Bundle_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_OnCreate_Landroid_os_Bundle_);
    return cb_onCreate_Landroid_os_Bundle_;
  }

  // Native callback
  static void n_OnCreate_Landroid_os_Bundle_ (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
  {
    var __this = global::Java.Lang.Object.GetObject<Android.App.Activity> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
    var savedInstanceState = global::Java.Lang.Object.GetObject<Android.OS.Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
    __this.OnCreate (savedInstanceState);
  }

  // Target method
  [Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
  protected virtual unsafe void OnCreate (Android.OS.Bundle? savedInstanceState)
  {
    const string __id = "onCreate.(Landroid/os/Bundle;)V";
    try {
      JniArgumentValue* __args = stackalloc JniArgumentValue [1];
      __args [0] = new JniArgumentValue ((savedInstanceState == null) ? IntPtr.Zero : ((global::Java.Lang.Object) savedInstanceState).Handle);
      _members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
    } finally {
      global::System.GC.KeepAlive (savedInstanceState);
    }
  }
}
```

The above code is actually generated in the `Android.App.Activity`
class while .NET for Android is built, from which our example
`MainActivity` eventually derives.

What happens with the above code depends on the registration mechanism
and is described in the sections below.

## Dynamic registration

This registration mechanism has been used by `.NET for Android` since
the beginning and it will remain in use for the foreseeable future
when the application is built in the `Debug` configuration or when
[Marshal Methods](#marshal-methods) are turned off.

### Dynamic Java Callable Wrappers registration code

Building on the C# example shown in the [Java Callable
Wrappers](#java-callable-wrappers-jcw) section, the following Java
code is generated (only the parts relevant to registration are shown):

```java
public class MainActivity
        extends androidx.appcompat.app.AppCompatActivity
{
/** @hide */
        public static final String __md_methods;
        static {
                __md_methods = 
                        "n_onCreateView:(Landroid/view/View;Ljava/lang/String;Landroid/content/Context;Landroid/util/AttributeSet;)Landroid/view/View;:GetOnCreateView_Landroid_view_View_Ljava_lang_String_Landroid_content_Context_Landroid_util_AttributeSet_Handler\n" +
                        "n_onCreate:(Landroid/os/Bundle;)V:GetOnCreate_Landroid_os_Bundle_Handler\n" +
                        "";
                mono.android.Runtime.register ("HelloAndroid.MainActivity, HelloAndroid", MainActivity.class, __md_methods);
        }

        public android.view.View onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3)
        {
                return n_onCreateView (p0, p1, p2, p3);
        }

        private native android.view.View n_onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3);

        public void onCreate (android.os.Bundle p0)
        {
                n_onCreate (p0);
        }

        private native void n_onCreate (android.os.Bundle p0);
}
```

Code fragment which takes part in registration is the class's static
constructor.  For each method registered for the type (that is,
implemented or overridden in the managed code), the JCW generator
outputs a single string which contains full information about the type
and method to register.  Each such registration string is terminated
with the newline character and the entire sequence ends with an empty
string.  Together, all the lines are concatenated and placed in the
`__md_methods` static variable.  The `mono.android.Runtime.register`
method (see below for more details) is then invoked to register all
the methods.

### Dynamic Registration call sequence

All the "native" methods declared in the generated Java type are
registered when the type is constructed or accessed for the first
time.  This is when the Java VM invokes the type's static constructor,
kicking off a sequence of calls that eventually ends with all the type
methods registered with JNI:

  1. `mono.android.Runtime.register` is itself a native method,
     declared in the
     [`Runtime`](../../src/java-runtime/java/mono/android/Runtime.java)
     class of .NET for Android's Java runtime code, and implemented in
     the native .NET for Android
     [runtime](../../src/monodroid/jni/monodroid-glue.cc) (the
     `MonodroidRuntime::Java_mono_android_Runtime_register` method).
	 Purpose of this method is to prepare a call into the
     .NET for Android managed runtime code, the
     [`Android.Runtime.JNIEnv::RegisterJniNatives`](../../src/Mono.Android/Android.Runtime/JNIEnv.cs)
     method.
  2. `Android.Runtime.JNIEnv::RegisterJniNatives` is passed name of
     the managed type for which to register Java methods and uses .NET
     reflection to load that type, followed by a call to cache the
     type (via `RegisterType` method in the
     [`TypeManager`](../../src/Mono.Android/Java.Interop/TypeManager.cs)
     class) to end with a call to the
     `Android.Runtime.AndroidTypeManager::RegisterNativeMembers`
     method.
  3. `Android.Runtime.AndroidTypeManager::RegisterNativeMembers`
     eventually calls the
     `Java.Interop.JniEnvironment.Types::RegisterNatives` method which
	 first generates a delegate to the native callback method, using
     `System.Reflection.Emit` (via the
     [`Android.Runtime.JNINativeWrapper::CreateDelegate`](../../src/Mono.Android/Android.Runtime/JNINativeWrapper.cs)
     method) and, eventually, invokes Java JNI's `RegisterNatives`
     function, finally registering the native methods for a managed
     type.
  
The `System.Reflection.Emit` sequence mentioned in 3. above is among
the most costly operations, repeated for each registered method.

Some more information about Java type registration can be found
[here](https://github.com/xamarin/xamarin-android/wiki/Blueprint#java-type-registration).

## Marshal methods

The goal of marshal methods is to completely bypass the [dynamic
registration sequence](#dynamic-registration-call-sequence), replacing
it with native code generated and compiled during application build,
thus saving on the startup time of the application.

Marshal methods registration mechanism takes advantage of the JNI
ability to look up implementations of `native` Java methods in actual
native (shared) libraries.  Such symbols must have names that follow a
set of rules, so that JNI is able to properly locate them (details are
explained in the [JNI Requirements](#jni-requirements) section below).

To achieve that, the marshal methods mechanism uses a number of
classes which [generate native](#llvm-ir-code-generation) code and 
[modify assemblies](#assembly-rewriting) that contain the registered
methods.

Current implementation of the marshal methods classifier recognizes
the "standard" method registration pattern, using the example of the
`OnCreate` method shown in [Registration](#registration) above.

The standard pattern consists of:

  * the "connector" method, `GetOnCreate_Landroid_os_Bundle_Handler`
    above
  * the delegate backing field, `cb_onCreate_Landroid_os_Bundle_`
    above
  * the native callback method, `n_OnCreate_Landroid_os_Bundle_` above
  * and the virtual target method which dispatches the call to the
    actual object, `OnCreate` above.

Whenever the classifier's `ShouldBeDynamicallyRegistered` method is
called, it is passed not only the method's declaring type, but also
the `Register` attribute instance which it then uses to check whether
the method being registered conforms to the "standard" registration
pattern shown above.  The connector, native callback methods as well
as the backing field must be private and static in order for the
registered method to be considered as a candidate for static
registration.

Registered methods which don't follow the "standard" pattern will be
registered dynamically.

### Marshal Methods Java Callable Wrappers registration code

Building on the C# example show in the [Java Callable
Wrappers](#java-callable-wrappers-jcw) section, the following Java
code is generated (only the parts relevant to registration are shown):

```java
public class MainActivity
        extends androidx.appcompat.app.AppCompatActivity
{
  public android.view.View onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3)
  {
    return n_onCreateView (p0, p1, p2, p3);
  }

  private native android.view.View n_onCreateView (android.view.View p0, java.lang.String p1, android.content.Context p2, android.util.AttributeSet p3);

  public void onCreate (android.os.Bundle p0)
  {
    n_onCreate (p0);
  }

  private native void n_onCreate (android.os.Bundle p0);
}
```

Note that, compared to the code generated for the [dynamic
registration](#dynamic-java-callable-wrappers-registration-code)
mechanism, there is no static constructor while the rest 
of the code remains exactly the same.

### Marshal methods C# source code

The marshal methods sections below will all refer to this code
fragment:


```csharp
public class MainActivity : AppCompatActivity
{
  // Connector backing field
  static Delegate? cb_onCreate_Landroid_os_Bundle_;

  // Connector method
  static Delegate GetOnCreate_Landroid_os_Bundle_Handler ()
  {
    if (cb_onCreate_Landroid_os_Bundle_ == null)
      cb_onCreate_Landroid_os_Bundle_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_OnCreate_Landroid_os_Bundle_);
    return cb_onCreate_Landroid_os_Bundle_;
  }

  // Native callback
  static void n_OnCreate_Landroid_os_Bundle_ (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
  {
    var __this = global::Java.Lang.Object.GetObject<Android.App.Activity> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
    var savedInstanceState = global::Java.Lang.Object.GetObject<Android.OS.Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
    __this.OnCreate (savedInstanceState);
  }

  // Target method
  [Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
  protected virtual unsafe void OnCreate (Android.OS.Bundle? savedInstanceState)
  {
    const string __id = "onCreate.(Landroid/os/Bundle;)V";
    try {
      JniArgumentValue* __args = stackalloc JniArgumentValue [1];
      __args [0] = new JniArgumentValue ((savedInstanceState == null) ? IntPtr.Zero : ((global::Java.Lang.Object) savedInstanceState).Handle);
      _members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
    } finally {
      global::System.GC.KeepAlive (savedInstanceState);
    }
  }
}
```

### JNI requirements

JNI
[specifies](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/design.html#resolving_native_method_names)
a number of rules which govern how the native symbol name is
constructed, so that a mapping of object-oriented Java code (with its
package names/namespaces, class names and overloadable methods) into the essentially
"flat" procedural "namespace" of the lowest common denominator C code.

The precise rules are outlined in the URL above, and their short
version is as follows:

  * Each symbol starts with the `Java_` prefix
  * Next follows the mangled (see below) **fully qualified class
    name**
  * Next the `_` character serves as a separator before
  * A mangled **method** name, which is optionally followed by
  * Double underscore `__` and the mangled method argument signature

"Mangling" is a way of encoding certain characters that are not
directly representable both in the source code and in the native
symbol name.  The JNI specification allows for direct use of ASCII
letters (capital and lowercase) and digits, while all the other
characters are either represented by placeholders or encoded as 16-bit
hexadecimal Unicode character code (table copied from the JNI
specification for easier reference):

| Escape sequence | Denotes                                  |
|-----------------|------------------------------------------|
| _0XXXX          | a Unicode character XXXX, all lower case |
| _1              | The `_` character                        |
| _2              | The `;` character in signatures          |
| _3              | The `[` character in signatures          |
| _               | The `.` or `/` characters                |

Generation of JNI symbol names is performed by the
[`MarshalMethodsNativeAssemblyGenerator`](../../src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsNativeAssemblyGenerator.cs)
class while generating the native function source code.

JNI supports two forms of the native symbol name, as signalled in the
bullet list above - a short and a long one.  The former is looked up
first by the Java VM, followed by the latter.  The latter needs to be
used only for overloaded methods, which is what our generator does.

### LLVM IR code generation

[`MarshalMethodsNativeAssemblyGenerator`](../../src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsNativeAssemblyGenerator.cs)
uses the LLVM IR generator infrastructure to output both data and
executable code for all the marshal methods wrappers.  It is not
necessary to understand the generated code unless one needs to modify
it, so this document only shows the equivalent C++ code which can
serve as a guide to understanding how the marshal method runtime
invocation works:

```C++
using get_function_pointer_fn = void(*)(uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr);

static get_function_pointer_fn get_function_pointer;

void xamarin_app_init (get_function_pointer_fn fn) noexcept
{
  get_function_pointer = fn;
}

using android_app_activity_on_create_bundle_fn = void (*) (JNIEnv *env, jclass klass, jobject savedInstanceState);
static android_app_activity_on_create_bundle_fn android_app_activity_on_create_bundle = nullptr;

extern "C" JNIEXPORT void
JNICALL Java_helloandroid_MainActivity_n_1onCreate__Landroid_os_Bundle_2 (JNIEnv *env, jclass klass, jobject savedInstanceState) noexcept
{
  if (android_app_activity_on_create_bundle == nullptr) {
    get_function_pointer (
      16, // mono image index
      0,  // class index
      0x0600055B, // method token
      reinterpret_cast<void*&>(android_app_activity_on_create_bundle) // target pointer
    );
  }

  android_app_activity_on_create_bundle (env, klass, savedInstanceState);
}
```

The `xamarin_app_init` function is output only once and is called by
the `.NET for Android` runtime twice during application startup - once
to pass `get_function_pointer_fn` which does **not** use any locks (as
we know that until a certain point during startup we are in a single
lock, so no data access races can happen) and the other time just
before handing control over to the MonoVM, to pass pointer to
`get_function_pointer_fn` which **does** employ locking (since during
runtime it may very well happen that our generated Java native
functions will be called from different threads simultaneously).

The `Java_helloandroid_MainActivity_n_1onCreate__Landroid_os_Bundle_2`
function is a template which is repeated for each Java native
function, with each function having its own set of arguments and its
own callback backing field (`android_app_activity_on_create_bundle`
here).

The `get_function_pointer` function takes as parameters indexes into a
couple of tables, one for `MonoImage*` pointers and the other for
`MonoClass*` pointers - both of which are generated by the
`MarshalMethodsNativeAssemblyGenerator` class at application build
time and allow for very fast lookup during run time.  Target methods
are retrieved by their token value, within the specified `MonoImage*`
(essentially a pointer to managed assembly image in memory) and class.

The method identified in such manner, **must** be decorated in the
managed code with the
[`[UnmanagedCallersOnly]`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute?view=net-6.0)
attribute (see [below](#unmanagedcallersonly-attribute) for more
details) so that it can be invoked directly, as if it was a native
method itself, with minimal managed marshaling overhead.

### Assembly rewriting

Please refer to the [C# code fragment](#marshal-methods-c-source-code)
above in order to understand this section.

Managed assemblies (including `Mono.Android.dll`) which contain Java
types need to be usable in two contexts: with the "traditional"
dynamic registration and with marshal methods.  Both of these
mechanisms, however, have different requirements.  We cannot assume
that any assembly (either from .NET for Android or a third party nuget)
will have "marshal methods friendly" code and thus we need to make
sure that the code meets our requirements.

We do it by reading each relevant assembly and modifying it by
altering the definition of the native callbacks and removing the
code that's no longer used by marshal methods.  This task is performed
by the
[`MarshalMethodsAssemblyRewriter`](../../src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsAssemblyRewriter.cs)
invoked during application build after all the assemblies are linked
but **before** type maps are generated (as rewriting **will** alter
the method and potentially type tokens)

The exact modifications we apply are:

  * Removal of the **connector backing field**
  * Removal of the **connector method**
  * Generation of a **native callback wrapper** method, which catches
    and propagates unhandled exceptions thrown by the native callback
    or the target method.  This method is decorated with the
    `[UnmanagedCallersOnly]` attribute and called directly from the
    native code.
  * Optionally, generate code in the **native callback wrapper** to handle
    [non-blittable types](#wrappers-for-methods-with-non-blittable-types).

All the modifications are performed with `Mono.Cecil`.

After modifications, the assembly contains equivalent of the following
C# code for each marshal method:

```csharp
public class MainActivity : AppCompatActivity
{
  // Native callback
  static void n_OnCreate_Landroid_os_Bundle_ (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
  {
    var __this = global::Java.Lang.Object.GetObject<Android.App.Activity> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
    var savedInstanceState = global::Java.Lang.Object.GetObject<Android.OS.Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
    __this.OnCreate (savedInstanceState);
  }

  // Native callback exception wrapper
  [UnmanagedCallersOnly]
  static void n_OnCreate_Landroid_os_Bundle__mm_wrapper (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
  {
    try {
      n_OnCreate_Landroid_os_Bundle_ (jnienv, native__this, native_savedInstanceState)
    } catch (Exception ex) {
      Android.Runtime.AndroidEnvironmentInternal.UnhandledException (ex);
    }
  }

  // Target method
  [Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
  protected virtual unsafe void OnCreate (Android.OS.Bundle? savedInstanceState)
  {
    const string __id = "onCreate.(Landroid/os/Bundle;)V";
    try {
      JniArgumentValue* __args = stackalloc JniArgumentValue [1];
      __args [0] = new JniArgumentValue ((savedInstanceState == null) ? IntPtr.Zero : ((global::Java.Lang.Object) savedInstanceState).Handle);
      _members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
    } finally {
      global::System.GC.KeepAlive (savedInstanceState);
    }
  }
}
```

#### Wrappers for methods with non-blittable types

The
[`[UnmanagedCallersOnly]`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute?view=net-6.0)
attribute requires that all the argument types as well as the method
return type are
[blittable](https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types).

Among these types is one that's commonly used by the managed classes
implementing Java methods: `bool`.  Currently this is the **only**
non-blittable type we've encountered in bindings, so at this point it
is the only one supported by the assembly rewriter.

Whenever we encounter a method with a non-blittable type, we must
generate a wrapper for it, so that we can decorate it with the
`[UnmanagedCallersOnly]` attribute.  This is easier and less error
prone than modifying the native callback method's IL stream to
implement the necessary conversion.

An example of such method is
`Android.Views.View.IOnTouchListener::OnTouch`:

```csharp
static bool n_OnTouch_Landroid_view_View_Landroid_view_MotionEvent_ (IntPtr jnienv, IntPtr native__this, IntPtr native_v, IntPtr native_e)
{
  var __this = global::Java.Lang.Object.GetObject<Android.Views.View.IOnTouchListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
  var v = global::Java.Lang.Object.GetObject<Android.Views.View> (native_v, JniHandleOwnership.DoNotTransfer);
  var e = global::Java.Lang.Object.GetObject<Android.Views.MotionEvent> (native_e, JniHandleOwnership.DoNotTransfer);
  bool __ret = __this.OnTouch (v, e);
  return __ret;
}
```

As it returns a `bool` value, it needs a wrapper to cast the return
value properly. Each wrapper method retains the native callback method
name, but appends the `_mm_wrapper` suffix to it:

```csharp
static bool n_OnTouch_Landroid_view_View_Landroid_view_MotionEvent_ (IntPtr jnienv, IntPtr native__this, IntPtr native_v, IntPtr native_e)
{
  var __this = global::Java.Lang.Object.GetObject<Android.Views.View.IOnTouchListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
  var v = global::Java.Lang.Object.GetObject<Android.Views.View> (native_v, JniHandleOwnership.DoNotTransfer);
  var e = global::Java.Lang.Object.GetObject<Android.Views.MotionEvent> (native_e, JniHandleOwnership.DoNotTransfer);
  bool __ret = __this.OnTouch (v, e);
  return __ret;
}

[UnmanagedCallersOnly]
static byte n_OnTouch_Landroid_view_View_Landroid_view_MotionEvent__mm_wrapper (IntPtr jnienv, IntPtr native__this, IntPtr native_v, IntPtr native_e)
{
  try {
    return n_OnTouch_Landroid_view_View_Landroid_view_MotionEvent_(jnienv, native__this, native_v, native_e) ? 1 : 0;
  } catch (Exception ex) {
    Android.Runtime.AndroidEnvironmentInternal.UnhandledException (ex);
    return default;
  }
}
```

The wrapper's return statement uses the ternary operator to "cast" the
boolean value to `1` (for `true`) or `0` (for `false`) because the
value of `bool` across the managed runtime can take a range of values:

  * `0` for `false`
  * `-1` or `1` for `true`
  * `!= 0` for true

Since the `bool` type in C# can be 1, 2 or 4 bytes long, we need to
cast it to some type of a known and static size.  The managed type
`byte` was chosen as it corresponds to the Java/JNI `jboolean` type,
defined as an unsigned 8-bit type.

Whenever an **argument** value needs to be converted between `byte` and
`bool`, we generate code that is equivalent of the `argument != 0`
comparison, for instance for the
`Android.Views.View.IOnFocusChangeListener::OnFocusChange` method:

```csharp
[UnmanagedCallersOnly]
static void n_OnFocusChange_Landroid_view_View_Z (IntPtr jnienv, IntPtr native__this, IntPtr native_v, byte hasFocus)
{
  n_OnFocusChange_Landroid_view_View_Z (jnienv, native__this, native_v, hasFocus != 0);
}

static void n_OnFocusChange_Landroid_view_View_Z (IntPtr jnienv, IntPtr native__this, IntPtr native_v, bool hasFocus)
{
  var __this = global::Java.Lang.Object.GetObject<Android.Views.View.IOnFocusChangeListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
  var v = global::Java.Lang.Object.GetObject<Android.Views.View> (native_v, JniHandleOwnership.DoNotTransfer);
  __this.OnFocusChange (v, hasFocus);
}
```

#### UnmanagedCallersOnly attribute

Each marshal methods native callback method is decorated with the
[`[UnmanagedCallersOnly]`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute?view=net-6.0)
attribute, in order for us to be able to invoke the callback directly
from native code with minimal overhead compared to traditional
managed-from-native method calls (`mono_runtime_invoke`)

### Marshal Methods Registration call sequence

The sequence described in the [dynamic
registration sequence](#dynamic-registration-call-sequence) section
above is completely removed for the marshal methods.  What remains
common for both dynamic and marshal methods registration, is the
resolution of the native function target done by the Java VM runtime.
In both cases the method declared in a Java class as `native` is
looked up by the Java VM when first JIT-ing the code.  The difference
lies in the way this lookup is performed.

Dynamic registration uses the
[`RegisterNatives`](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/functions.html#RegisterNatives)
JNI function at the runtime, which stores a pointer to the registered
method inside the structure which describes a Java class in the Java
VM.

Marshal methods, however, don't register anything with the JNI,
instead they rely on the symbol lookup mechanism of the Java VM.
Whenever a call to `native` Java method is JIT-ed and it is not
registered previously using the `RegisterNatives` JNI function, Java
VM will proceed to look for symbols in the process runtime image (e.g.
using `dlopen` + `dlsym` calls on Unix) and, having found a matching
symbol, use pointer to it as the target of the `native` Java method
call.
