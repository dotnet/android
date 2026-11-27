#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javaasinterfaceoftrimmedclass
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/9038 (and the open https://github.com/dotnet/android/issues/9738):
// when the app never references the most derived binding of a Java object, the trimmer removes it and the object is
// activated as a base type that does not implement the bound interface, so `is IAnimatable` fails. JavaAs<T> () is the
// supported way to get the interface: it checks the Java type and activates the interface's invoker.

using System;
using Android.App;
using Android.Graphics.Drawables;
using Android.Runtime;
using Java.Interop;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Graphics.Drawables.IAnimatableInvoker")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Graphics.Drawables.IAnimatableInvoker",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "get_IsRunning()")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Graphics.Drawables.AnimationDrawable")]

[Instrumentation (Name = "net.dot.trimmingtests.CaseInstrumentation")]
public class CaseInstrumentation : Instrumentation
{
	// Android creates instrumentations before the runtime starts, so their peer is activated later through this constructor.
	public CaseInstrumentation (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public override void CallApplicationOnCreate (Application? app)
	{
		base.CallApplicationOnCreate (app);
		try {
			// Create an android.graphics.drawable.AnimationDrawable without referencing its binding.
			var constructor = Java.Lang.Class.ForName ("android.graphics.drawable.AnimationDrawable").GetConstructor ()
				?? throw new InvalidOperationException ("AnimationDrawable has no default constructor.");
			var drawable = constructor.NewInstance () ?? throw new InvalidOperationException ("newInstance returned null.");
			var animatable = drawable.JavaAs<IAnimatable> ()
				?? throw new InvalidOperationException ($"JavaAs<IAnimatable> () returned null for {drawable.GetType ()}.");
			if (animatable.IsRunning) {
				throw new InvalidOperationException ("A new AnimationDrawable should not be running.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
