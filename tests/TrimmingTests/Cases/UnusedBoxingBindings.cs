#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.unusedboxingbindings
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// An app that never converts between Java boxed values and .NET primitives does not need JavaConvert or the
// Java.Lang boxing bindings. Today every app keeps them: https://github.com/dotnet/android/issues/13635

using System;
using Android.App;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Interop.JavaConvert")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Boolean")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Byte")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Character")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Short")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Integer")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Long")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Float")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Lang.Double")]

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
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
	}
}
