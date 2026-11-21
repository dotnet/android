#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property RootNamespace=TrimmingTests
#:property AndroidGenerateLayoutBindings=true
#:property ApplicationId=net.dot.android.trimmingtests.layoutbindings
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/8807 (fixed by https://github.com/dotnet/android/pull/8805):
// the code generated for layout bindings (AndroidGenerateLayoutBindings) produced trimming warnings in every app.

using System;
using Android.App;
using Android.OS;
using Android.Util;

namespace TrimmingTests
{
	[Activity (Label = "LayoutBindings", MainLauncher = true)]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			try {
				SetContentView (Resource.Layout.main);
				var binding = new Binding.main (this);
				if (binding.message?.Text != "hello") {
					throw new InvalidOperationException ($"The bound TextView shows '{binding.message?.Text}'.");
				}
				Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
			} catch (Exception e) {
				Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
			}
		}
	}
}
