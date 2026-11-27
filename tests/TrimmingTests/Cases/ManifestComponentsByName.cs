#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.manifestcomponentsbyname
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/8940 (fixed by https://github.com/dotnet/android/pull/9099,
// and on the trimmable type map by https://github.com/dotnet/android/pull/12498): with TrimMode=full, a [Service] that only
// the generated AndroidManifest.xml references was trimmed, so Firebase push notifications silently stopped arriving.
// Here Android creates a receiver and a service that the app refers to only by their Java names.

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

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
			// The receiver starts the service, and the service reports the result.
			var packageName = context.PackageName ?? throw new InvalidOperationException ("The application has no package name.");
			context.SendBroadcast (new Intent ().SetClassName (packageName, "net.dot.trimmingtests.ByNameReceiver"));
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[BroadcastReceiver (Name = "net.dot.trimmingtests.ByNameReceiver", Exported = false)]
[Kept]
[KeptMember (".ctor()")]
class ByNameReceiver : BroadcastReceiver
{
	[Kept]
	public override void OnReceive (Context? context, Intent? intent)
	{
		if (context?.PackageName is not { } packageName) {
			Android.Util.Log.Error ("TrimmingTests", "TRIMMING_TEST_RESULT: FAIL The receiver got no context package name.");
			return;
		}
		context.StartService (new Intent ().SetClassName (packageName, "net.dot.trimmingtests.ByNameService"));
	}
}

[Service (Name = "net.dot.trimmingtests.ByNameService", Exported = false)]
[Kept]
[KeptMember (".ctor()")]
class ByNameService : Service
{
	public override IBinder? OnBind (Intent? intent) => null;

	[Kept]
	public override StartCommandResult OnStartCommand (Intent? intent, StartCommandFlags flags, int startId)
	{
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		StopSelf ();
		return StartCommandResult.NotSticky;
	}
}
