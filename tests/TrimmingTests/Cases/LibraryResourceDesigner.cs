#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.libraryresourcedesigner
#:property SupportedOSPlatformVersion=24
#:package SkiaSharp.Views@3.119.1
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/10852: library code that reads its Android resource IDs goes
// through _Microsoft.Android.Resource.Designer, which wasn't passed to ILC, so ILC compiled SKCanvasView.Initialize () to
// "will always throw" and creating the view crashed on NativeAOT.

using System;
using Android.App;
using Android.OS;
using SkiaSharp.Views.Android;

[Activity (Label = "LibraryResourceDesigner", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			SetContentView (new SKCanvasView (this));
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
