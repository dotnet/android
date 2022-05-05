using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("UsesDevice"), Category ("AOT"), Category ("ProfiledAOT"), Category ("Node-3")]
	// TODO: either we get .NET 7 support for https://github.com/dotnet/runtime/issues/56989
	// Or update for .NET 7: https://github.com/jonathanpeppers/Mono.Profiler.Android
	[Category ("DotNetIgnore")]
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
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			AddDotNetProfilerNativeLibraries (proj);
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
				if (Builder.UseDotNet && !IsWindows) {
					var aprofutil = Path.Combine (Root, b.ProjectDirectory, "aprofutil");
					RunProcess ("chmod", $"u+x {aprofutil}");
				}

				Assert.IsTrue (b.RunTarget (proj, "FinishAotProfiling", doNotCleanupOnUpdate: true), "Run of FinishAotProfiling should have succeeded.");
				var customProfile = Path.Combine (Root, projDirectory, "custom.aprof");
				FileAssert.Exists (customProfile);
			}
		}

		void AddDotNetProfilerNativeLibraries (XamarinAndroidApplicationProject proj)
		{
			// TODO: only needed in .NET 6+
			// See https://github.com/dotnet/runtime/issues/56989
			if (!Builder.UseDotNet)
				return;

			// Files are built from dotnet/runtime & stored at:
			const string github = "https://github.com/jonathanpeppers/android-profiled-aot";

			proj.Sources.Add (new BuildItem ("None", "aprofutil") {
				WebContent = $"{github}/raw/main/binaries/aprofutil"
			});
			proj.Sources.Add (new BuildItem ("None", "aprofutil.exe") {
				WebContent = $"{github}/raw/main/binaries/aprofutil.exe"
			});
			proj.Sources.Add (new BuildItem ("None", "Mono.Profiler.Log.dll") {
				WebContent = $"{github}/raw/main/binaries/Mono.Profiler.Log.dll"
			});
			proj.SetProperty ("AProfUtilToolPath", "$(MSBuildThisFileDirectory)");

			foreach (var rid in proj.GetProperty (KnownProperties.RuntimeIdentifiers).Split (';')) {
				//NOTE: each rid has the same file name, so using WebClient directly
				var bytes = new WebClient ().DownloadData ($"{github}/raw/main/binaries/{rid}/libmono-profiler-aot.so");
				proj.Sources.Add (new AndroidItem.AndroidNativeLibrary ($"{rid}\\libmono-profiler-aot.so") {
					BinaryContent = () => bytes,
				});
			}
		}
	}
}
