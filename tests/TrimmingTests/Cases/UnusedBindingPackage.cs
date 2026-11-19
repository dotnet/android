#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.unusedbindingpackage
#:property SupportedOSPlatformVersion=24
#:package GoogleGson@2.14.0.1
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/12045: when trimming removed every type of a referenced binding
// package, its per-assembly type map was removed too, but the root type map still named it, so the app crashed at startup
// with FileNotFoundException for '_GoogleGson.TypeMap'. The app references GoogleGson but never uses it.

using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: RemovedTypeInAssembly ("GoogleGson", "GoogleGson.Gson")]

[Activity (Label = "UnusedBindingPackage", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
	}
}
