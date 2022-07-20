# Architecture

## Xamarin.Android Architecture

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

2. [tools/generator](tools/generator),
    which generates *binding assemblies*, which in turn contain
    three things: (1) JNI glue code to permit managed code to invoke Java code;
    (2) JNI marshal methods to facilitate Java code calling managed code; and
    (3) lots of custom attributes to facilitate Android Callable Wrapper
    generation.

3. [Java Callable Wrappers][xa-acw], which are "Java stubs" containing Java
    code with native method declarations for all methods overridden or
    implemented from managed code. See also
    [Java.Interop.Tools.JavaCallableWrappers](src/Java.Interop.Tools.JavaCallableWrappers).

[xa-acw]: http://developer.xamarin.com/guides/android/advanced_topics/java_integration_overview/android_callable_wrappers/

4. MSBuild glue code to glue various things together. See
    [xamarin-android/src/Xamarin.Android.Build.Tasks][xa-tasks].

[xa-tasks]: https://github.com/xamarin/xamarin-android/tree/master/src/Xamarin.Android.Build.Tasks

Furthermore, there's a matter of "time": binding assemblies (2) are emitted
at one time, while everything else (1, 3, 4) are bundled with the SDK and
thus could potentially change. Consequently, all four need to be kept in sync;
there is, in effect, an ABI between `Mono.Android.dll`, binding assemblies,
the build process, and Java callable wrappers. Any fix that involves the
boundary between these may "leak" into other areas, or otherwise not be
viable without requiring e.g. that customers rebuild binding assemblies.

What would such a change be?

For example, we would like Xamarin.Android to support *Ahead Of Time* (AOT)
compilation of assemblies into native code, to reduce or eliminate JIT
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

It's a big, monolithic, ball of mud.

## Proposed Java.Interop Architecture

Java.Interop aims to (eventually) change everything. (This is the end-game; the
commit history at the time of this writing does *not* fulfill this.)

The problem with Xamarin.Android is a lack of flexibility:

* Binding assemblies limit wide-scale improvements.
* Marshaling control and behavior is restricted from public use.
* Tying JNI glue code mechanics with the Android API makes it harder to reuse
    JNI glue code elsewhere (the desktop JVM?).
* An incomplete binding ABI restricts fully embracing AOT

Relatedly, there has long been a desire to provide a
[C# 4 `dynamic` provider][Java.Interop.Dynamic] to permit invoking Java methods
without requiring a separately generated binding assembly. `dynamic` providers,
in turn, implement the [IDynamicMetaObjectProvider][IDynamicMetaObjectProvider]
interface, which is based ~entirely upon
[System.Linq.Expressions][System.Linq.Expressions], which *also* supports
generating IL for execution at runtime (or saving to disk).

[Java.Interop.Dynamic]: src/Java.Interop.Dynamic
[IDynamicMetaObjectProvider]: https://msdn.microsoft.com/en-us/library/system.dynamic.idynamicmetaobjectprovider%28v=vs.110%29.aspx
[System.Linq.Expressions]: https://msdn.microsoft.com/en-us/library/system.linq.expressions.aspx

Thus, the solution to *all our problems*? *Embrace* `System.Linq.Expressions`.
We still need a separate code generator for binding assemblies, but *instead*
of emitting "static" C# code that hardcodes all information about marshaling,
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
        Func<Java.Lang.Object, Java.Lang.Object> d = Delegate.CreateDelegate (...);
        JniEnvironment.Runtime.InvokeMethod (jnienv, native__this, d /*, args... */);
    }

(API needs work/thinking through.)

The idea is that the generator-emitted methods would be "shims" into
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
            var __this = __jvm.ValueManager.GetValue<ImplementationType>(native__this);
            var __mret = __this.Clone ();
            var __jret = References.NewReturnToJniRef(__mret);
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
assemblies, and allow new types to participate in the marshal method code
generation. This allows for a more flexible marshaling system, with fewer
interdependencies, and could permit long-desired features such as
[value marshaling][value-marshaling] (copying Java values into managed types
which *don't* inherit from `JavaObject`, e.g. marshal a
`Android.Graphics.Point` into a `System.Drawing.Point`, eliminating the need
for a long-held, GC-tracked, JNI Global Reference.)

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
approach, the overhead is significant: to *just* invoke
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

*This is not acceptable*. Performance is a concern with Xamarin.Android;
we can't be making it *worse*.

Meanwhile, I *really* dislike using `IntPtr`s everywhere, as it doesn't let you
know what the value actually represents.

To solve this issue, *avoid `SafeHandle` types* in the public API.

Downside: this means we can't have the GC collect our garbage JNI references.

Upside: the Java.Interop effort will actually be usable.

Instead of using `SafeHandle` types, we introduce a
`JniObjectReference` struct type. This represents a JNI Local, Global, or
WeakGlobal object reference. The `JniObjectReference` struct also contains
the *reference type* as `JniObjectReferenceType`.
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


## Naming Conventions

Types with a `Java` prefix are "high-level" types which participate in cross-VM
object-reference semantics, e.g. you could add a `JavaObject` subclass to a
Java-side collection, perform a GC, and the instance will survive the GC.

Types with a `Jni` prefix are "low-level" types and do *not* participate in
object-reference semantics.


## Notes

### JDK and Global References

The JDK VM supports an effectively unlimited number of global references.
While Dalvik bails out after creating ~64k GREFs, consider the following
on the JDK:

    var t = new JniType ("java/lang/Object");
    var c = t.GetConstructor ("()V");
    var o = t.NewInstance (c);
    int count = 0;
    while (true) {
        Console.WriteLine ("count: {0}", count++);
        o.NewGlobalRef ();
    }

I halted the above loop after reaching 25686556 instances.

    count: 25686556
    ^C

I'm not sure when the JDK would stop handing out references, but it's probably
bound to process heap limits (e.g. depends on 32-bit vs. 64-bit process).


