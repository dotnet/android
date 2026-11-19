#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.exportthrows
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/12564: the trimmable type map's Java callable wrappers dropped
// the `throws` clause of exported constructors, so Java callers lost the checked exceptions they declare.
// Case.targets keeps the `throws` clauses through R8, as an app that reflects over them has to.

using System;
using Android.App;
using Android.OS;
using Java.Interop;

[Activity (Label = "ExportThrows", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			using var thrower = new Thrower ();
			var constructor = thrower.Class.GetConstructor () ?? throw new InvalidOperationException ("No Thrower () constructor.");
			AssertThrowsIOException ("Thrower ()", constructor.GetExceptionTypes ());
			var method = thrower.Class.GetMethod ("check") ?? throw new InvalidOperationException ("No check () method.");
			AssertThrowsIOException ("check ()", method.GetExceptionTypes ());
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	static void AssertThrowsIOException (string member, Java.Lang.Class []? exceptionTypes)
	{
		if (exceptionTypes is null || !Array.Exists (exceptionTypes, t => t.Name == "java.io.IOException")) {
			throw new InvalidOperationException ($"{member} does not declare `throws java.io.IOException`.");
		}
	}
}

class Thrower : Java.Lang.Object
{
	[Export (Throws = new [] { typeof (Java.IO.IOException) })]
	public Thrower ()
	{
	}

	[Export ("check", Throws = new [] { typeof (Java.IO.IOException) })]
	public void Check ()
	{
	}
}
