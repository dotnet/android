#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.unusedbindings
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// An app that is only an Activity: Mono.Android bindings it never uses must be trimmed away.
// It also guards these fixed bugs:
// - https://github.com/dotnet/android/pull/11144: ILLink failed with IL1034 because the app assembly was rooted as an entry
//   point; rooting the whole app assembly would also keep UnusedHelper.
// - https://github.com/dotnet/android/pull/11982: NativeAOT apps crashed at startup because the runtime-only assembly's Java
//   callable wrapper was missing.
// - https://github.com/dotnet/android/issues/10672: NativeAOT rooted every assembly ILLink kept, so nothing was removed.
// - https://github.com/dotnet/android/issues/10911: the type map kept every binding event Implementor.

using System;
using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

// Used by the app.
[assembly: KeptTypeInAssembly ("Mono.Android", "Android.App.Activity")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.App.Activity", "OnCreate(Android.OS.Bundle)")]

// Never used by the app.
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Webkit.WebView")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Bluetooth.BluetoothAdapter")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Media.MediaPlayer")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Widget.CalendarView")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Locations.LocationManager")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Util.Zip.ZipFile")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnClickListenerInvoker")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Views.View/IOnClickListenerImplementor")]

// Android instantiates the activity through its compiler-generated default constructor.
[Activity (Label = "UnusedBindings", MainLauncher = true)]
[KeptMember (".ctor()")]
public class MainActivity : Activity
{
	[Kept]
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
	}

	[Removed]
	static void Unused ()
	{
		Console.WriteLine ("never called");
	}
}

[Removed]
class UnusedHelper
{
	public static void Run () => Console.WriteLine ("never called");
}
