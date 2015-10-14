Timing:

The original Java.Interop effort weanted a type-safe and simple binding. As such, it usedd SafeHandles.

As the Xamarin.Forms team has turned their attention to profiling
Xamarin.Forms apps, and finding major Xamarin.Android-related
performance issues, performance needs to be considered.

For example, GC object allocation is a MAJOR concern for them;
ideally, you could have ZERO GC ALLOCATIONS performed when
invoking a Java method.

SafeHandles don't fit "nicely" in that world; every method that returns a SafeHandle ALLOCATES A NEW GC OBJECT.

So...how bad is it?

What's in this directory is a VERY TRIMMED DOWN Java.Interop layer.
Really, it's NOT Java.Interop; it's the core generated JniEnvironment.g.cs (as `jni.cs`)
with code for both SafeHandles and IntPtr-oriented invocation strategies.

The test? Invoke java.util.Arrays.binarySearch(int[], int) for 10,000,000 times.

Result:

    # SafeHandle timing: 00:00:02.7913432
    #	Average Invocation: 0.00027913432ms
    # JniObjectReference timing: 00:00:01.9809859
    #	Average Invocation: 0.00019809859ms

Basically, with a `JniObjectReference` struct-oriented approach, SafeHandles take ~1.4x as long to run.
Rephrased: the JniObjectReference struct takes 70% of the time of SafeHandles.

Ouch.

What about the current Xamarin.Android "all IntPtrs all the time!" approach?

    # SafeHandle timing: 00:00:02.8118485
    #	Average Invocation: 0.00028118485ms
    # JniObjectReference timing: 00:00:02.0061727
    #	Average Invocation: 0.00020061727ms

The performance difference is comparable -- SafeHandles take ~1.4x as long to run, or
IntPtrs take ~70% as long as using SafeHandles.

Interesting -- but probably not *that* interesting -- is that in an absolute sense, the `JniObjectReference`
struct was *faster* than the `IntPtr` approach, even though `JniObjectReference` contains *both* an `IntPtr`
*and* an enum -- and is thus bigger!

That doesn't make any sense.

Regardless, `JniObjectReference` doesn't appear to be *slower*, and thus should be a viable option here.

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
