#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.castclasstoboundtype
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13286: `(PowerManager) GetSystemService (...)` throws
// InvalidCastException on CoreCLR, because ILLink keeps the type map entry of a bound type for `isinst` but not for
// `castclass`, so Android's object is activated as Java.Lang.Object. The app references PowerManager only in a cast.

using System;
using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.OS.PowerManager")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.OS.PowerManager",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "get_IsInteractive()")]

[Activity (Label = "CastClassToBoundType", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var power = (PowerManager?) GetSystemService (PowerService) ?? throw new InvalidOperationException ("No power service.");
			_ = power.IsInteractive;
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
