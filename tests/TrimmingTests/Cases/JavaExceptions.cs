#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javaexceptions
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// A Java exception reaches managed code as its bound exception type, which the runtime activates through the type map.
// The exception the app catches is kept; exception bindings the app never sees are removed.

using System;
using Android.App;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Java.Lang.NumberFormatException")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Java.Lang.NumberFormatException", ".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)")]

[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.ArrayStoreException")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Util.Zip.DataFormatException")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Database.Sqlite.SQLiteFullException")]

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
