using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using System.IO.Compression;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[TestCategory ("XamarinBuildDownload")]
	public class BundleToolTests : DeviceTest
	{
		static readonly string [] Abis = ["arm64-v8a", "x86_64"];
		XamarinAndroidLibraryProject lib;
		XamarinAndroidApplicationProject app;
		ProjectBuilder libBuilder, appBuilder;
		string intermediate;
		string bin;
		AndroidRuntime runtime;

		// Disable split by language
		const string BuildConfig = @"{
	""compression"": {
		""uncompressedGlob"": [
			""assets/*.data""
		]
	},
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

		// Keep these checks in one case so CI slicing cannot repeat the project builds.
		[TestMethod]
		[TestCategory ("UsesDevice")]
		[DataRow (AndroidRuntime.CoreCLR)]
		[DataRow (AndroidRuntime.NativeAOT)]
		public void BundleArtifacts (AndroidRuntime runtime)
		{
			this.runtime = runtime;
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var path = Path.Combine ("temp", TestName);
			lib = new XamarinAndroidLibraryProject {
				ProjectName = "Localization",
				IsRelease = isRelease,
				OtherBuildItems = {
					new BuildItem ("EmbeddedResource", "Foo.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
					new BuildItem ("EmbeddedResource", "Foo.es.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancelar</value></data>")
					}
				}
			};

			lib.SetRuntime (runtime);

			var bytes = new byte [1024];
			app = new XamarinFormsMapsApplicationProject {
				IsRelease = isRelease,
				PackageName = "com.xamarin.bundletooltests",
			};
			app.SetRuntime (runtime);
			app.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("foo.bar") {
				BinaryContent = () => bytes,
			});
			app.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("foo.wav") {
				BinaryContent = () => bytes,
			});
			app.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("foo.data") {
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
			app.SetRuntimeIdentifiers (Abis);
			app.SetProperty ("AndroidBundleConfigurationFile", "buildConfig.json");

			libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (libBuilder.Build (lib), "Library build should have succeeded.");
			appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (appBuilder.Build (app), "App build should have succeeded.");

			var projectDir = Path.Combine (Root, appBuilder.ProjectDirectory);
			intermediate = Path.Combine (projectDir, app.IntermediateOutputPath);
			bin = Path.Combine (projectDir, app.OutputPath);

			CheckBaseZip ();
			CheckAppBundle ();
			CheckAppBundleSigned ();
			CheckApkSet ();
		}

		[TestCleanup]
		public void TearDown ()
		{
			if (TestContext.CurrentTestOutcome == UnitTestOutcome.Failed) {
				if (libBuilder != null)
					libBuilder.CleanupOnDispose = false;
				if (appBuilder != null)
					appBuilder.CleanupOnDispose = false;
			}
			libBuilder?.Dispose ();
			appBuilder?.Dispose ();
		}

		string [] ListArchiveContents (string archive, bool usesAssembliesBlob)
		{
			var helper = new ArchiveAssemblyHelper (archive, usesAssembliesBlob);
			List<string> entries = helper.ListArchiveContents ();
			entries.Sort ();
			return entries.ToArray ();
		}

		void CheckBaseZip ()
		{
			var baseZip = Path.Combine (intermediate, "android", "bin", "base.zip");
			var contents = ListArchiveContents (baseZip, usesAssembliesBlob: runtime == AndroidRuntime.CoreCLR);
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
			};

			string blobEntryPrefix = ArchiveAssemblyHelper.DefaultAssemblyStoreEntryPrefix;

			//These are random files from Google Play Services .aar files
			expectedFiles.Add ("root/play-services-base.properties");
			expectedFiles.Add ("root/play-services-basement.properties");
			expectedFiles.Add ("root/play-services-maps.properties");
			expectedFiles.Add ("root/play-services-tasks.properties");

			foreach (var abi in Abis) {
				if (runtime == AndroidRuntime.NativeAOT) {
					expectedFiles.Add ($"lib/{abi}/libUnnamedProject.so");
					continue;
				}

				// All assemblies are in per-abi directories now
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Java.Interop.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Mono.Android.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Localization.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib-es{MonoAndroidHelper.SATELLITE_CULTURE_END_MARKER_CHAR}Localization.resources.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_UnnamedProject.dll.so");

				expectedFiles.Add ($"lib/{abi}/libmonodroid.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_System.Private.CoreLib.dll.so");
				expectedFiles.Add ($"lib/{abi}/libSystem.IO.Compression.Native.so");
				expectedFiles.Add ($"lib/{abi}/libSystem.Native.so");
			}
			foreach (var expected in expectedFiles) {
				CollectionAssert.Contains (contents, expected, $"`{baseZip}` did not contain `{expected}`");
			}
		}

		void CheckAppBundle ()
		{
			var aab = Path.Combine (intermediate, "android", "bin", $"{app.PackageName}.aab");
			Assert.IsTrue (File.Exists (aab));
			var contents = ListArchiveContents (aab, usesAssembliesBlob: runtime == AndroidRuntime.CoreCLR);
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
				"BundleConfig.pb",
			};

			string blobEntryPrefix = ArchiveAssemblyHelper.DefaultAssemblyStoreEntryPrefix;

			//These are random files from Google Play Services .aar files
			expectedFiles.Add ("base/root/play-services-base.properties");
			expectedFiles.Add ("base/root/play-services-basement.properties");
			expectedFiles.Add ("base/root/play-services-maps.properties");
			expectedFiles.Add ("base/root/play-services-tasks.properties");

			foreach (var abi in Abis) {
				if (runtime == AndroidRuntime.NativeAOT) {
					expectedFiles.Add ($"base/lib/{abi}/libUnnamedProject.so");
					continue;
				}

				// All assemblies are in per-abi directories now
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Java.Interop.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Mono.Android.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_Localization.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib-es{MonoAndroidHelper.SATELLITE_CULTURE_END_MARKER_CHAR}Localization.resources.dll.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_UnnamedProject.dll.so");

				expectedFiles.Add ($"base/lib/{abi}/libmonodroid.so");
				expectedFiles.Add ($"{blobEntryPrefix}{abi}/lib_System.Private.CoreLib.dll.so");
				expectedFiles.Add ($"base/lib/{abi}/libSystem.IO.Compression.Native.so");
				expectedFiles.Add ($"base/lib/{abi}/libSystem.Native.so");
			}
			foreach (var expected in expectedFiles) {
				CollectionAssert.Contains (contents, expected, $"`{aab}` did not contain `{expected}`");
			}
		}

		void CheckAppBundleSigned ()
		{
			var aab = Path.Combine (bin, $"{app.PackageName}-Signed.aab");
			Assert.IsTrue (File.Exists (aab));
			var contents = ListArchiveContents (aab, usesAssembliesBlob: false);
			Assert.IsTrue (StringAssertEx.ContainsText (contents, "META-INF/MANIFEST.MF"), $"{aab} is not signed!");
		}

		void CheckApkSet ()
		{
			appBuilder.BuildLogFile = "install.log";
			Assert.IsTrue (appBuilder.RunTarget (app, "Install"), "App should have installed.");

			var aab = Path.Combine (intermediate, "android", "bin", $"{app.PackageName}.apks");
			Assert.IsTrue (File.Exists (aab));
			// Expecting: splits/base-arm64_v8a.apk, splits/base-master.apk, splits/base-xxxhdpi.apk
			// This are split up based on: abi, base, and dpi
			var contents = ListArchiveContents (aab, usesAssembliesBlob: false).Where (a => a.EndsWith (".apk", StringComparison.OrdinalIgnoreCase)).ToArray ();
			Assert.AreEqual (3, contents.Length, "Expecting three APKs!");

			// Language split has been removed by the bundle configuration file, and therefore shouldn't be present
			var languageSplitContent = ListArchiveContents (aab, usesAssembliesBlob: runtime == AndroidRuntime.CoreCLR).Where (a => a.EndsWith ("-en.apk", StringComparison.OrdinalIgnoreCase)).ToArray ();
			Assert.AreEqual (0, languageSplitContent.Length, "Found language split apk in bundle, but disabled by bundle configuration file!");

			using (var stream = new MemoryStream ())
			using (var apkSet = ZipFile.OpenRead (aab)) {
				// We have a zip inside a zip
				var baseMaster = apkSet.GetEntry ("splits/base-master.apk") ?? throw new InvalidOperationException ("Missing base-master.apk.");
				using (var source = baseMaster.Open ())
					source.CopyTo (stream);

				stream.Position = 0;
				var uncompressed = new List<string> {
					".bar",
					".wav",
					".data",
					".blob",
					".dll",
				};
				using (var baseApk = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
					foreach (var file in baseApk.Entries) {
						foreach (var ext in uncompressed) {
							if (file.FullName.EndsWith (ext, StringComparison.OrdinalIgnoreCase)) {
								Assert.AreEqual (file.Length, file.CompressedLength, $"{file.FullName} should be uncompressed!");
							}
						}
					}
				}
			}
		}
	}
}
