# Motivation

**Java.Interop** is a binding of the [Java Native Interface][jni] for use from
managed languages such as C#, and an associated set of code generators to
allow Java code to invoke managed code. It is *also* a brain-delusional
[Second System Syndrome][sss] rebuild of the monodroid/Xamarin.Android core,
intended to fix some of the shortcomings and design mistakes I've made over the years.

[jni]: http://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/jniTOC.html
[sss]: http://en.wikipedia.org/wiki/Second-system_effect

In particular, it attempts to fix the following issues: 

* Split out the core invocation logic so that the containing assembly is in the
  `xbuild-frameworks\MonoAndroid\v1.0` directory, allowing low-level JNI use
  without taking an API-level constraint.
* Make the assembly a PCL lib.
* Support use of the lib on "desktop" Java VMs. This would allow more testing
  without an Android device, could allow using Xamarin.Android Views to be shown
  in the GUI designer, etc.
* Improve type safety.
* Improve consistency.

In particular are the last two points: Xamarin.Android currently uses `IntPtr`s
*everywhere*, and it's not at all obvious what they are (method IDs vs.
local refs vs. global refs vs. ...). This culminates in `JNIEnv.FindClass()`,
which returns a global reference while most other methods return a local ref.

The `JNIEnv` API is also huge, unwieldy, and terrible.


