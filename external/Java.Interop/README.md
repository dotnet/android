# Java.Interop

**Java.Interop** is a brain-delusional [Second System Syndrome][sss] rebuild
 of the monodroid/Xamarin.Android core, intended to fix some of the shortcomings
  and design mistakes I've made over the years.

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

In particular are the last two points: monodroid currently uses `IntPtr`s
*everywhere*, and it's not at all obvious what they are (method IDs vs.
local refs vs. global refs vs. ...). This culminates in `JNIEnv.FindClass()`,
which returns a global reference while most other methods return a local ref.

The `JNIEnv` API is also huge, unwieldy, and terrible.

## Requirements

The current Oracle JDK7 installer only provides 64-bit binaries, while
Mono for OS X is currently a 32-bit binary. These don't work together. :-(

Consequently, you must download the Apple OS X Java 6 developer package:

 1. Go to the [Downloads page](https://developer.apple.com/downloads/index.action).  
    Note: This may require you to login first.
 2. In the "Search" panel (above the "tree" on the left), enter "Java". Hit `[RETURN]`.
 3. Look for the **Java for OS X ... Developer Package** entry.  
    To the right, under the Release Date column, is the installer. Click it.

At the time of this writing, this links to
[Java for OS X 2013-005 Developer Package][osx-jdk6], released October 15, 2013.

[osx-jdk6]: http://adcdownload.apple.com/Developer_Tools/java_for_os_x_2013005_developer_package/java_for_os_x_2013005_dp__11m4609.dmg

*Furthermore*, if running on Yosemite you must *also* download the latest
[Java for OS X package](http://support.apple.com/downloads/#java), currently
[JavaForOSX2014-001.dmg](http://support.apple.com/downloads/DL1572/en_US/JavaForOSX2014-001.dmg).

Once download, you need to "remove" any previously installed Java packages, as
the Developer package won't install over a newer runtime packages:

    sudo mv /System/Library/Frameworks/JavaVM.framework /System/Library/Frameworks/JavaVM.framework-Yosemite

Then install the Developer Package `java_for_os_x_2013005_dp__11m4609.dmg`,
then install the runtime package `JavaForOSX2014-001.dmg`.

If you fail to re-install the runtime package, then `jar` will fail to run:

    $ jar cf "../../bin/Debug/java-interop.jar" -C "../../bin/Debug/ji-classes" .
    java.lang.AssertionError: Platform not recognized
            at sun.nio.fs.DefaultFileSystemProvider.create(DefaultFileSystemProvider.java:73)
            at java.nio.file.FileSystems$DefaultFileSystemHolder.getDefaultProvider(FileSystems.java:108)
    ...


## Type Safety

The start of the reboot is to use strongly typed [`SafeHandle`][SafeHandle]
subclasses everywhere instead of `IntPtr`. This allows a local reference to be
type-checked and distinct from a global ref, complete with compiler
type checking.

[SafeHandle]: http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.safehandle.aspx

Since we now have actual types in more places, we can move the current `JNIEnv`
methods into more semantically meaningful types.

## Naming Conventions

Types with a `Java` prefix are "high-level" types which participate in cross-VM
object-reference semantics, e.g. you could add a `JavaObject` subclass to a
Java-side collection, perform a GC, and the instance will survive the GC.

The exception to this rule is the `JavaVM` type, which is the entrypoint
to doing lots of interesting things.

Types with a `Jni` prefix are "low-level" types and do *not* participate in
object-reference semantics.

## Problems

Due to the increases use of reference types, there will be increased GC heap
use. I don't know if this will have a meaningful impact on performance. 

## Android Tests

The top-level `make run-android` target will run the Java.Interop unit tests
on Android via the Android.Interop-Tests project.

The Android.Interop-Tests project currently contains *all* tests, including
the time intensive "PerformanceTests".

To run a specific test fixture, set the FIXTURE variable:

    make run-android FIXTURE=Java.Interop.PerformanceTests.TimingTests

## Notes

The JDK VM supports an effectively unlimited number of global references.
While Dalvik craps out after creating ~64k GREFs, consider the following
on the JDK:

    var t = new JniType ("java/lang/Object");
    var c = t.GetConstructor ("()V");
    var o = t.NewInstance (c);
    int count = 0;
    while (true) {
        Console.WriteLine ("count: {0}", count++);
        o.NewGlobalRef ();
    }

I killed the above loop after reaching 25686556 instances.

    count: 25686556
    ^C

I'm not sure when the JDK would stop handing out references, but it's probably
bound to process heap limits (e.g. depends on 32-bit vs. 64-bit process).
