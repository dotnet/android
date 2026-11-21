#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.typecheckonjavacreatedpeer
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/9399 (fixed by https://github.com/dotnet/android/pull/9435):
// `FindPreference (...) as SwitchPreferenceCompat` returned null in Release. Java creates the object, so the app never
// instantiates the bound type, and the trimmer folded the type check to false. Here the app never creates an
// Android.Content.ClipboardManager: it gets one from Android, type-checks it, and calls a member through the result.

using System;
using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Content.ClipboardManager")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Content.ClipboardManager", ".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)")]

[Activity (Label = "TypeCheckOnJavaCreatedPeer", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var service = GetSystemService (ClipboardService);
			if (service is not Android.Content.ClipboardManager clipboard) {
				throw new InvalidOperationException ($"The clipboard service was activated as {service?.GetType ()}.");
			}
			_ = clipboard.HasPrimaryClip;
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
