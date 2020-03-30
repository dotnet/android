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
		XamarinAndroidLibraryProject lib;
		XamarinAndroidApplicationProject app;
		ProjectBuilder libBuilder, appBuilder;
		string intermediate;
		string bin;

		const string Resx = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
	<resheader name=""resmimetype"">
		<value>text/microsoft-resx</value>
	</resheader>
	<resheader name=""version"">
		<value>2.0</value>
	</resheader>
	<resheader name=""reader"">
		<value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
	<resheader name=""writer"">
		<value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
	<!--contents-->
</root>";
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
						TextContent = () => ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
					new BuildItem ("EmbeddedResource", "Foo.es.resx") {
						TextContent = () => ResxWithContents ("<data name=\"CancelButton\"><value>Cancelar</value></data>")
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
			var abis = new string [] { "armeabi-v7a", "arm64-v8a", "x86" };
			app.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			app.SetProperty ("AndroidBundleConfigurationFile", "buildConfig.json");

			libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (libBuilder.Build (lib), "Library build should have succeeded.");
			appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (appBuilder.Build (app), "App build should have succeeded.");

			var projectDir = Path.Combine (Root, appBuilder.ProjectDirectory);
			intermediate = Path.Combine (projectDir, app.IntermediateOutputPath);
			bin = Path.Combine (projectDir, app.OutputPath);
		}

		string ResxWithContents (string contents)
		{
			return Resx.Replace ("<!--contents-->", contents);
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
			var expectedFiles = new [] {
				"dex/classes.dex",
				"lib/arm64-v8a/libmono-btls-shared.so",
				"lib/arm64-v8a/libmonodroid.so",
				"lib/arm64-v8a/libmono-native.so",
				"lib/arm64-v8a/libmonosgen-2.0.so",
				"lib/arm64-v8a/libxamarin-app.so",
				"lib/armeabi-v7a/libmono-btls-shared.so",
				"lib/armeabi-v7a/libmonodroid.so",
				"lib/armeabi-v7a/libmono-native.so",
				"lib/armeabi-v7a/libmonosgen-2.0.so",
				"lib/armeabi-v7a/libxamarin-app.so",
				"lib/x86/libmono-btls-shared.so",
				"lib/x86/libmonodroid.so",
				"lib/x86/libmono-native.so",
				"lib/x86/libmonosgen-2.0.so",
				"lib/x86/libxamarin-app.so",
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
				"root/assemblies/mscorlib.dll",
				"root/assemblies/System.Core.dll",
				"root/assemblies/System.dll",
				"root/assemblies/System.Runtime.Serialization.dll",
				"root/assemblies/Localization.dll",
				"root/assemblies/es/Localization.resources.dll",
				"root/assemblies/UnnamedProject.dll",
				"root/NOTICE",
				//These are random files from Google Play Services .jar/.aar files
				"root/build-data.properties",
				"root/com/google/api/client/repackaged/org/apache/commons/codec/language/dmrules.txt",
				"root/error_prone/Annotations.gwt.xml",
				"root/protobuf.meta",
			};
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
			var expectedFiles = new [] {
				"base/dex/classes.dex",
				"base/lib/arm64-v8a/libmono-btls-shared.so",
				"base/lib/arm64-v8a/libmonodroid.so",
				"base/lib/arm64-v8a/libmono-native.so",
				"base/lib/arm64-v8a/libmonosgen-2.0.so",
				"base/lib/arm64-v8a/libxamarin-app.so",
				"base/lib/armeabi-v7a/libmono-btls-shared.so",
				"base/lib/armeabi-v7a/libmonodroid.so",
				"base/lib/armeabi-v7a/libmono-native.so",
				"base/lib/armeabi-v7a/libmonosgen-2.0.so",
				"base/lib/armeabi-v7a/libxamarin-app.so",
				"base/lib/x86/libmono-btls-shared.so",
				"base/lib/x86/libmonodroid.so",
				"base/lib/x86/libmono-native.so",
				"base/lib/x86/libmonosgen-2.0.so",
				"base/lib/x86/libxamarin-app.so",
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
				"base/root/assemblies/mscorlib.dll",
				"base/root/assemblies/System.Core.dll",
				"base/root/assemblies/System.dll",
				"base/root/assemblies/System.Runtime.Serialization.dll",
				"base/root/assemblies/Localization.dll",
				"base/root/assemblies/es/Localization.resources.dll",
				"base/root/assemblies/UnnamedProject.dll",
				"base/root/NOTICE",
				"BundleConfig.pb",
				//These are random files from Google Play Services .jar/.aar files
				"base/root/build-data.properties",
				"base/root/com/google/api/client/repackaged/org/apache/commons/codec/language/dmrules.txt",
				"base/root/error_prone/Annotations.gwt.xml",
				"base/root/protobuf.meta",
			};
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
			if (!HasDevices)
				Assert.Ignore ("Skipping Installation. No devices available.");

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
