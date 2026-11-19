#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.interfacethroughtrimmedsuperclass
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/11386 (fixed by https://github.com/dotnet/android/pull/11345):
// the trust manager's Java class extends javax.net.ssl.X509ExtendedTrustManager, whose binding is trimmed, so walking
// only the Java superclasses found no type map entry and activated it as ITrustManagerInvoker instead of
// IX509TrustManagerInvoker. The runtime must also walk the Java interfaces.
// Don't use HttpClient/AndroidMessageHandler here: they keep the trust manager invokers themselves.

using System;
using Android.App;
using Android.OS;
using Javax.Net.Ssl;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Javax.Net.Ssl.IX509TrustManagerInvoker")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Javax.Net.Ssl.X509ExtendedTrustManager")]

[Activity (Label = "InterfaceThroughTrimmedSuperclass", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var factory = TrustManagerFactory.GetInstance (TrustManagerFactory.DefaultAlgorithm)
				?? throw new InvalidOperationException ("No TrustManagerFactory.");
			factory.Init ((Java.Security.KeyStore?) null);
			var trustManagers = factory.GetTrustManagers () ?? throw new InvalidOperationException ("No trust managers.");
			if (trustManagers [0] is not IX509TrustManager trustManager) {
				throw new InvalidOperationException ($"The trust manager was activated as {trustManagers [0].GetType ()}.");
			}
			if (trustManager.GetAcceptedIssuers () is not { Length: > 0 }) {
				throw new InvalidOperationException ("The trust manager accepts no issuers.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
