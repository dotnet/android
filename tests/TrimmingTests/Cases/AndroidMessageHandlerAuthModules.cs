#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.androidmessagehandlerauthmodules
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/12791: AndroidMessageHandler created its Basic and Digest
// authentication modules eagerly, so every app using it kept them. They are only needed once PreAuthenticationData is set.

using System;
using System.Net.Http;
using Android.App;
using Android.OS;
using Xamarin.Android.Net;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Xamarin.Android.Net.AndroidMessageHandler")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Xamarin.Android.Net.AuthModuleBasic")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Xamarin.Android.Net.AuthModuleDigest")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Xamarin.Android.Net.AuthDigestSession")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Xamarin.Android.Net.AuthDigestHeaderParser")]

[Activity (Label = "AndroidMessageHandlerAuthModules", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
#pragma warning disable CS0618 // AndroidMessageHandler is obsolete, but apps still use it.
			using var client = new HttpClient (new AndroidMessageHandler ());
#pragma warning restore CS0618
			if (client.Timeout <= TimeSpan.Zero) {
				throw new InvalidOperationException ($"Unexpected timeout {client.Timeout}.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
