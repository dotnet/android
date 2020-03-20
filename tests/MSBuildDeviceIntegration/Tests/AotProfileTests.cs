using NUnit.Framework;
using System;
using System.IO;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class AotProfileTests : DeviceTest
	{

		readonly string PermissionManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{0}"">
	<uses-sdk />
	<application android:label=""{0}"">
	</application>
	<uses-permission android:name=""android.permission.INTERNET"" />
</manifest>";

		[Test]
		public void BuildBasicApplicationAndAotProfileIt ()
		{
			if (!HasDevices)
				Assert.Ignore ("Skipping test. No devices available.");

			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			var port = 9000 + new Random ().Next (1000);
			proj.SetProperty ("AndroidAotProfilerPort", port.ToString ());
			proj.AndroidManifest = string.Format (PermissionManifest, proj.PackageName);
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
