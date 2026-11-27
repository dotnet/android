#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.boundreturntypeconstructors
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/10448 (MAUI .NET 10 size regression,
// https://github.com/dotnet/android/issues/10550): bindings return objects through Java.Lang.Object.GetObject<T> (), and
// [DynamicallyAccessedMembers (Constructors)] on its T kept every constructor of every bound return type. Reading
// Context.CacheDir needs only the activation constructor of Java.IO.File. #11801 re-added the annotation:
// https://github.com/dotnet/android/issues/13649

using System;
using Android.App;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptMemberInAssembly ("Mono.Android", "Java.IO.File",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "get_AbsolutePath()")]
[assembly: RemovedMemberInAssembly ("Mono.Android", "Java.IO.File",
	".ctor(Java.IO.File,System.String)", ".ctor(System.String,System.String)", ".ctor(Java.Net.URI)", ".ctor(System.String)")]

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
			if (string.IsNullOrEmpty (CacheDir?.AbsolutePath)) {
				throw new InvalidOperationException ("CacheDir has no path.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
