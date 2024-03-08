using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("UsesDevice"), Category ("AOT"), Category ("ProfiledAOT")]
	public class AotProfileTests : DeviceTest
	{
		[TearDown]
		protected void ClearProp ()
		{
			ClearShellProp ("debug.mono.profile");
		}

		readonly string PermissionManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{0}"">
	<uses-sdk />
	<application android:label=""{0}"">
	</application>
	<uses-permission android:name=""android.permission.INTERNET"" />
</manifest>";

		[Test]
		[NonParallelizable]
		public void BuildBasicApplicationAndAotProfileIt ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = false,
			};
			proj.SetAndroidSupportedAbis (DeviceAbi);

			// TODO: only needed in .NET 6+
			// See https://github.com/dotnet/runtime/issues/56989
			proj.PackageReferences.Add (KnownPackages.Mono_AotProfiler_Android);

			var port = 9000 + new Random ().Next (1000);
			proj.SetProperty ("AndroidAotProfilerPort", port.ToString ());
			proj.AndroidManifest = string.Format (PermissionManifest, proj.PackageName);
			var projDirectory = Path.Combine ("temp", TestName);
			using (var b = CreateApkBuilder (projDirectory)) {
				Assert.IsTrue (b.RunTarget (proj, "BuildAndStartAotProfiling"), "Run of BuildAndStartAotProfiling should have succeeded.");
				WaitForAppBuiltForOlderAndroidWarning (proj.PackageName, Path.Combine (Root, b.ProjectDirectory, "oldsdk-logcat.log"));
				System.Threading.Thread.Sleep (5000);
				b.BuildLogFile = "build2.log";

				// Need execute permission
				if (!IsWindows) {
					var aprofutil = Path.Combine (Root, b.ProjectDirectory, "aprofutil");
					RunProcess ("chmod", $"u+x {aprofutil}");
				}

				Assert.IsTrue (b.RunTarget (proj, "FinishAotProfiling", doNotCleanupOnUpdate: true), "Run of FinishAotProfiling should have succeeded.");
				var customProfile = Path.Combine (Root, projDirectory, "custom.aprof");
				FileAssert.Exists (customProfile);
			}
		}
	}
}
