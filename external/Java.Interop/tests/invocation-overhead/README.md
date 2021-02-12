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

What's in this directory is insanity: there are four different "strategies"
for dealing with JNI:

 1. `SafeHandle` All The Things! (`SafeTiming`)

 2. Xamarin.Android JNI handling from 2011 until Xamarin.Android 6.1 (2016)
    (`XAIntPtrTiming`)

    This uses `IntPtr`s *everywhere*, e.g. `JNIEnv::CallObjectMethod()` returns
    an `IntPtr`.

 3. "Happier Medium?" (`JIIntPtrTiming`)

    `IntPtr`s everywhere means it's trivial to forget that
    a JNI handle is a GREF vs. an LREF vs… What if we used the same `JNIEnv`
    invocation logic as `XAIntPtrTiming`, but instead of `IntPtr`s everywhere
    we instead had a `JniObjectReference` structure?

 4. "Optimize (3)" (`JIPinvokeTiming`)

    (3) was slower than (2).  What if we rethought the `JNIEnv`
    invocation logic and removed all the `Marshal.GetDelegateForFunctionPointer()`
    invocations with normal P/Invokes?

To compare these four strategies, `jnienv-gen.exe` was updated so that *all*
of them could be emitted into the same `.cs` file, into separate namespaces.
These "core" JNI bindings could then be used with to invoke
`java.util.Arrays.binarySearch(int[], int)`, 10,000,000 times, and compare
the results.

Result in 2015 (commit [25de1f38][25de]):

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

Does this mean SafeHandle-oriented use should die a horrible flaming death?

Perhaps.

However, I still fear a future precise-stack-scanning world, or handle-swizzling, or...
and SafeHandles and HandleRefs are the only ways I know of to help support precise GCs.
Unfortunately, `HandleRef` is NOT in *any* PCL profile, and thus isn't viable either
while fulfilling the other desires/requirements of Java.Interop, so that just leaves
`SafeHandle`s.

Which means, for "sanity", we'd want an API that can support both...at least with minor variations.

Meaning we "abstract out" the actual handle representation.

This isn't entirely straightforward; the point to an abstraction would be a stable API.

For example, what should `JNIEnv::CallObjectMethod()` return? It needs to return a `jobject`,
in some form, and that type itself needs to be part of the stable API.

We could say that it should be `IJavaObject` (or whatever), but the low-level wrappers shouldn't be *hidden*.
Sometimes you don't want that marshaling overhead! (See also recent Java.Interop.Dynamic-related commits).

The origial SafeHandle idea was that `JNIEnv::CallObjectMethod()` would return `JniLocalReference`,
but that's clearly no good now.

I think what we could instead do is have `JniObjectReference` as the stable API.
When supporting SafeHandles as a backend, JniObjectReference can contain the SafeHandle
as a member instead of the current IntPtr, thus preserving compatibility.

That handles return types. What about arguments?

The wonderful thing about SafeHandles (see above waxing poetic about precise GCs) is that
when passed as an argument to native code they'll be automagically pinned and kept alive.
(`HandleRef` does that too, but no `HandleRef` in PCL!)

The current (above) timing comparison uses `IntPtr` for arguments.

We should standardize on `JniObjectReference` (again).

## 2021 Timing Update

How do these timings compare in 2021 on Desktop Mono (macOS)?

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
