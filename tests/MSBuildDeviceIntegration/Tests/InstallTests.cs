using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] //These tests deploy to devices
	[Category ("Commercial"), Category ("UsesDevice")]
	public class InstallTests : DeviceTest
	{
		static byte [] GetKeystore ()
		{
			var assembly = typeof (XamarinAndroidCommonProject).Assembly;
			using (var stream = assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.test.keystore")) {
				var data = new byte [stream.Length];
				stream.Read (data, 0, (int) stream.Length);
				return data;
			}
		}

		string GetContentFromAllOverrideDirectories (string packageName, bool useRunAsCommand = true)
		{
			var adbShellArgs = $"shell run-as {packageName} ls";

			var directorylist = string.Empty;
			foreach (var dir in GetOverrideDirectoryPaths (packageName)) {
				var listing = RunAdbCommand ($"{adbShellArgs} {dir}");
				if (!listing.Contains ("No such file or directory") && !listing.Contains ("Permission denied"))
					directorylist += $"{listing} ";
			}
			return directorylist;
		}

		[Test]
		public void ReInstallIfUserUninstalled ([Values (false, true)] bool isRelease)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			}
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.IsTrue (builder.Output.AreTargetsAllBuilt ("_Upload"), "_Upload should have built completely.");
				Assert.IsTrue (builder.Install (proj));
				Assert.IsTrue (builder.Output.AreTargetsAllSkipped ("_Upload"), "_Upload should have been skipped.");
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
				Assert.AreEqual ("Success", RunAdbCommand ($"uninstall {proj.PackageName}").Trim (), $"{proj.PackageName} was not uninstalled.");
				Assert.IsTrue (builder.Install (proj));
				Assert.IsTrue (builder.Output.AreTargetsAllBuilt ("_Upload"), "_Upload should have built completely.");
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");
				Assert.IsTrue (builder.Uninstall (proj), "unnstall should have succeeded.");
			}
		}

		[Test]
		public void InstallAndUnInstall ([Values (false, true)] bool isRelease)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease) {
				// Set debuggable=true to allow run-as command usage with a release build
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			}
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				var directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				if (!isRelease) {
					StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");
				}
				else {
					Assert.AreEqual ("", directorylist.Trim (), "fastdev directory should NOT exist for Release builds.");
				}

				Assert.IsTrue (builder.Uninstall (proj));
				Assert.AreNotEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is installed on the device.");
			}
		}

		[Test]
		public void ChangeKeystoreRedeploy ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				PackageName = "com.xamarin.keytest"
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			using (var builder = CreateApkBuilder ()) {
				// Use the default debug.keystore XA generates
				Assert.IsTrue (builder.Install (proj), "first install should succeed.");
				byte [] data = GetKeystore ();
				proj.OtherBuildItems.Add (new BuildItem (BuildActions.None, "test.keystore") {
					BinaryContent = () => data
				});

				string password = "android";
				proj.SetProperty ("AndroidSigningStorePass", password);
				proj.SetProperty ("AndroidSigningKeyPass", password);
				proj.SetProperty ("AndroidKeyStore", "True");
				proj.SetProperty ("AndroidSigningKeyStore", "test.keystore");
				proj.SetProperty ("AndroidSigningKeyAlias", "mykey");

				Assert.IsTrue (builder.Install (proj), "second install should succeed.");
				Assert.IsTrue (builder.Uninstall (proj), "unnstall should have succeeded.");
			}
		}

		[Test]
		public void SwitchConfigurationsShouldRedeploy ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
			};
			// Set debuggable=true to allow run-as command usage with a release build
			proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				var directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");

				proj.IsRelease = true;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				Assert.AreEqual ("", directorylist.Trim (), "fastdev directory should NOT exist for Release builds.");

				proj.IsRelease = false;
				Assert.IsTrue (builder.Build (proj));
				Assert.IsTrue (builder.Install (proj));
				Assert.AreEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is not installed on the device.");

				directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				StringAssert.Contains ($"{proj.AssemblyName}", directorylist, $"{proj.AssemblyName} not found in fastdev directory.");

				Assert.IsTrue (builder.Uninstall (proj));
				Assert.AreNotEqual ($"package:{proj.PackageName}", RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim (),
					$"{proj.PackageName} is installed on the device.");
			}
		}

		[Test]
		public void InstallWithoutSharedRuntime ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty (proj.ReleaseProperties, "Optimize", false);
			proj.SetProperty (proj.ReleaseProperties, "DebugType", "none");
			if (Builder.UseDotNet) {
				// NOTE: in .NET 6, EmbedAssembliesIntoApk=true by default for Release builds
				proj.SetProperty (proj.ReleaseProperties, "EmbedAssembliesIntoApk", "false");
			} else {
				proj.RemoveProperty (proj.ReleaseProperties, "EmbedAssembliesIntoApk");
			}
			var abis = new [] { "armeabi-v7a", "x86" };
			proj.SetAndroidSupportedAbis (abis);
			using (var builder = CreateApkBuilder ()) {
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
				var directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				StringAssert.Contains ($"{proj.ProjectName}.dll", directorylist, $"{proj.ProjectName}.dll should exist in the .__override__ directory.");
				if (Builder.UseDotNet)
					StringAssert.Contains ($"System.Private.CoreLib.dll", directorylist, $"System.Private.CoreLib.dll should exist in the .__override__ directory.");
				else
					StringAssert.Contains ($"System.dll", directorylist, $"System.dll should exist in the .__override__ directory.");

				StringAssert.Contains ($"Mono.Android.dll", directorylist, $"Mono.Android.dll should exist in the .__override__ directory.");
				Assert.IsTrue (builder.Uninstall (proj), "unnstall should have succeeded.");
			}
		}

		[Test]
		public void InstallErrorCode ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			//Setup a situation where we get INSTALL_FAILED_NO_MATCHING_ABIS
			var abi = "armeabi-v7a";
			var proj = new XamarinAndroidApplicationProject {
				EmbedAssembliesIntoApk = true,
			};
			proj.SetAndroidSupportedAbis (abi);

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
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject {
				EmbedAssembliesIntoApk = false,
				OtherBuildItems = {
					new BuildItem.NoActionResource ("UnnamedProject.dll.config") {
						TextContent = () => "<?xml version='1.0' ?><configuration/>",
						Metadata = {
							{ "CopyToOutputDirectory", "PreserveNewest" },
						}
					}
				}
			};

			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				var directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				StringAssert.Contains ($"{proj.ProjectName}.dll", directorylist, $"{proj.ProjectName}.dll should exist in the .__override__ directory.");
				// Configuration files are not supported in One .NET, we don't need to make sure that they're fast deployed.
				if (!Builder.UseDotNet) {
					StringAssert.Contains ($"{proj.ProjectName}.dll.config", directorylist, $"{proj.ProjectName}.dll.config should exist in the .__override__ directory.");
				}

				//Now toggle FastDev to OFF
				proj.EmbedAssembliesIntoApk = true;
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");

				Assert.IsTrue (builder.Install (proj), "Second install should have succeeded.");

				directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				Assert.AreEqual ("", directorylist, "There should be no files in Fast Dev directories! Instead found: " + directorylist);

				//Deploy one last time to verify install still works without the .__override__ directory existing
				Assert.IsTrue (builder.Install (proj), "Third install should have succeeded.");
				Assert.IsTrue (builder.Uninstall (proj), "unnstall should have succeeded.");
			}
		}

		[Test]
		public void LoggingPropsShouldCreateOverrideDirForRelease ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			// Set debuggable=true to allow run-as command usage with a release build
			proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");

			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				RunAdbCommand ("shell setprop debug.mono.log timing");
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
				var didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity", Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
				RunAdbCommand ("shell setprop debug.mono.log \"\"");
				Assert.True (didLaunch, "Activity should have started.");
				var directorylist = GetContentFromAllOverrideDirectories (proj.PackageName);
				builder.Uninstall (proj);
				StringAssert.Contains ("counters.txt", directorylist, $"counters.txt did not exist in the .__override__ directory.\nFound:{directorylist}");
			}
		}

		[Test]
		public void BlankAdbTarget ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var serial = GetAttachedDeviceSerial ();
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.DebugProperties, "EmbedAssembliesIntoApk", false);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Build (proj, parameters: new [] { $"AdbTarget=\"-e {serial}\"" });
				// Build again, no $(AdbTarget)
				b.Build (proj);
				Assert.IsTrue (b.Output.IsTargetSkipped ("_BuildApkFastDev"), "_BuildApkFastDev should be skipped!");
			}
		}

#pragma warning disable 414
		static object [] AndroidStoreKeyTests = new object [] {
				// useApkSigner, isRelease, PackageFormat, AndroidKeyStore, password, ExpectedResult, ShouldInstall
			new object[] { true,  false , "apk"     , "False"        , "android",      "debug.keystore"         , true},
			new object[] { true,  true  , "apk"     , "False"        , "android",      "debug.keystore"         , true},
			new object[] { true,  false , "apk"     , "True"         , "android",      "--ks test.keystore"     , true},
			new object[] { true,  true  , "apk"     , "True"         , "android",      "--ks test.keystore"     , true},
			new object[] { true,  false , "apk"     , ""             , "android",      "debug.keystore"         , true},
			new object[] { true,  true  , "apk"     , ""             , "android",      "debug.keystore"         , true},
			new object[] { true,  false , "apk"     , "True"         , "env:android",  "--ks test.keystore"     , true},
			new object[] { true,  true  , "apk"     , "True"         , "env:android",  "--ks test.keystore"     , true},
			new object[] { true,  false , "apk"     , "True"         , "file:android", "--ks test.keystore"     , true},
			new object[] { true,  true  , "apk"     , "True"         , "file:android", "--ks test.keystore"     , true},
			// dont use apksigner
			new object[] { false, false , "apk"     , "False"        , "android",      "debug.keystore"         , true},
			new object[] { false, true  , "apk"     , "False"        , "android",      "debug.keystore"         , true},
			new object[] { false, false , "apk"     , "True"         , "android",      "-keystore test.keystore", true},
			new object[] { false, true  , "apk"     , "True"         , "android",      "-keystore test.keystore", true},
			new object[] { false, false , "apk"     , ""             , "android",      "debug.keystore"         , true},
			new object[] { false, true  , "apk"     , ""             , "android",      "debug.keystore"         , true},
			new object[] { false, false , "apk"     , "True"         , "env:android",  "-keystore test.keystore", true},
			new object[] { false, true  , "apk"     , "True"         , "env:android",  "-keystore test.keystore", true},
			new object[] { false, false , "apk"     , "True"         , "file:android", "-keystore test.keystore", true},
			new object[] { false, true  , "apk"     , "True"         , "file:android", "-keystore test.keystore", true},
			// aab signing tests
			new object[] { true,  true  , "aab"     , "True"         , "android",      "-ks test.keystore"      , true},
			new object[] { true,  true  , "aab"     , "True"         , "file:android", "-ks test.keystore"      , true},
			new object[] { true,  true  , "aab"     , "True"         , "env:android",  "-ks test.keystore"      , false},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (AndroidStoreKeyTests))]
		public void TestAndroidStoreKey (bool useApkSigner, bool isRelease, string packageFormat, string androidKeyStore, string password, string expected, bool shouldInstall)
		{
			AssertHasDevices ();
			if (DeviceSdkVersion >= 30 && !useApkSigner && packageFormat == "apk") {
				Assert.Ignore ($"Test Skipped. jarsigner and {packageFormat} does not work with API 30 and above");
				return;
			}

			string path = Path.Combine ("temp", TestName.Replace (expected, expected.Replace ("-", "_")));
			string storepassfile = Path.Combine (Root, path, "storepass.txt");
			string keypassfile = Path.Combine (Root, path, "keypass.txt");
			byte [] data = GetKeystore ();
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease
			};
			Dictionary<string, string> envVar = new Dictionary<string, string> ();
			if (password.StartsWith ("env:", StringComparison.Ordinal)) {
				envVar.Add ("_MYPASSWORD", password.Replace ("env:", string.Empty));
				proj.SetProperty ("AndroidSigningStorePass", "env:_MYPASSWORD");
				proj.SetProperty ("AndroidSigningKeyPass", "env:_MYPASSWORD");
			} else if (password.StartsWith ("file:", StringComparison.Ordinal)) {
				proj.SetProperty ("AndroidSigningStorePass", $"file:{storepassfile}");
				proj.SetProperty ("AndroidSigningKeyPass", $"file:{keypassfile}");
			} else {
				proj.SetProperty ("AndroidSigningStorePass", password);
				proj.SetProperty ("AndroidSigningKeyPass", password);
			}
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			proj.SetProperty ("AndroidKeyStore", androidKeyStore);
			proj.SetProperty ("AndroidSigningKeyStore", "test.keystore");
			proj.SetProperty ("AndroidSigningKeyAlias", "mykey");
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
			proj.SetProperty ("AndroidUseApkSigner", useApkSigner.ToString ());
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.None, "test.keystore") {
				BinaryContent = () => data
			});
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.None, "storepass.txt") {
				TextContent = () => password.Replace ("file:", string.Empty),
				Encoding = Encoding.ASCII,
			});
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.None, "keypass.txt") {
				TextContent = () => password.Replace ("file:", string.Empty),
				Encoding = Encoding.ASCII,
			});
			using (var b = CreateApkBuilder (path, false, false)) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj, environmentVariables: envVar), "Build should have succeeded.");
				if (packageFormat == "apk") {
					StringAssertEx.Contains (expected, b.LastBuildOutput,
					"The Wrong keystore was used to sign the apk");
				}
				b.BuildLogFile = "install.log";
				Assert.AreEqual (shouldInstall, b.Install (proj, doNotCleanupOnUpdate: true), $"Install should have {(shouldInstall ? "succeeded" : "failed")}.");
				if (packageFormat == "aab") {
					StringAssertEx.Contains (expected, b.LastBuildOutput,
						"The Wrong keystore was used to sign the apk");
				}
				if (!shouldInstall)
					return;
				b.BuildLogFile = "uninstall.log";
				Assert.IsTrue (b.Uninstall (proj, doNotCleanupOnUpdate: true), "Uninstall should have succeeded.");
			}
		}

		// https://xamarin.github.io/bugzilla-archives/31/31705/bug.html
		[Test]
		public void LocalizedAssemblies_ShouldBeFastDeployed ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject {
				EmbedAssembliesIntoApk = false,
				OtherBuildItems = {
					new BuildItem ("EmbeddedResource", "Foo.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
					new BuildItem ("EmbeddedResource", "Foo.es.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancelar</value></data>")
					}
				}
			};

			using (var builder = CreateApkBuilder ()) {
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				var projectOutputPath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
				var resourceFilesFromDisk = Directory.EnumerateFiles (projectOutputPath, "*.resources.dll", SearchOption.AllDirectories)
					.Select (r => r = r.Replace (projectOutputPath, string.Empty).Replace ("\\", "/"));

				var overrideContents = string.Empty;
				foreach (var dir in GetOverrideDirectoryPaths (proj.PackageName)) {
					overrideContents += RunAdbCommand ($"shell run-as {proj.PackageName} find {dir}");
				}
				builder.BuildLogFile = "uninstall.log";
				builder.Uninstall (proj);
				Assert.IsTrue (resourceFilesFromDisk.Any (), $"Unable to find any localized assemblies in {resourceFilesFromDisk}");
				foreach (var res in resourceFilesFromDisk) {
					StringAssert.Contains (res, overrideContents, $"{res} did not exist in the .__override__ directory.\nFound:{overrideContents}");
				}

			}
		}

		[Test]
		public void IncrementalFastDeployment ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var lib1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("Class1.cs") {
						TextContent = () => "namespace Library1 { public class Class1 { } }"
					},
				}
			};

			var lib2 = new DotNetStandard {
				ProjectName = "Library2",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard2.0",
				Sources = {
					new BuildItem.Source ("Class2.cs") {
						TextContent = () => "namespace Library2 { public class Class2 { } }"
					},
				}
			};

			var app = new XamarinFormsAndroidApplicationProject () {
				EmbedAssembliesIntoApk = false,
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\Library2\\Library2.csproj"),
				},
			};

			// Set up library projects
			var rootPath = Path.Combine (Root, "temp", TestName);
			using (var lb1 = CreateDllBuilder (Path.Combine (rootPath, lib1.ProjectName)))
				Assert.IsTrue (lb1.Build (lib1), "First library build should have succeeded.");
			using (var lb2 = CreateDllBuilder (Path.Combine (rootPath, lib2.ProjectName)))
				Assert.IsTrue (lb2.Build (lib2), "Second library build should have succeeded.");

			using (var builder = CreateApkBuilder (Path.Combine (rootPath, app.ProjectName))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Install (app), "First install should have succeeded.");
				var firstInstallTime = builder.LastBuildTime;
				Assert.IsTrue (builder.Install (app, doNotCleanupOnUpdate: true, saveProject: false), "Second install should have succeeded.");
				var secondInstallTime = builder.LastBuildTime;

				var filesToTouch = new [] {
					Path.Combine (rootPath, lib1.ProjectName, "Class1.cs"),
					Path.Combine (rootPath, lib2.ProjectName, "Class2.cs"),
					Path.Combine (rootPath, app.ProjectName, "MainPage.xaml"),
				};
				foreach (var file in filesToTouch) {
					FileAssert.Exists (file);
					File.SetLastWriteTimeUtc (file, DateTime.UtcNow);
				}

				Assert.IsTrue (builder.Install (app, doNotCleanupOnUpdate: true, saveProject: false), "Third install should have succeeded.");
				var thirdInstallTime = builder.LastBuildTime;
				Assert.IsTrue (builder.Install (app, doNotCleanupOnUpdate: true, saveProject: false), "Fourth install should have succeeded.");
				var fourthInstalTime = builder.LastBuildTime;

				Assert.IsTrue (thirdInstallTime < firstInstallTime, $"Third incremental install: '{thirdInstallTime}' should be faster than clean install: '{firstInstallTime}'.");
				Assert.IsTrue (secondInstallTime < firstInstallTime && secondInstallTime < thirdInstallTime,
					$"Second unchanged install: '{secondInstallTime}' should be faster than clean install: '{firstInstallTime}' and incremental install: '{thirdInstallTime}'.");
				Assert.IsTrue (fourthInstalTime < firstInstallTime && fourthInstalTime < thirdInstallTime,
					$"Fourth unchanged install: '{fourthInstalTime}' should be faster than clean install: '{firstInstallTime}' and incremental install: '{thirdInstallTime}'.");
			}
		}

	}
}
