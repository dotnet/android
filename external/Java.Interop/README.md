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

Unfortunately, you can't *install* it on El Capitan. It'll install...but it
won't *do* anything, probably because of [System Integrity Protection][sip].

[sip]: https://en.wikipedia.org/wiki/System_Integrity_Protection

To develop on El Capitan, download the above
`java_for_os_x_2013005_dp__11m4609.dmg` file, open it within Finder,
copy the contained `JavaDeveloper.pkg` file into this directory,
then run the `osx-setup` target:

    $ make osx-setup JDK=JavaDeveloper.pkg


## Type Safety

The start of the reboot was to use strongly typed [`SafeHandle`][SafeHandle]
subclasses everywhere instead of `IntPtr`. This allows a local reference to be
type-checked and distinct from a global ref, complete with compiler
type checking.

[SafeHandle]: http://msdn.microsoft.com/en-us/library/system.runtime.interopservices.safehandle.aspx

Since we now have actual types in more places, we can move the current `JNIEnv`
methods into more semantically meaningful types.

Unfortunately, various tests demonstrated that while `SafeHandle`s provided
increased type safety, they did so at a large runtime cost:

1. `SafeHandle`s are reference types, increasing GC heap allocations and pressure.
2. [`SafeHandle`s are *thread-safe* in order to prevent race conditions and handle recycling attacks][reliability].

[reliability]: http://blogs.msdn.com/b/bclteam/archive/2005/03/16/396900.aspx

Compared to a Xamarin.Android-like "use `IntPtr`s for *everything*" binding
approach, the overhread is significant: to *just* invoke
`JNIEnv::CallObjectMethod()`, using `SafeHandle`s for everything causes
execution time to take ~1.4x longer than a comparable struct-oriented approach.

Make the test more realistic -- compared to current Xamarin.Android and
current Java.Interop -- so that `JniEnvironment.Members.CallObjectMethod()`
also calls `JniEnvironment.Errors.ExceptionOccurred()`, which also returns
a JNI local reference -- and runtime execution time *jumped to ~3.6x*:

    # SafeHandle timing: 00:00:09.9393493
    #	Average Invocation: 0.00099393493ms
    # JniObjectReference timing: 00:00:02.7254572
    #	Average Invocation: 0.00027254572ms

(See the [tests/invocation-overhead](tests/invocation-overhead) directory
for the invocation comparison sourcecode.)

*This is not acceptable*. Performance is a known issue with Xamarin.Android;
we can't be making it *worse*.

Meanwhile, I *really* dislike using `IntPtr`s everywhere, as it doesn't let you
know what the value actually represents.

To solve this issue, *avoid `SafeHandle` types* in the public API.

Downside: this means we can't have the GC collect our garbage JNI references.

Upside: the Java.Interop effort will actually be usable.

Instead of using `SafeHandle` types, we introduce a
`JniObjectReference` struct type. This represents a JNI Local, Global, or
WeakGlobal object reference. The `JniObjectReference` struct also contains
the *reference type* as `JniObjectReferenceType`, formerly `JniReferenceType`.
`jmethodID` and `jfieldID` become "normal" class types, permitting type safety,
but lose their `SafeHandle` status, which was never really necessary because
they don't require cleanup *anyway*. Furthermore, these values should be
*cached* -- see `JniPeerMembers` -- so making them GC objects shouldn't be
a long-term problem.

By doing so, we allow Java.Interop to have *two separate implementations*,
controlled by build-time `#define`s:

* `FEATURE_HANDLES_ARE_SAFE_HANDLES`: Causes `JniObjectReference` to
    contain a `SafeHandle` wrapping the underlying JNI handle.
* `FEATURE_HANDLES_ARE_INTPTRS`: Causes `JniObjectReference` to contain
    an `IntPtr` for the underlying JNI handle.

The rationale for this is twofold:

1. It allows swapping out "safer" `SafeHandle` and "less safe" `IntPtr`
    implementations, permitting easier performance comparisons.
2. It allows migrating the existing code, as some of the existing
    tests may assume that JNI handles are garbage collected, which
    won't be the case when `FEATURE_HANDLES_ARE_INTPTRS` is set.

`FEATURE_HANDLES_ARE_INTPTRS` support is still in-progresss.

## Naming Conventions

Types with a `Java` prefix are "high-level" types which participate in cross-VM
object-reference semantics, e.g. you could add a `JavaObject` subclass to a
Java-side collection, perform a GC, and the instance will survive the GC.

Types with a `Jni` prefix are "low-level" types and do *not* participate in
object-reference semantics.

## Architecture

### Xamarin.Android Architecture

For reference/comparision, The [Xamarin.Android architecture][xa-arch] and
[JNI use][xa-jni] is reasonably well documented; *how* it all fits together
isn't.

[xa-arch]: http://developer.xamarin.com/guides/android/under_the_hood/architecture/
[xa-jni]: http://developer.xamarin.com/guides/android/advanced_topics/java_integration_overview/working_with_jni/

Within Xamarin.Android, there are four "moving parts":

1. `Mono.Android.dll`, which contains two things: (1) a `generator`-produced
    (2) binding of the Android API, and a fair bit of "glue code" to make
    things actually work -- type marshaling, method invocation, helper types,
    etc.

2. `generator`, which generates *binding assemblies*, which in turn contain
    three things: (1) JNI glue code to permit managed code to invoke Java code;
    (2) JNI marshal methods to facilitate Java code calling managed code; and
    (3) lots of custom attributes to facilitate Android Callable Wrapper
    generation.

3. [Android Callable Wrappers][xa-acw], which are "Java stubs" containing Java
    code with native method declarations for all methods overridden or
    implemented from managed code.

[xa-acw]: http://developer.xamarin.com/guides/android/advanced_topics/java_integration_overview/android_callable_wrappers/

4. MSBuild glue code to glue various things together.

Furthermore, there's a matter of "time": binding assemblies (2) are emitted
at one time, while everything else (1, 3, 4) are bundled with the SDK and
thus could potentially change. Consequently, all four need to be kept in sync;
there is, in effect, an ABI between `Mono.Android.dll`, binding assemblies,
the build process, and Android callable wrappers. Any fix that involves the
boundary between these may "leak" into other areas, or otherwise not be
viable without requiring e.g. that customers rebuild binding assemblies.

What would such a change be?

For example, we would like Xamarin.Android to support *Ahead Of Time* (AOT)
compilation of assemblies into native code, to reduce or elimitate JIT
overheads during process startup and runtime execution.

The problem is that, at present, *everything* needs to be wrapped in a
runtime-generated `try`/`catch` block (emitted via `System.Reflection.Emit`)
to perform Java exception marshaling duties.

To "fully" do this for AOT, the *binding assembly* would need to contain the
exception marshaling logic, which (1) originally couldn't be *written* in C#
(it used IL fault blocks), and (2) would increase the ABI requirements.
Alternatively, we'd need to instead generate "new" marshal methods at
packaging time, resulting in *3* places using *2* different code generators
that generate marshal methods (binding assemblies, AOT, `[Export]`).

*Then* there's the "minor" problem of the implementation of the
[`[Export]` custom attribute][xa-export], which currently *always* requires runtime
code generation. It would be nice to remove this requirement.

[xa-export]: http://developer.xamarin.com/guides/android/advanced_topics/java_integration_overview/working_with_jni/#ExportAttribute_and_ExportFieldAttribute

*Finally*, none of this is *extensible*: lots of marshaling logic is hardcoded,
e.g. translating `java.io.InputStream` to `System.IO.Stream` (and back),
and there's no facility for additional types to participate in *any* of this.

It's a big, monolothic, ball of mud.

### Java.Interop Architecture

Java.Interop aims to (eventually) change everything. (This is the end-gaim; the
commit history at the time of this writing does *not* fulfill this.)

The problem with Xamarin.Android is a lack of flexibility:

* Binding assemblies limit wide-scale improvements.
* Marshaling control and behavior is restricted from public use.
* Tying JNI glue code mechanics with the Android API makes it harder to reuse
    JNI glue code elsewhere (the desktop JVM?).
* An incomplete binding ABI restricts fully embracing AOT

Relatedly, there has long been a desire to provide a
[C# 4 `dynamic` provider][Java.Interop.Dynamic] to permit invoking Java methods
without requiring a separtely generated binding assembly. `dynamic` providers,
in turn, implement the [IDynamicMetaObjectProvider][IDynamicMetaObjectProvider]
interface, which is based ~entirly upon
[System.Linq.Expressions][System.Linq.Expressions], which *also* supports
generating IL for execution at runtime (or saving to disk).

[Java.Interop.Dynamic]: src/Java.Interop.Dynamic
[IDynamicMetaObjectProvider]: https://msdn.microsoft.com/en-us/library/system.dynamic.idynamicmetaobjectprovider%28v=vs.110%29.aspx
[System.Linq.Expressions]: https://msdn.microsoft.com/en-us/library/system.linq.expressions.aspx

Thus, the solution to *all our problems*? *Embrace* `System.Linq.Expressions`.
We still need a separate code generator for binding assemblies, but *instead*
of emmitting "static" C# code that hardcodes all information about marshaling,
have it call into a runtime method that performs the work *at runtime*:

    // generator-emitted marshal method:
    // Old-and-busted (current Xamarin.Android behavior)
    static IntPtr n_Clone (IntPtr jnienv, IntPtr native__this)
    {
        Java.Lang.Object __this = global::Java.Lang.Object.GetObject<Java.Lang.Object> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
        return JNIEnv.ToLocalJniHandle (__this.Clone ());
    }
    
    // New hotness *from generator* (proposed Java.Interop behavior)
    [Dynamic]
    static IntPtr n_Clone (IntPtr jnienv, IntPtr native__this)
    {
        JniTransition __envp = new JniTransition (jnienv);
        try {
            var __jvm = JniEnvironment.Runtime;
            return __jvm.CallObjectMethod (native__this, "Clone");
        }
        catch (Exception __e) {
            __envp.SetPendingException (__e);
        }
        finally {
            __envp.Dispose ();
        }
    }

(API needs work/thinking through.)

The idea is that the generator-emitted methods would be "shims" into1
Java.Interop methods which would do the heavy lifting.

This would, of course, result in additional runtime overhead.

To rectify this, we could use a post-build step which would *replace*
all these shims with *real* method bodies:

    // Post-build generated code
    static IntPtr n_Clone (IntPtr jnienv, IntPtr native__this)
    {
        JniTransition __envp = new JniTransition (jnienv);
        try {
            var __jvm = __envp.Runtime;
            var __this = __jvm.GetObject<ExportTest>(native__this);
            var __mret = __this.Clone ();
            __jret = Handles.NewReturnToJniRef(__mret);
            return __jret;
        }
        catch (Exception __e) {
            __envp.SetPendingException (__e);
        }
        finally {
            __envp.Dispose ();
        }
    }

By making everything dynamic and then *replacing* everything of consequence
at package time, we loosen up ABI restrictions, allow marshaling bugs to
be inserted in future releases without requiring re-generation of binding
assemblies, and allow new types to par†icipate in the marshal method code
generation. This allows for a more flexibble marshaling system, with fewer
interdependencies, and could permit long-desired features such as
[value marshaling][value-marshaling] (copying Java values into managed types
which *don't* inherit from `JavaObject`, e.g. marshal a
`Android.Graphics.Point` into a `System.Drawing.Point`, eliminating the need
for a JNI Global Reference.)

[value-marshaling]: https://trello.com/c/M8zkFtR3/143-research-adding-valuetype-semantics-to-some-types

Furthermore, by *relying* on a post-build step, this also allows for easily
supporting `[Export]`/`[JavaCallable]` annotated methods without *requiring*
runtime code generation, *and* to AOT the marshal methods for them!

We *centralize* the *actual* value marshaling logic into `Java.Interop`,
removing code duplication from `generator`, and use that infrastructure
for as much as possible: C# 4 `dynamic`, post-build code generation,
runtime code generation (to support Debug builds so post-build steps
aren't always required).

That said, we also want to *emphasize* the efficient path: we don't want
the "simple" path to require code generation; we want the simple path
to support and prefer the post-build AOT-supporting codegen path.
This in turn means that certain implemented features -- such as generic
argument marshaling -- need to change so that they're not inadvertently
used.

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
When `count` is 100000, the `static void i3` output is:

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
with the `count` vaue.

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
