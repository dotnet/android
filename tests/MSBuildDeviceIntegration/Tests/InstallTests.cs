using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] //These tests deploy to devices
	[Category ("Commercial")]
	public class InstallTests : DeviceTest
	{
		[Test]
		public void ReInstallIfUserUninstalled ([Values (false, true)] bool isRelease)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease) {
				var abis = new string [] { "armeabi-v7a", "x86" };
				proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			}
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
				Assert.AreEqual ("Success", RunAdbCommand ($"uninstall {proj.PackageName}").Trim (), $"{proj.PackageName} was not uninstalled.");
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
			}
		}

		[Test]
		public void InstallAndUnInstall ([Values (false, true)] bool isRelease)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}
			
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease) {
				var abis = new string [] { "armeabi-v7a", "x86" };
				proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			}
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				var overrideDirs = new string [] {
					$"/storage/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/mnt/shell/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/storage/sdcard/Android/data/{proj.PackageName}/files/.__override__",
				};
				var directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				if (!isRelease) {
					StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");
				}
				else {
					StringAssert.IsMatch ("", directorylist.Trim (), "fastdev directory should NOT exist for Release builds.");
				}

				Assert.IsTrue (builder.Uninstall (proj));
				Assert.AreNotEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is installed on the device.");
			}
		}

		[Test]
		public void SwitchConfigurationsShouldRedeploy ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};
			var abis = new string [] { "armeabi-v7a", "x86" };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				var overrideDirs = new string [] {
					$"/storage/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/mnt/shell/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/storage/sdcard/Android/data/{proj.PackageName}/files/.__override__",
				};
				var directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");

				proj.IsRelease = true;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
			
				directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				StringAssert.IsMatch ("", directorylist.Trim (), "fastdev directory should NOT exist for Release builds.");

				proj.IsRelease = false;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
				directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");
			
				Assert.IsTrue (builder.Uninstall (proj));
				Assert.AreNotEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is installed on the device.");
			}
		}

		[Test]
		public void InstallWithoutSharedRuntime ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty (proj.ReleaseProperties, "Optimize", false);
			proj.SetProperty (proj.ReleaseProperties, "DebugType", "none");
			proj.SetProperty (proj.ReleaseProperties, "AndroidUseSharedRuntime", false);
			proj.RemoveProperty (proj.ReleaseProperties, "EmbedAssembliesIntoApk");
			var abis = new string [] { "armeabi-v7a", "x86" };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name), false, false)) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				if (RunAdbCommand ("shell pm list packages Mono.Android.DebugRuntime").Trim ().Length != 0)
					RunAdbCommand ("uninstall Mono.Android.DebugRuntime");
				Assert.IsTrue (builder.Install (proj));
				var runtimeInfo = builder.GetSupportedRuntimes ();
				var apkPath = Path.Combine (Root, builder.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var apk = ZipHelper.OpenZip (apkPath)) {
					foreach (var abi in abis) {
						var runtime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Runtime == "debug");
						Assert.IsNotNull (runtime, "Could not find the expected runtime.");
						var inApk = ZipHelper.ReadFileFromZip (apk, String.Format ("lib/{0}/{1}", abi, runtime.Name));
						var inApkRuntime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Size == inApk.Length);
						Assert.IsNotNull (inApkRuntime, "Could not find the actual runtime used.");
						Assert.AreEqual (runtime.Size, inApkRuntime.Size, "expected {0} got {1}", "debug", inApkRuntime.Runtime);
					}
				}
				//FIXME: https://github.com/xamarin/androidtools/issues/141
				//Assert.AreEqual (0, RunAdbCommand ("shell pm list packages Mono.Android.DebugRuntime").Trim ().Length,
				//	"The Shared Runtime should not have been installed.");
				var overrideDirs = new string [] {
					$"/data/data/{proj.PackageName}/files/.__override__",
					$"/storage/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/mnt/shell/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/storage/sdcard/Android/data/{proj.PackageName}/files/.__override__",
				};
				var directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				StringAssert.Contains ($"{proj.ProjectName}.dll", directorylist, $"{proj.ProjectName}.dll should exist in the .__override__ directory.");
				StringAssert.Contains ($"System.dll", directorylist, $"System.dll should exist in the .__override__ directory.");
				StringAssert.Contains ($"Mono.Android.dll", directorylist, $"Mono.Android.dll should exist in the .__override__ directory.");

			}
		}

		[Test]
		public void InstallErrorCode ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			//Setup a situation where we get INSTALL_FAILED_NO_MATCHING_ABIS
			var abi = "armeabi-v7a";
			var proj = new XamarinAndroidApplicationProject {
				AndroidUseSharedRuntime = false,
				EmbedAssembliesIntoApk = true,
			};
			proj.SetProperty (proj.DebugProperties, KnownProperties.AndroidSupportedAbis, abi);

			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = false;
				if (!builder.Install (proj)) {
					Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "ADB0020"), "Should receive ADB0020 error code.");
				} else {
					Assert.Ignore ($"Install should have failed, but we might have an {abi} emulator attached.");
				}
			}
		}

		[Test]
		public void ToggleFastDev ()
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			var proj = new XamarinAndroidApplicationProject {
				AndroidUseSharedRuntime = true,
				EmbedAssembliesIntoApk = false,
			};

			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

				var overrideDirs = new string [] {
					$"/data/data/{proj.PackageName}/files/.__override__",
					$"/storage/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/mnt/shell/emulated/0/Android/data/{proj.PackageName}/files/.__override__",
					$"/storage/sdcard/Android/data/{proj.PackageName}/files/.__override__",
				};
				var directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}
				StringAssert.Contains ($"{proj.ProjectName}.dll", directorylist, $"{proj.ProjectName}.dll should exist in the .__override__ directory.");

				//Now toggle FastDev to OFF
				proj.AndroidUseSharedRuntime = false;
				proj.EmbedAssembliesIntoApk = true;
				var abis = new string [] { "armeabi-v7a", "x86" };
				proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));

				Assert.IsTrue (builder.Install (proj), "Second install should have succeeded.");

				directorylist = string.Empty;
				foreach (var dir in overrideDirs) {
					var listing = RunAdbCommand ($"shell ls {dir}");
					if (!listing.Contains ("No such file or directory"))
						directorylist += listing;
				}

				Assert.AreEqual ("", directorylist, "There should be no files in Fast Dev directories! Instead found: " + directorylist);

				//Deploy one last time to verify install still works without the .__override__ directory existing
				Assert.IsTrue (builder.Install (proj), "Third install should have succeeded.");
			}
		}

		[Test]
		public void BlankAdbTarget ()
		{
			if (!CommercialBuildAvailable) {
				Assert.Ignore ("Not required on Open Source Builds");
			}
			if (!HasDevices) {
				Assert.Ignore ("Test Skipped no devices or emulators found.");
			}

			var serial = GetAttachedDeviceSerial ();
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.DebugProperties, "AndroidUseSharedRuntime", true);
			proj.SetProperty (proj.DebugProperties, "EmbedAssembliesIntoApk", false);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Build (proj, parameters: new [] { $"AdbTarget=\"-e {serial}\"" });
				// Build again, no $(AdbTarget)
				b.Build (proj);
				Assert.IsTrue (b.Output.IsTargetSkipped ("_BuildApkFastDev"), "_BuildApkFastDev should be skipped!");
			}
		}
	}
}
