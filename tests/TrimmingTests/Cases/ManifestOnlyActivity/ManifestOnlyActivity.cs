#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.manifestonlyactivity
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/11037 and https://github.com/dotnet/android/pull/12498: a Java
// peer that only a hand-written AndroidManifest.xml entry refers to has no managed references, so the trimmable type map
// has to root it; otherwise Android can't create the activity.

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[Activity (Label = "ManifestOnlyActivity", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			// The started activity reports the result.
			StartActivity (new Intent ().SetClassName (PackageName!, "net.dot.trimmingtests.ManifestDeclaredActivity"));
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[Register ("net/dot/trimmingtests/ManifestDeclaredActivity")]
[Kept]
[KeptMember (".ctor()")]
public class ManifestDeclaredActivity : Activity
{
	[Kept]
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		Finish ();
	}
}
