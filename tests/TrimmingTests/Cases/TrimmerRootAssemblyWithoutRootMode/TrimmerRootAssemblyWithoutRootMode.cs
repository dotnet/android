#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.trimmerrootassemblywithoutrootmode
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/8775 and https://github.com/dotnet/android/issues/10758
// (fixed by https://github.com/dotnet/android/pull/10759): a user TrimmerRootAssembly item without RootMode metadata broke
// the build with MSB4096, because our targets batched over its metadata. Case.targets adds such an item. The CoreCLR
// build is covered by BuildTest.TrimmerRootAssemblyWithoutRootMode, so this case runs only with NativeAOT.

using System;
using Android.App;
using Android.OS;

[Activity (Label = "TrimmerRootAssemblyWithoutRootMode", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
	}
}
