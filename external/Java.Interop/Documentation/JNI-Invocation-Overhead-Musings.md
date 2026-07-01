# JNI Invocation Overhead Musings

[tests/PerformanceTests/TimingTests.cs](tests/PerformanceTests/TimingTests.cs)
contains various tests to investigate the overheads involved in using JNI to
invoke Java methods. In particular, see the
`Java.Interop.PerformanceTests.JniMethodInvocationOverheadTiming.MethodInvocationTiming()`
tests, invokes a JNI method `count` times and attempts to "normalize" that to
invoking an equivalent, non-inlined, C# method.

**Update**: I believe the tests are wrong, but I don't know *how* they're
wrong. (Leaving "misleading" statements in this file until I know what's
actually going on.)

The content below suggests that the JNI overhead is proportional with the
number of JNI invocations, e.g. when `count` is 10, JNI invocations are ~5x
that of an "equivalent" C# method invocation, while when `count` is 100000,
JNI invocations are ~808x, which makes *no sense at all*, but that was the
observed behavior.

Further investigation suggests that this hypothesis is bunk, those numbers
are bunk, something else is going on, and I still have no idea what's going on.

That said, consider [commit c9db386c][c9db386c], which now starts printing
the *average* invocation time in a nice, human-readable, format.
When `count` is 100,000, the `static void i3` output is:

[c9db386c]: https://github.com/xamarin/Java.Interop/commit/c9db386c5457ff6243b3e36d919ca8669f502192

    Method Invoke: static void i3: JNI is 94x managed
              C/JNI:    2.0     ms               | average:      0.002   ms
                JNI:   14.3707  ms;   7x C/JNI   | average:      0.01437 ms
            Managed:    0.1529  ms               | average:      0.00015 ms
            Pinvoke:    0.1269  ms;   1x managed | average:      0.00013 ms

(Quick aside: odd that the above run says JNI is 94x managed, while below
it's 808x managed! Numbers be *weird*.)

Of interest here is the **C/JNI** line, which shows that the average
invocation time of the `static void i3` method is 0.002ms.

What the description below suggests is that this average time is proportional
to `count`: when `count` is small, the C/JNI average time is smaller than
the C/JNI average time when `count` is large.

*This cannot be independently verified.*

In point of fact, [I cannot reproduce this behavior][art-timing-test.zip].
No matter what `count` value I choose, the average time for invoking
`JNIEnv::CallStaticVoidMethod()` *is the same* (roughly): 0.001ms. (It differs
from the above `static void i3` value, likely because it's a `static void`).

[art-timing-test.zip]: https://files.xamarin.com/~jonp/tests/art-timing-test.zip

For a given Java method signature, e.g. `static void m()`, invocation time is
consistent on ART (Android M Preview 2, Nexus 5). It doesn't vary, certainly not
with the `count` value.

Instead, JNI overhead appears to be fairly consistent (for a given signature).
At ~0.002ms per invocation, you can perform roughly 500,000 JNI method
invocations per *second* from C#, which sounds good unless you need to
manipulate raw Bitmap data...

---

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
associated with using `SafeHandle`s, in large part because `SafeHandle`s
need to be [*thread safe*][cbrumme-SafeHandle] in order to prevent
[handle recycling attacks][handle-recycle]. Xamarin.Android doesn't
suffer from handle recycling attacks *only* because Mono's SGEN GC
conservatively scans the stack, prolonging the lifetime of all temporaries
found there. If/when Xamarin.Android moves to a precise GC for the stack,
this may no longer be the case and handle recycling attacks -- along
with possibly finalizing/`Dispose()`ing of instances
*while they're still being used* -- can become "a thing".

[9d2dfc5]: https://github.com/xamarin/Java.Interop/commit/9d2dfc5
[cbrumme-SafeHandle]: http://blogs.msdn.com/b/cbrumme/archive/2004/02/20/77460.aspx
[handle-recycle]: http://blogs.msdn.com/b/cbrumme/archive/2003/04/19/51365.aspx

(...except that handle recycling attacks *can't* become "a thing". A working
GC bridge precludes it, because collections must *always* be delayed until
a Java-side collection has been completed, as the JVM may be keeping an
instance alive. It is thus highly unlikely, even if a precise GC were used,
that a Java bridged instance would be collected in this manner.)

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
