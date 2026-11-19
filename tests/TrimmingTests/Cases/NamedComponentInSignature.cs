#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.namedcomponentinsignature
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/13143 (fixed by https://github.com/dotnet/android/pull/13147):
// the type map ignored the Java name given by a component attribute ([Service (Name = ...)]) when writing other Java
// callable wrappers' signatures, so javac failed on a reference to a CRC64-named class that didn't exist.

using System;
using Android.App;
using Android.Content;
using Android.OS;

[Activity (Label = "NamedComponentInSignature", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			using var holder = new ServiceHolder (new NamedService ());
			var parameterTypes = Array.ConvertAll (holder.Class.GetConstructors (), c => c.GetParameterTypes ());
			if (!Array.Exists (parameterTypes, p => p.Length == 1 && p [0].Name == "net.dot.trimmingtests.NamedService")) {
				throw new InvalidOperationException ("The Java callable wrapper has no ServiceHolder (net.dot.trimmingtests.NamedService) constructor.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

[Service (Name = "net.dot.trimmingtests.NamedService", Exported = false)]
class NamedService : Service
{
	public override IBinder? OnBind (Intent? intent) => null;
}

class ServiceHolder : Java.Lang.Object
{
	public ServiceHolder (NamedService service) => Service = service;

	public NamedService Service { get; }
}
