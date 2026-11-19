#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.customapplication
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for:
// - https://github.com/dotnet/android/issues/8914 (fixed by https://github.com/dotnet/android/pull/8936): an [Application]
//   with TrimMode=full broke the build, and its BackupAgent and ManageSpaceActivity types had to be kept by hand.
// - https://github.com/dotnet/android/issues/13175 (fixed by https://github.com/dotnet/android/pull/13210): the trimmable
//   type map dropped [MetaData] on the Application from the manifest, and full trimming could remove types that only the
//   generated manifest names.
// Android creates the application, the backup agent and the manage-space activity; the app never references them.

using System;
using Android.App;
using Android.App.Backup;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[Activity (Label = "CustomApplication", MainLauncher = true)]
public class MainActivity : Activity
{
	const string MetaDataKey = "net.dot.trimmingtests.value";

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			if (!State.ApplicationCreated) {
				throw new InvalidOperationException ($"Android did not run the custom application's OnCreate (); the application is {Application?.Class.Name}.");
			}
			var packageManager = PackageManager ?? throw new InvalidOperationException ("No PackageManager.");
			var packageName = PackageName ?? throw new InvalidOperationException ("The activity has no package name.");
			var info = OperatingSystem.IsAndroidVersionAtLeast (33)
				? packageManager.GetApplicationInfo (packageName, PackageManager.ApplicationInfoFlags.Of ((long) PackageInfoFlags.MetaData))
				: packageManager.GetApplicationInfo (packageName, PackageInfoFlags.MetaData);
			var value = info.MetaData?.GetString (MetaDataKey);
			if (value != "present") {
				throw new InvalidOperationException ($"The {MetaDataKey} meta-data is '{value}'.");
			}
			AssertActivatedFromJava (info.BackupAgentName, "TestBackupAgent");
			AssertActivatedFromJava (info.ManageSpaceActivityName, "TestManageSpaceActivity");
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	void AssertActivatedFromJava (string? javaName, string expectedTypeName)
	{
		if (javaName is null) {
			throw new InvalidOperationException ($"The manifest does not name the {expectedTypeName} class.");
		}
		var instance = Java.Lang.Class.ForName (javaName, true, ClassLoader).GetConstructor ()?.NewInstance ();
		if (instance?.GetType ().Name != expectedTypeName) {
			throw new InvalidOperationException ($"new {javaName} () was activated as {instance?.GetType ()}.");
		}
	}
}

static class State
{
	public static bool ApplicationCreated;
}

[Application (Label = "CustomApplication", AllowBackup = true, BackupAgent = typeof (TestBackupAgent), ManageSpaceActivity = typeof (TestManageSpaceActivity))]
[MetaData ("net.dot.trimmingtests.value", Value = "present")]
[Kept]
[KeptMember (".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)")]
public class TestApplication : Application
{
	public TestApplication (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	[Kept]
	public override void OnCreate ()
	{
		base.OnCreate ();
		State.ApplicationCreated = true;
	}
}

[Kept]
[KeptMember (".ctor()")]
public class TestBackupAgent : BackupAgent
{
	public override void OnBackup (ParcelFileDescriptor? oldState, BackupDataOutput? data, ParcelFileDescriptor? newState)
	{
	}

	public override void OnRestore (BackupDataInput? data, int appVersionCode, ParcelFileDescriptor? newState)
	{
	}
}

[Activity]
[Kept]
[KeptMember (".ctor()")]
public class TestManageSpaceActivity : Activity
{
}
