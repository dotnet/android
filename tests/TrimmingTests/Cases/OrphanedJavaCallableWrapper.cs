#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.orphanedjavacallablewrapper
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13650: a Java peer that survives trimming without being
// created in managed code loses its constructors and type map proxy, but its Java callable wrapper was still packaged, so
// Java creating it failed with "No implementation found for ... nctor_0". The Java class must be removed as well.

using System;
using Android.App;
using Android.Runtime;
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
			if (OrphanPeer.Created) {
				throw new InvalidOperationException ("Nothing should have created an OrphanPeer yet.");
			}
			try {
				var instance = Java.Lang.Class.ForName ("net.dot.trimmingtests.OrphanPeer", true, context.ClassLoader).GetConstructor ()?.NewInstance ();
				throw new InvalidOperationException ($"Java created the trimmed peer as {instance?.GetType ()}.");
			} catch (Java.Lang.ClassNotFoundException) {
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

// The static field keeps the type, but nothing in managed code creates it. ILC needs no type data for a static field alone.
[Android.Runtime.Register ("net/dot/trimmingtests/OrphanPeer")]
[Kept (By = Tool.Trimmer)]
public class OrphanPeer : Java.Lang.Object
{
	public static bool Created;

	[Removed]
	public OrphanPeer () => Created = true;
}
