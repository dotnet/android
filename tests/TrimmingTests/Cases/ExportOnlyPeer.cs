#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.exportonlypeer
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13205 (fixed by https://github.com/dotnet/android/pull/13224):
// a Java peer whose [Export] methods are called only from Java had no managed references, so trimming removed it along
// with its Java callable wrapper. [Export] members are Java entry points that keep the peer; [Register] alone does not.

using System;
using Android.App;
using Android.Runtime;
using Java.Interop;
using Microsoft.Android.TrimmingTests.Expectations;

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
			var context = app ?? throw new InvalidOperationException ("No application.");
			// Load the Java class without referencing the managed type.
			var type = Java.Lang.Class.ForName ("net.dot.trimmingtests.ExportOnly", true, context.ClassLoader);
			var instance = type.GetConstructor ()?.NewInstance () ?? throw new InvalidOperationException ("newInstance returned null.");
			var value = type.GetMethod ("getValue")?.Invoke (instance)?.ToString ();
			if (value != "42") {
				throw new InvalidOperationException ($"getValue () returned {value}.");
			}
			var echo = type.GetMethod ("echo", Java.Lang.Class.ForName ("java.lang.String"))?.Invoke (instance, new Java.Lang.String ("hello"))?.ToString ();
			if (echo != "hello") {
				throw new InvalidOperationException ($"echo () returned {echo}.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[Register ("net/dot/trimmingtests/ExportOnly")]
[Kept]
[KeptMember (".ctor()")]
public class ExportOnly : Java.Lang.Object
{
	[Kept]
	[Export ("getValue")]
	public int GetValue () => 42;

	[Kept]
	[Export ("echo")]
	public string? Echo (string? value) => value;

	[Removed]
	public void NotExported () => Console.WriteLine ("never called");
}

[Register ("net/dot/trimmingtests/RegisterOnly")]
[Removed]
public class RegisterOnly : Java.Lang.Object
{
}
