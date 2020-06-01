using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[NonParallelizable]
	public class XASdkDeployTests : DeviceTest
	{
		[Test]
		public void DotNetInstallAndRun ([Values (false, true)] bool isRelease, [Values (false, true)] bool xamarinForms)
		{
			if (!HasDevices)
				Assert.Ignore ("Skipping Test. No devices available.");

			XASdkProject proj;
			if (xamarinForms) {
				proj = new XamarinFormsXASdkProject {
					IsRelease = isRelease
				};
			} else {
				proj = new XASdkProject {
					IsRelease = isRelease
				};
			}
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, DeviceAbi);
			proj.SetRuntimeIdentifier (DeviceAbi);

			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir     = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));

			Assert.IsTrue (dotnet.Run (), "`dotnet run` should succeed");
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (fullProjDir, "logcat.log"), 30);
			RunAdbCommand ($"uninstall {proj.PackageName}");
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}
	}
}
