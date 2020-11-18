using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class BundleToolTests : BaseTest
	{
		static readonly string [] Abis = new [] { "armeabi-v7a", "arm64-v8a", "x86" };
		XamarinAndroidLibraryProject lib;
		XamarinAndroidApplicationProject app;
		ProjectBuilder libBuilder, appBuilder;
		string intermediate;
		string bin;

		// Disable split by language
		const string BuildConfig = @"{
	""optimizations"": {
		""splits_config"": {
			""split_dimension"": [
				{
					""value"": ""LANGUAGE"",
					""negate"": true
				}
			]
		}
	}
}";

		[OneTimeSetUp]
		public void OneTimeSetUp ()
		{
			var path = Path.Combine ("temp", TestName);
			lib = new XamarinAndroidLibraryProject {
				ProjectName = "Localization",
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("EmbeddedResource", "Foo.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
					new BuildItem ("EmbeddedResource", "Foo.es.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancelar</value></data>")
					}
				}
			};

			var bytes = new byte [1024];
			app = new XamarinFormsMapsApplicationProject {
				IsRelease = true,
			};
			app.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("foo.bar") {
				BinaryContent = () => bytes,
			});
			app.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("foo.wav") {
				BinaryContent = () => bytes,
			});
			app.OtherBuildItems.Add (new BuildItem ("None", "buildConfig.json") {
				TextContent = () => BuildConfig,
			});
			app.SetProperty ("AndroidStoreUncompressedFileExtensions", ".bar");
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			//NOTE: this is here to enable adb shell run-as
			app.AndroidManifest = app.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
			app.SetProperty (app.ReleaseProperties, "AndroidPackageFormat", "aab");
			app.SetAndroidSupportedAbis (Abis);
			app.SetProperty ("AndroidBundleConfigurationFile", "buildConfig.json");

			libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (libBuilder.Build (lib), "Library build should have succeeded.");
			appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (appBuilder.Build (app), "App build should have succeeded.");

			var projectDir = Path.Combine (Root, appBuilder.ProjectDirectory);
			intermediate = Path.Combine (projectDir, app.IntermediateOutputPath);
			bin = Path.Combine (projectDir, app.OutputPath);
		}

		[TearDown]
		public void TearDown ()
		{
			var status = TestContext.CurrentContext.Result.Outcome.Status;
			if (status == NUnit.Framework.Interfaces.TestStatus.Failed) {
				if (libBuilder != null)
					libBuilder.CleanupOnDispose = false;
				if (appBuilder != null)
					appBuilder.CleanupOnDispose = false;
			}
		}

		[OneTimeTearDown]
		public void OneTimeTearDown ()
		{
			libBuilder?.Dispose ();
			appBuilder?.Dispose ();
		}

		string [] ListArchiveContents (string archive)
		{
			var entries = new List<string> ();
			using (var zip = ZipArchive.Open (archive, FileMode.Open)) {
				foreach (var entry in zip) {
					entries.Add (entry.FullName);
				}
			}
			entries.Sort ();
			return entries.ToArray ();
		}

		[Test]
		public void BaseZip ()
		{
			var baseZip = Path.Combine (intermediate, "android", "bin", "base.zip");
			var contents = ListArchiveContents (baseZip);
			var expectedFiles = new List<string> {
				"dex/classes.dex",
				"manifest/AndroidManifest.xml",
				"res/drawable-hdpi-v4/icon.png",
				"res/drawable-mdpi-v4/icon.png",
				"res/drawable-xhdpi-v4/icon.png",
				"res/drawable-xxhdpi-v4/icon.png",
				"res/drawable-xxxhdpi-v4/icon.png",
				"res/layout/main.xml",
				"resources.pb",
				"root/assemblies/Java.Interop.dll",
				"root/assemblies/Mono.Android.dll",
				"root/assemblies/Localization.dll",
				"root/assemblies/es/Localization.resources.dll",
				"root/assemblies/UnnamedProject.dll",
			};
			if (Builder.UseDotNet) {
				expectedFiles.Add ("root/assemblies/System.Console.dll");
				expectedFiles.Add ("root/assemblies/System.IO.FileSystem.dll");
				expectedFiles.Add ("root/assemblies/System.Linq.dll");
				expectedFiles.Add ("root/assemblies/System.Net.Http.dll");

				//These are random files from Google Play Services .aar files
				expectedFiles.Add ("root/play-services-base.properties");
				expectedFiles.Add ("root/play-services-basement.properties");
				expectedFiles.Add ("root/play-services-maps.properties");
				expectedFiles.Add ("root/play-services-tasks.properties");
			} else {
				expectedFiles.Add ("root/assemblies/mscorlib.dll");
				expectedFiles.Add ("root/assemblies/System.Core.dll");
				expectedFiles.Add ("root/assemblies/System.dll");
				expectedFiles.Add ("root/assemblies/System.Runtime.Serialization.dll");

				//These are random files from Google Play Services .aar files
				expectedFiles.Add ("root/build-data.properties");
				expectedFiles.Add ("root/com/google/api/client/repackaged/org/apache/commons/codec/language/dmrules.txt");
				expectedFiles.Add ("root/error_prone/Annotations.gwt.xml");
				expectedFiles.Add ("root/protobuf.meta");
			}
			foreach (var abi in Abis) {
				expectedFiles.Add ($"lib/{abi}/libmonodroid.so");
				expectedFiles.Add ($"lib/{abi}/libmonosgen-2.0.so");
				expectedFiles.Add ($"lib/{abi}/libxamarin-app.so");
				if (Builder.UseDotNet) {
					expectedFiles.Add ($"lib/{abi}/libSystem.IO.Compression.Native.so");
					expectedFiles.Add ($"lib/{abi}/libSystem.Native.so");
				} else {
					expectedFiles.Add ($"lib/{abi}/libmono-native.so");
					expectedFiles.Add ($"lib/{abi}/libmono-btls-shared.so");
				}
			}
			foreach (var expected in expectedFiles) {
				CollectionAssert.Contains (contents, expected, $"`{baseZip}` did not contain `{expected}`");
			}
		}

		[Test]
		public void AppBundle ()
		{
			var aab = Path.Combine (intermediate, "android", "bin", "UnnamedProject.UnnamedProject.aab");
			FileAssert.Exists (aab);
			var contents = ListArchiveContents (aab);
			var expectedFiles = new List<string> {
				"base/dex/classes.dex",
				"base/manifest/AndroidManifest.xml",
				"base/native.pb",
				"base/res/drawable-hdpi-v4/icon.png",
				"base/res/drawable-mdpi-v4/icon.png",
				"base/res/drawable-xhdpi-v4/icon.png",
				"base/res/drawable-xxhdpi-v4/icon.png",
				"base/res/drawable-xxxhdpi-v4/icon.png",
				"base/res/layout/main.xml",
				"base/resources.pb",
				"base/root/assemblies/Java.Interop.dll",
				"base/root/assemblies/Mono.Android.dll",
				"base/root/assemblies/Localization.dll",
				"base/root/assemblies/es/Localization.resources.dll",
				"base/root/assemblies/UnnamedProject.dll",
				"BundleConfig.pb",
			};
			if (Builder.UseDotNet) {
				expectedFiles.Add ("base/root/assemblies/System.Console.dll");
				expectedFiles.Add ("base/root/assemblies/System.IO.FileSystem.dll");
				expectedFiles.Add ("base/root/assemblies/System.Linq.dll");
				expectedFiles.Add ("base/root/assemblies/System.Net.Http.dll");

				//These are random files from Google Play Services .aar files
				expectedFiles.Add ("base/root/play-services-base.properties");
				expectedFiles.Add ("base/root/play-services-basement.properties");
				expectedFiles.Add ("base/root/play-services-maps.properties");
				expectedFiles.Add ("base/root/play-services-tasks.properties");
			} else {
				expectedFiles.Add ("base/root/assemblies/mscorlib.dll");
				expectedFiles.Add ("base/root/assemblies/System.Core.dll");
				expectedFiles.Add ("base/root/assemblies/System.dll");
				expectedFiles.Add ("base/root/assemblies/System.Runtime.Serialization.dll");

				//These are random files from Google Play Services .aar files
				expectedFiles.Add ("base/root/build-data.properties");
				expectedFiles.Add ("base/root/com/google/api/client/repackaged/org/apache/commons/codec/language/dmrules.txt");
				expectedFiles.Add ("base/root/error_prone/Annotations.gwt.xml");
				expectedFiles.Add ("base/root/protobuf.meta");
			}
			foreach (var abi in Abis) {
				expectedFiles.Add ($"base/lib/{abi}/libmonodroid.so");
				expectedFiles.Add ($"base/lib/{abi}/libmonosgen-2.0.so");
				expectedFiles.Add ($"base/lib/{abi}/libxamarin-app.so");
				if (Builder.UseDotNet) {
					expectedFiles.Add ($"base/lib/{abi}/libSystem.IO.Compression.Native.so");
					expectedFiles.Add ($"base/lib/{abi}/libSystem.Native.so");
				} else {
					expectedFiles.Add ($"base/lib/{abi}/libmono-native.so");
					expectedFiles.Add ($"base/lib/{abi}/libmono-btls-shared.so");
				}
			}
			foreach (var expected in expectedFiles) {
				CollectionAssert.Contains (contents, expected, $"`{aab}` did not contain `{expected}`");
			}
		}

		[Test]
		public void AppBundleSigned ()
		{
			var aab = Path.Combine (bin, "UnnamedProject.UnnamedProject-Signed.aab");
			FileAssert.Exists (aab);
			var contents = ListArchiveContents (aab);
			Assert.IsTrue (StringAssertEx.ContainsText (contents, "META-INF/MANIFEST.MF"), $"{aab} is not signed!");
		}

		[Test, Category ("UsesDevice")]
		public void ApkSet ()
		{
			AssertHasDevices ();

			Assert.IsTrue (appBuilder.RunTarget (app, "Install"), "App should have installed.");

			var aab = Path.Combine (intermediate, "android", "bin", "UnnamedProject.UnnamedProject.apks");
			FileAssert.Exists (aab);
			// Expecting: splits/base-arm64_v8a.apk, splits/base-master.apk, splits/base-xxxhdpi.apk
			// This are split up based on: abi, base, and dpi
			var contents = ListArchiveContents (aab).Where (a => a.EndsWith (".apk", StringComparison.OrdinalIgnoreCase)).ToArray ();
			Assert.AreEqual (3, contents.Length, "Expecting three APKs!");

			// Language split has been removed by the bundle configuration file, and therefore shouldn't be present
			var languageSplitContent = ListArchiveContents (aab).Where (a => a.EndsWith ("-en.apk", StringComparison.OrdinalIgnoreCase)).ToArray ();
			Assert.AreEqual (0, languageSplitContent.Length, "Found language split apk in bundle, but disabled by bundle configuration file!");

			using (var stream = new MemoryStream ())
			using (var apkSet = ZipArchive.Open (aab, FileMode.Open)) {
				// We have a zip inside a zip
				var baseMaster = apkSet.ReadEntry ("splits/base-master.apk");
				baseMaster.Extract (stream);

				stream.Position = 0;
				var uncompressed = new [] { ".dll", ".bar", ".wav" };
				using (var baseApk = ZipArchive.Open (stream)) {
					foreach (var file in baseApk) {
						foreach (var ext in uncompressed) {
							if (file.FullName.EndsWith (ext, StringComparison.OrdinalIgnoreCase)) {
								Assert.AreEqual (CompressionMethod.Store, file.CompressionMethod, $"{file.FullName} should be uncompressed!");
							}
						}
					}
				}
			}
		}

		[Test]
		public void BuildAppBundleCommand ()
		{
			var task = new BuildAppBundle {
				BaseZip = "base.zip",
				Output = "foo.aab",
			};
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"build-bundle --modules base.zip --output foo.aab", cmd);
		}

		[Test]
		public void BuildApkSetCommand ()
		{
			var task = new BuildApkSet {
				AppBundle = "foo.aab",
				Output = "foo.apks",
				KeyStore = "foo.keystore",
				KeyAlias = "alias",
				KeyPass = "keypass",
				StorePass = "storepass",
				Aapt2ToolPath = Path.Combine ("aapt", "with spaces"),
				Aapt2ToolExe = "aapt2",
				AdbToolPath = Path.Combine ("adb", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554"
			};
			string aapt2 = Path.Combine (task.Aapt2ToolPath, task.Aapt2ToolExe);
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"build-apks --connected-device --bundle foo.aab --output foo.apks --mode default --adb \"{adb}\" --device-id emulator-5554 --aapt2 \"{aapt2}\" --ks foo.keystore --ks-key-alias alias --key-pass pass:keypass --ks-pass pass:storepass", cmd);
		}

		[Test]
		public void InstallApkSetCommand ()
		{
			var task = new InstallApkSet {
				ApkSet = "foo.apks",
				AdbToolPath = Path.Combine ("path", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554"
			};
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"install-apks --apks foo.apks --adb \"{adb}\" --device-id emulator-5554 --allow-downgrade --modules _ALL_", cmd);
		}
	}
}
