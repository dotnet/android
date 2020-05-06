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
		static readonly string SdkVersion = Assembly.GetAssembly (typeof(XASdkProject))
			.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Where (attr => attr.Key == "SdkVersion")
			.Select (attr => attr.Value)
			.FirstOrDefault () ?? "0.0.1";

		[Test]
		public void DotNetInstallAndRun ([Values (false, true)] bool isRelease)
		{
			if (!HasDevices)
				Assert.Ignore ("Skipping Test. No devices available.");

			var proj = new XASdkProject (SdkVersion) {
				IsRelease = isRelease
			};
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
