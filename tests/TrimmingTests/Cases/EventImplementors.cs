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
using Android.Runtime;
using Android.Widget;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Views.View/IOnClickListenerImplementor")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnLongClickListenerImplementor")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnFocusChangeListenerImplementor")]

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
			bool clicked = false;
			var view = new TextView (context);
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
