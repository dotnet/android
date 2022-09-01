<!--toc:start-->
- [Introduction](#introduction)
- [Java <-> Managed interoperability overview](#java-managed-interoperability-overview)
 - [Java Callable Wrappers](#java-callable-wrappers)
 - [Managed Callable Wrappers](#managed-callable-wrappers)
- [Dynamic registration](#dynamic-registration)
 - [Java Callable Wrappers registration code](#java-callable-wrappers-registration-code)
 - [Registration call sequence](#registration-call-sequence)
- [Marshal methods](#marshal-methods)
 - [JNI requirements](#jni-requirements)
 - [LLVM IR code generation](#llvm-ir-code-generation)
 - [Assembly rewriting](#assembly-rewriting)
  - [Wrappers for methods with non-blittable types](#wrappers-for-methods-with-non-blittable-types)
  - [UnmanagedCallersOnly attribute](#unmanagedcallersonly-attribute)
<!--toc:end-->

# Introduction

At the core of `Xamarin.Android` is its ability to interoperate with
the Java/Kotlin APIs implemented in the Android system.  To make it
work, it is necessary to "bridge" the two separate worlds of Java VM
(`ART` in the Android OS) and the Managed VM (`MonoVM`).  Application
developer expects to be able to call native Android APIs and receive
calls (or react to events) from the Android side using code written in
one of the .NET managed languages.  In order to make it work,
`Xamarin.Android` employs a number of techniques, both at build and at
run time, which are described in the sections below.

This guide is meant to explain the technical implementation in a way
that is sufficient to understand the system without having to read the
actual source code.

# Java <-> Managed interoperability overview

Java VM and Managed VM are two entirely separate entities which
co-exist in the same process/application.  Despite sharing the same
process resources, they don't "naturally" communicate with each other.
There is no direct way to call Java/Kotlin from .NET a'la the
`p/invoke` mechanism which allows calling native code APIs.  Nor there
exists a way for Java/Kotlin code to invoke managed methods.  In order
to make it possible, `Xamarin.Android` takes advantage of the Java's
[JNI](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html)
(`Java Native Interface`), a mechanism that allows native code
(managed code being "native" in this context) to register
implementations of Java methods, written outside the Java VM and in
languages other than Java/Kotlin (for instance in `C`, `C++` or
`Rust`).

Such methods need to be appropriately declared in the Java code, for
instance:

``` java
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

## Java Callable Wrappers

`Xamarin.Android` wraps the entire Android API by generating
appropriate C# code which reflects the Java/Kotlin code (classes,
interfaces, methods, properties etc).  Each generated class that
corresponds to a Java/Kotlin type, is derived from the
`Java.Lang.Object` class (implemented in the `Mono.Android` assembly),
which marks it as a "Java interoperable type", meaning that it can
implement or override virtual Java methods.  In order to make it
possible to register and invoke such methods, it is necessary to
generate a Java class which reflects the Managed one and provides an
entry point to the Java <-> Managed transition.  The Java classes are
generated during application (as well as `Xamarin.Android`) build and
we call them **Java Callable Wrappers**.  For instance, the following
managed class:

``` c#
public class MainActivity : AppCompatActivity
{
  public override Android.Views.View? OnCreateView (Android.Views.View? parent, string name, Android.Content.Context context, Android.Util.IAttributeSet attrs)
  {
    return base.OnCreateView (parent, name, context, attrs);
  }

  protected override void OnCreate(Bundle savedInstanceState)
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
not correspond to any method found in the base Java type.

The Java Callable Wrapper generated for the above class would look as
follows (a few generated methods not relevant to the discussion have
been omitted for brevity):

``` java
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
mechanisms described in farther sections of this document.  The
[Dynamic registration](#dynamic-registration) section will expand on
this example in order to explain the details of how the Managed type
and its methods are registered with the Java VM.

## Managed Callable Wrappers

# Dynamic registration

This registration mechanism has been used by `Xamarin.Android` since
the beginning and it will remain in use for the foreseeable future
when the application is built in the `Debug` configuration or when
[Marshal Methods](#marshal-methods) are turned off.

## Java Callable Wrappers registration code

## Registration call sequence

# Marshal methods 

## JNI requirements

## LLVM IR code generation

## Assembly rewriting 

### Wrappers for methods with non-blittable types

### UnmanagedCallersOnly attribute
