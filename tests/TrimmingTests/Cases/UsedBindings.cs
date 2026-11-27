#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.usedbindings
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Using one binding keeps exactly what the app calls: the TextView constructor and the click-listener plumbing that Java
// calls back into, but not the binding's other members or sibling widgets. The app's own Java peer keeps only what Java
// can call (https://github.com/dotnet/android/issues/12270, fixed by https://github.com/dotnet/android/pull/12272:
// every member of every Java peer type used to be kept).

using System;
using Android.App;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Widget.TextView")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Widget.TextView", ".ctor(Android.Content.Context)")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Views.View",
	"SetOnClickListener(Android.Views.View/IOnClickListener)", "PerformClick()")]
[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Views.View/IOnClickListener")]

[assembly: RemovedMemberInAssembly ("Mono.Android", "Android.Widget.TextView",
	"SetLines(System.Int32)", "SetTextIsSelectable(System.Boolean)")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Widget.Button")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Widget.CalendarView")]

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
			var listener = new ClickListener ();
			var view = new TextView (context);
			view.SetOnClickListener (listener);
			if (!view.PerformClick () || !listener.Clicked) {
				throw new InvalidOperationException ("Java did not call the managed click listener.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[Kept]
class ClickListener : Java.Lang.Object, View.IOnClickListener
{
	public bool Clicked { get; private set; }

	[Kept]
	public void OnClick (View? v) => Clicked = true;

	[Removed]
	public void UnusedHelper () => Clicked = false;

	[Removed]
	public virtual void UnusedVirtual () => Clicked = false;
}
