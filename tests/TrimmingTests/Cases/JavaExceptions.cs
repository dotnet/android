#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javaexceptions
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// A Java exception reaches managed code as its bound exception type, which the runtime activates through the type map.
// The exception the app catches is kept; exception bindings the app never sees are removed.

using System;
using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Java.Lang.NumberFormatException")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Java.Lang.NumberFormatException", ".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)")]

[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.ArrayStoreException")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Util.Zip.DataFormatException")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Database.Sqlite.SQLiteFullException")]

[Activity (Label = "JavaExceptions", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			try {
				Java.Lang.Integer.ParseInt ("not a number");
				throw new InvalidOperationException ("Integer.parseInt did not throw.");
			} catch (Java.Lang.NumberFormatException e) when (e.GetType () == typeof (Java.Lang.NumberFormatException)) {
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
