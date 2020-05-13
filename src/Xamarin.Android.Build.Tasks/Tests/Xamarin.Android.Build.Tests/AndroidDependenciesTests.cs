using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			var path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");
			var old = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			try {
				string sdkPath = Path.Combine (path, "android-sdk");
				CreateFauxAndroidSdkDirectory (sdkPath, buildToolsVersion: "23.0.0");
				// Provide mock version info so the `tools` component won't be downloaded.
				File.WriteAllText (Path.Combine (sdkPath, "tools", "source.properties"), "Pkg.Revision=99.99.99");
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", sdkPath);
				var proj = new XamarinAndroidApplicationProject ();
				// Use `BuildHelper.CreateApkBuilder()` instead of `BaseTest.CreateApkBuilder()` so
				// `android-sdk` can be placed beside the project directory rather than within it.
				using (var b = BuildHelper.CreateApkBuilder (Path.Combine (path, "Project"))) {
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					b.ThrowOnBuildFailure = false;
					Assert.IsFalse (b.Build (proj, parameters: new string [] { "AcceptAndroidSDKLicenses=false" }), "InstallAndroidDependencies should have failed.");
					IEnumerable<string> taskOutput = b.LastBuildOutput
						.SkipWhile (x => !(x.StartsWith ("  Detecting Android SDK in") && x.Contains (sdkPath)))
						.TakeWhile (x => !x.StartsWith ("Done executing task \"InstallAndroidDependencies\""))
						.Where (x => x.StartsWith ("  Dependency to be installed:"));
					Assert.AreEqual (2, taskOutput.Count (), "Exactly two dependencies should be identified to be installed.");
					StringAssertEx.Contains ("Dependency to be installed: Android SDK Platform", taskOutput);
					StringAssertEx.Contains ("Dependency to be installed: Android SDK Build-Tools", taskOutput);
					b.ThrowOnBuildFailure = true;
					Assert.IsTrue (b.Build (proj, parameters: new string [] { "AcceptAndroidSDKLicenses=true" }), "InstallAndroidDependencies should have succeeded.");
					Assert.IsTrue (Directory.Exists (Path.Combine (sdkPath, "platforms")), "At least one platform should have been installed");
					Assert.IsTrue (Directory.Exists (Path.Combine (sdkPath, "build-tools")), "At least one Build Tools version should have been installed");
					b.Target = defaultTarget;
					Assert.IsTrue (b.Build (proj), "build should have succeeded.");
					taskOutput = b.LastBuildOutput
						.SkipWhile (x => !x.StartsWith ("  ResolveSdks Outputs:"))
						.TakeWhile (x => !x.StartsWith ("Done executing task \"ResolveSdks\""));
					StringAssert.Contains (sdkPath, taskOutput.First (x => x.StartsWith ("    AndroidSdkPath:")));
				}
			} finally {
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", old);
			}
		}
	}
}
