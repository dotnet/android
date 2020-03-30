using System;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-3")]
	[NonParallelizable] // Do not run environment modifying tests in parallel.
	public class AndroidDependenciesTests : BaseTest
	{
		[Test]
		public void InstallAndroidDependenciesTest ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");
			var old = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			try {
				string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", sdkPath);
				var proj = new XamarinAndroidApplicationProject ();
				using (var b = CreateApkBuilder ()) {
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					Assert.IsTrue (b.Build (proj, parameters: new string [] { "AcceptAndroidSDKLicenses=true" }), "InstallAndroidDependencies should have succeeded.");
					b.Target = defaultTarget;
					Assert.IsTrue (b.Build (proj), "build should have succeeded.");
				}
			} finally {
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", old);
			}
		}
	}
}
