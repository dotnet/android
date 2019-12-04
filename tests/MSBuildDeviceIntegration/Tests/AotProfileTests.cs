using NUnit.Framework;
using System.IO;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class AotProfileTests : DeviceTest
	{

		[Test]
		public void BuildBasicApplicationAndAotProfileIt ()
		{
			if (!HasDevices)
				Assert.Ignore ("Skipping test. No devices available.");

			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			var projDirectory = Path.Combine ("temp", TestName);
			using (var b = CreateApkBuilder (projDirectory)) {
				Assert.IsTrue (b.RunTarget (proj, "BuildAndStartAotProfiling"), "Run of BuildAndStartAotProfiling should have succeeded.");
				System.Threading.Thread.Sleep (5000);
				b.BuildLogFile = "build2.log";
				Assert.IsTrue (b.RunTarget (proj, "FinishAotProfiling", doNotCleanupOnUpdate: true), "Run of FinishAotProfiling should have succeeded.");
				var customProfile = Path.Combine (Root, projDirectory, "custom.aprof");
				FileAssert.Exists (customProfile);
			}
		}
	}
}
