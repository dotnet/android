#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.compatibilitymethodlistener
#:property SupportedOSPlatformVersion=24
#:package Xamarin.GooglePlayServices.Ads.Lite@123.4.0
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13235 (fixed by https://github.com/dotnet/android/pull/13242):
// the binding of com.google.android.gms.ads.mediation.MediationBannerListener registers a compatibility method (zza) that the
// Java interface doesn't declare, and the type map's Java callable wrappers marked it @Override, so javac failed with
// XAJVC0000 for the package's own Implementor and for app listeners like this one.

using System;
using Android.App;
using Android.Gms.Ads;
using Android.Gms.Ads.Mediation;
using Android.Runtime;

// The Ads SDK's content provider refuses to start without an application ID; this is Google's sample ID.
[assembly: MetaData ("com.google.android.gms.ads.APPLICATION_ID", Value = "ca-app-pub-3940256099942544~3347511713")]

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
			using var listener = new BannerListener ();
			if (!listener.Class.Name.EndsWith (".BannerListener", StringComparison.Ordinal)) {
				throw new InvalidOperationException ($"The listener's Java class is {listener.Class.Name}.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

#pragma warning disable CS0618 // Google marks the mediation API obsolete.
class BannerListener : Java.Lang.Object, IMediationBannerListener
{
	public void OnAdClicked (IMediationBannerAdapter? adapter) { }
	public void OnAdClosed (IMediationBannerAdapter? adapter) { }
	public void OnAdFailedToLoad (IMediationBannerAdapter? adapter, int errorCode) { }
	public void OnAdFailedToLoad2 (IMediationBannerAdapter? p0, AdError? errorCode) { }
	public void OnAdLeftApplication (IMediationBannerAdapter? adapter) { }
	public void OnAdLoaded (IMediationBannerAdapter? adapter) { }
	public void OnAdOpened (IMediationBannerAdapter? adapter) { }
	public void Zzb (IMediationBannerAdapter? p0, string? p1, string? p2) { }
}
#pragma warning restore CS0618
