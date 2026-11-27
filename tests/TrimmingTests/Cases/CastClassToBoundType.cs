#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.castclasstoboundtype
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13286: `(PowerManager) GetSystemService (...)` throws
// InvalidCastException on CoreCLR, because ILLink keeps the type map entry of a bound type for `isinst` but not for
// `castclass`, so Android's object is activated as Java.Lang.Object. The app never creates or type-checks a
// PowerManager: it casts the object Android returns and calls a member through the result.

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.OS.PowerManager")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.OS.PowerManager",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "get_IsInteractive()")]

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
			var power = (PowerManager?) context.GetSystemService (Context.PowerService) ?? throw new InvalidOperationException ("No power service.");
			_ = power.IsInteractive;
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
