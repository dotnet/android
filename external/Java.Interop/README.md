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

### JDK and Global References

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

### JNI Invocation Overhead

[tests/PerformanceTests/TimingTests.cs](tests/PerformanceTests/TimingTests.cs)
contains various tests to investigate the overheads involved in using JNI to
invoke Java methods. In particular, see the
`Java.Interop.PerformanceTests.JniMethodInvocationOverheadTiming.MethodInvocationTiming()`
tests, invokes a JNI method `count` times and attempts to "normalize" that to
invoking an equivalent, non-inlined, C# method.

[Commit c60f6093][c60f6093] observed that Android appeared to be much faster
than the JVM at these tests. This observation appears to have been wrong.
More interesting is that the "JNI method invocation overhead," defined as
what this test is attempting to measure (which may be wrong!), varies
based on the number of method invocations.

[c60f6093]: https://github.com/xamarin/Java.Interop/commit/c60f6093

For example, if we look at just the summary information of one test
as we vary the value of `count`, the number of times we invoke the
Java method via JNI or the C# method, we see that there is a nonlinear
relationship between the count and the overhead:

    count=    10: Method Invoke: static void i3: JNI is 5x managed
    count=   100: Method Invoke: static void i3: JNI is 10x managed
    count=   500: Method Invoke: static void i3: JNI is 22x managed
    count=  1000: Method Invoke: static void i3: JNI is 63x managed
    count= 10000: Method Invoke: static void i3: JNI is 474x managed
    count=100000: Method Invoke: static void i3: JNI is 808x managed

Particularly troubling is the *huge* jump between count=1000 and
count=10000. Count=1000000 is provided for comparison with the JVM,
which provides the following results:

    count=1000000: Method Invoke: static void i3: JNI is 413x managed   [JVM]

We don't know why there's a jump, but this is in fact somewhat encouraging:
if you're only calling methods in a one-off fashion -- as is frequently
the case in Xamarin.Android -- then the overhead isn't actually that bad,
on a per-method invoke basis. It appears to only get really bad when
invoking the same method repetitively, *a lot*, which I believe shouldn't
be *that* common a use case (outside of image manipulation?).

### JNI and P/Invoke

[Commit 9d2dfc5][9d2dfc5] observed that there's a fair bit of overhead
associated with using `SafeHandle`s, in large parge because `SafeHandle`s
need to be [*thread safe*][cbrumme-SafeHandle] in order to prevent
[handle recycling attacks][handle-recycle]. Xamarin.Android doesn't
suffer from handle recycling attacks *only* because Mono's SGEN GC
conservatively scans the stack, prolonging the lifetime of all temporaries
found there. If/when Xamarin.Android moves to a precise GC for the stack,
this will no longer be the case and handle recycling attacks -- along
with possibly finalizing/`Dispose()`ing of instances
*while they're still being used* -- can become "a thing".

[9d2dfc5]: https://github.com/xamarin/Java.Interop/commit/9d2dfc5
[cbrumme-SafeHandle]: http://blogs.msdn.com/b/cbrumme/archive/2004/02/20/77460.aspx
[handle-recycle]: http://blogs.msdn.com/b/cbrumme/archive/2003/04/19/51365.aspx

An idea that came to mind to reduce the overhead of `SafeHandle` use was to
P/Invoke to a native library to perform the `JNIEnv` function pointer
invocations instead of using `delegate` invocations alongside
`Marshal.GetDelegateForFunctionPointer()`, as is currently the case.

This was implemented in the [pinvoke-jnienv][pinvoke-jnienv] branch,
in [commit 802842a3][802842a3].

[pinvoke-jnienv]: https://github.com/xamarin/Java.Interop/commits/pinvoke-jnienv
[802842a3]: https://github.com/xamarin/Java.Interop/commit/802842a361380812e290fe3585fea8c0a7a19b97

The result: Using P/Invoke *increases* invocation overhead:

	# "Full" Invocations: JNIEnv::CallObjectMethod() + JNIEnv::DeleteLocalRef() for 10000 iterations
	           Java.Interop Object.toString() Timing: 00:00:30.0774575;   3.00774575 ms/iteration                                -- ~386.391119190154x
	        Xamarin.Android Object.toString() Timing: 00:00:00.0778420;    0.0077842 ms/iteration
	# JNIEnv::CallObjectMethod() for 500 iterations
	           Java.Interop Object.toString() Timing: 00:00:00.8041310;     1.608262 ms/CallVirtualObjectMethod()                -- ~266.648207712969x
	       Xamarin.Android CallObjectMethod() Timing: 00:00:00.0030157;    0.0060314 ms/CallObjectMethod()
	# JNIEnv::DeleteLocalRef() for 500 iterations
	 Java.Interop JniLocalReference.Dispose() Timing: 00:00:00.8979645;     1.795929 ms/Dispose()                                -- ~1661.05160932297x
	         Xamarin.Android DeleteLocalRef() Timing: 00:00:00.0005406;    0.0010812 ms/DeleteLocalRef()
	## Breaking down the above Object.toString() + JniLocalReference.Dispose() timings, the JNI calls:
	# JNIEnv::CallObjectMethod: SafeHandle vs. IntPtr
	                  Java.Interop safeCall() Timing: 00:00:00.0058775;     0.011755 ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~2.14538618776464x
	         Java.Interop P/Invoke safeCall() Timing: 00:00:00.0069479;    0.0138958 ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~2.53610016060739x
	                Java.Interop unsafeCall() Timing: 00:00:00.0027396;    0.0054792 ms/IntPtr JNIEnv::CallObjectMethodA()
	# JNIEnv::DeleteLocalRef: SafeHandle vs. IntPtr
	                   Java.Interop safeDel() Timing: 00:00:00.0006010;     0.001202 ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~1.47412312975227x
	          Java.Interop P/Invoke safeDel() Timing: 00:00:00.0007480;     0.001496 ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~1.83468236448369x
	                 Java.Interop unsafeDel() Timing: 00:00:00.0004077;    0.0008154 ms/IntPtr JNIEnv::DeleteLocalRef

In particular, note the `Java.Interop P/Invoke` lines:

	         Java.Interop P/Invoke safeCall() Timing: 00:00:00.0069479;    0.0138958 ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~2.53610016060739x
	          Java.Interop P/Invoke safeDel() Timing: 00:00:00.0007480;     0.001496 ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~1.83468236448369x

Compare to the `SafeHandle`-using delegate-based invocations:

	                  Java.Interop safeCall() Timing: 00:00:00.0058775;     0.011755 ms/SafeHandle JNIEnv::CallObjectMethodA()   -- ~2.14538618776464x
	                   Java.Interop safeDel() Timing: 00:00:00.0006010;     0.001202 ms/SafeHandle JNIEnv::DeleteLocalRef()      -- ~1.47412312975227x

Surprisingly, using delegates results in less overhead than using P/Invoke.
