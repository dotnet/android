#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.unicodejavanames
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/12607: Java peers with supported non-ASCII names must keep
// those names through the trimmable type map (Java callable wrapper, type map key, DEX) and be activated from Java.

using System;
using Android.App;
using Android.Runtime;

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
			using var café = new Café ();
			if (!café.Class.Name.EndsWith (".Café", StringComparison.Ordinal)) {
				throw new InvalidOperationException ($"Café's Java class is '{café.Class.Name}'.");
			}
			AssertActivatedFromJava (app?.ClassLoader, "net.dot.trimmingtests.Ünïcode", typeof (Ünïcode));
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	static void AssertActivatedFromJava (Java.Lang.ClassLoader? loader, string javaName, Type expected)
	{
		var instance = Java.Lang.Class.ForName (javaName, true, loader).GetConstructor ()?.NewInstance ();
		if (instance?.GetType () != expected) {
			throw new InvalidOperationException ($"new {javaName} () was activated as {instance?.GetType ()}.");
		}
	}
}

public class Café : Java.Lang.Object
{
}

[Register ("net/dot/trimmingtests/Ünïcode")]
public class Ünïcode : Java.Lang.Object
{
}
