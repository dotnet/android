#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.eventimplementors
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/10911 (fixed by https://github.com/dotnet/android/pull/11345):
// the type map kept every binding event Implementor (and its Java callable wrapper) unconditionally. A C# event keeps only
// the Implementor of the listener it uses.

using System;
using Android.App;
using Android.OS;
using Android.Widget;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Views.View/IOnClickListenerImplementor")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnLongClickListenerImplementor")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnFocusChangeListenerImplementor")]

[Activity (Label = "EventImplementors", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			bool clicked = false;
			var view = new TextView (this);
			view.Click += (sender, e) => clicked = true;
			if (!view.PerformClick () || !clicked) {
				throw new InvalidOperationException ("Java did not raise the Click event.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
