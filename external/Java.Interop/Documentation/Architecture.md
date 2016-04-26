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


