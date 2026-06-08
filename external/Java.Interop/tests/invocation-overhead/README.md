# JNI Invocation Overhead

The original Java.Interop effort wanted a *type-safe* and *simple*
binding around JNI. As such, it used `SafeHandle`s.

As the Xamarin.Forms team has turned their attention to profiling
Xamarin.Forms apps, and finding major Xamarin.Android-related
performance issues, performance needed to be considered.

For example, GC object allocation is a MAJOR concern for them;
ideally, you could have ZERO GC ALLOCATIONS performed when
invoking a Java method.

`SafeHandle`s don't fit "nicely" in that world; every method that
returns a `SafeHandle` ALLOCATES A NEW GC OBJECT.

So...how bad is that?

What's in this directory is insanity: there are four different active
non-`SafeHandle` "strategies" for dealing with JNI:

 1. Xamarin.Android JNI handling from 2011 until Xamarin.Android 6.1 (2016)
    (`XAIntPtrTiming`)

    This uses `IntPtr`s *everywhere*, e.g. `JNIEnv::CallObjectMethod()` returns
    an `IntPtr`.

 2. "Happier Medium?" (`JIIntPtrTiming`)

    `IntPtr`s everywhere means it's trivial to forget that
    a JNI handle is a GREF vs. an LREF vs… What if we used the same `JNIEnv`
    invocation logic as `XAIntPtrTiming`, but instead of `IntPtr`s everywhere
    we instead had a `JniObjectReference` structure?

 3. "Optimize (2)" (`JIPinvokeTiming`)

    (2) was slower than (1).  What if we rethought the `JNIEnv`
    invocation logic and removed all the `Marshal.GetDelegateForFunctionPointer()`
    invocations with normal P/Invokes?

 4. Function pointer invocation with error handling (`JIFunctionPointersTiming`)

To compare these strategies, `jnienv-gen.exe` was updated so that *all* of them
could be emitted into the same `.cs` file, into separate namespaces.
These "core" JNI bindings could then be used with to invoke
`java.util.Arrays.binarySearch(int[], int)`, 10,000,000 times, and compare
the results.

Historically, this benchmark also included a `SafeHandle` strategy.  Result in
2015 (commit [25de1f38][25de]):

[25de]: https://github.com/xamarin/Java.Interop/commit/25de1f38bb6b3ef2d4c98d2d95923a4bd50d2ea0

    # SafeHandle timing: 00:00:02.7913432
    #	Average Invocation: 0.00027913432ms
    # JIIntPtrTiming timing: 00:00:01.9809859
    #	Average Invocation: 0.00019809859ms

Basically, with a `JniObjectReference` struct-oriented approach, SafeHandles
take ~1.4x longer to run. Rephrased: the `JniObjectReference` struct takes
70% of the time of SafeHandles.

Ouch.

What about the current Xamarin.Android "all IntPtrs all the time!" approach?

    # SafeHandle timing: 00:00:02.8118485
    #	Average Invocation: 0.00028118485ms
    # XAIntPtrTiming timing: 00:00:02.0061727
    #	Average Invocation: 0.00020061727ms

The performance difference is comparable -- SafeHandles take ~1.4x as long to
run, or IntPtrs take ~70% as long as using SafeHandles.

Interesting -- but probably not *that* interesting -- is that in an absolute
sense, the `JniObjectReference` struct was *faster* than the `IntPtr` approach,
even though `JniObjectReference` contains *both* an `IntPtr` *and* an enum --
and is thus bigger!

That doesn't make any sense.

Regardless, `JniObjectReference` doesn't appear to be *slower*, and thus should
be a viable option here.

---

These historical results led to `JniObjectReference` becoming the stable public
API instead of exposing `JniLocalReference` or other `SafeHandle` subclasses.
The optional SafeHandle-backed implementation was kept for migration and
comparison, but was never used by the active build and is no longer maintained.
The supported representation is now the `IntPtr`-backed `JniObjectReference`
struct.

## Historical 2021 Timing Update

How did the old `SafeHandle` timings compare in 2021 on Desktop Mono (macOS)?

    # SafeTiming timing: 00:00:09.3850449
    #	Average Invocation: 0.00093850449ms
    # XAIntPtrTiming timing: 00:00:04.4930288
    #	Average Invocation: 0.00044930288ms
    # JIIntPtrTiming timing: 00:00:04.5563368
    #	Average Invocation: 0.00045563368ms
    # JIPinvokeTiming timing: 00:00:03.4710383
    #	Average Invocation: 0.00034710383ms

In an absolute sense, things are worse: 10e6 invocations in 2015 took 2-3sec.
Now, they're taking at least 3.5sec.

In a relative sense, `SafeHandles` got *worse*, and takes 2.09x longer than
`XAIntPtrTiming`, and 2.7x longer than `JIPinvokeTiming`!

What about .NET Core 3.1?  After some finagling, *that* can work too!

    # SafeTiming timing: 00:00:05.1734443
    #	Average Invocation: 0.00051734443ms
    # XAIntPtrTiming timing: 00:00:03.1048897
    #	Average Invocation: 0.00031048897ms
    # JIIntPtrTiming timing: 00:00:03.4353958
    #	Average Invocation: 0.00034353958ms
    # JIPinvokeTiming timing: 00:00:02.7470934
    #	Average Invocation: 0.00027470934000000004ms

Relative performance is a similar story: `SafeHandle`s are slowest.
