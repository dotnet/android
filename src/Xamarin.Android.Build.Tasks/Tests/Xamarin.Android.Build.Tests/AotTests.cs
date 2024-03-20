using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[Category ("AOT")]
	[Parallelizable (ParallelScope.Children)]
	public class AotTests : BaseTest
	{
		public string SdkWithSpacesPath {
			get {
				return Path.Combine (Root, "temp", string.Format ("SDK Ümläüts"));
			}
		}

		[OneTimeSetUp]
		public void Setup ()
		{
			if (!IsWindows)
				return;

			var sdkPath = AndroidSdkPath;
			var ndkPath = AndroidNdkPath;

			var symSdkPath = Path.Combine (SdkWithSpacesPath, "sdk");
			var symNdkPath = Path.Combine (SdkWithSpacesPath, "ndk");

			SymbolicLink.Create (symSdkPath, sdkPath);
			SymbolicLink.Create (symNdkPath, ndkPath);

			Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", symSdkPath);
			Environment.SetEnvironmentVariable ("TEST_ANDROID_NDK_PATH", symNdkPath);
		}

		[OneTimeTearDown]
		public void TearDown ()
		{
			if (!IsWindows)
				return;
			Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", "");
			Environment.SetEnvironmentVariable ("TEST_ANDROID_NDK_PATH", "");
			Directory.Delete (SdkWithSpacesPath, recursive: true);
		}

		void AssertProfiledAotBuildMessages(ProjectBuilder b)
		{
			StringAssertEx.ContainsRegex (@$"Using profile data file.*dotnet\.aotprofile", b.LastBuildOutput, "Should use default AOT profile", RegexOptions.IgnoreCase);
			StringAssertEx.ContainsRegex (@$"Method.*emitted at", b.LastBuildOutput, "Should contain verbose AOT compiler output", RegexOptions.IgnoreCase);
		}

		[Test, Category ("ProfiledAOT")]
		public void BuildBasicApplicationReleaseProfiledAot ([Values (true, false)] bool enableLLVM)
		{
			if (TestEnvironment.IsWindows && enableLLVM) {
				Assert.Ignore("https://github.com/dotnet/runtime/issues/93788");
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AndroidEnableProfiledAot = true,
			};
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidExtraAotOptions", "--verbose");
			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			AssertProfiledAotBuildMessages (b);
		}

		[Test, Category ("ProfiledAOT")]
		public void BuildBasicApplicationReleaseWithCustomAotProfile ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AndroidEnableProfiledAot = true,
			};
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidExtraAotOptions", "--verbose");

			byte [] custom_aot_profile;
			using (var stream = typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.custom.aotprofile")) {
				custom_aot_profile = new byte [stream.Length];
				stream.Read (custom_aot_profile, 0, (int) stream.Length);
			}
			proj.OtherBuildItems.Add (new BuildItem ("AndroidAotProfile", "custom.aotprofile") { BinaryContent = () => custom_aot_profile });

			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			AssertProfiledAotBuildMessages (b);
		}

		[Test, Category ("ProfiledAOT")]
		public void BuildBasicApplicationReleaseProfiledAotWithoutDefaultProfile ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AndroidEnableProfiledAot = true,
			};
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidUseDefaultAotProfile", "false");
			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			StringAssertEx.DoesNotContainRegex (@$"Using profile data file.*dotnet\.aotprofile", b.LastBuildOutput, "Should not use default AOT profile", RegexOptions.IgnoreCase);
		}

		[Test]
		[TestCase ("テスト", false, false)]
		[TestCase ("テスト", true, true)]
		[TestCase ("テスト", true, false)]
		[TestCase ("随机生成器", false, false)]
		[TestCase ("随机生成器", true, true)]
		[TestCase ("随机生成器", true, false)]
		[TestCase ("中国", false, false)]
		[TestCase ("中国", true, true)]
		[TestCase ("中国", true, false)]
		public void BuildAotApplicationWithSpecialCharactersInProject (string testName, bool isRelease, bool aot)
		{
			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = testName,
				IsRelease = isRelease,
				AotAssemblies = aot,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a",  "arm64-v8a", "x86", "x86_64");
			using (var builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName))){
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			}
		}

		static object [] AotChecks () => new object [] {
			new object[] {
				/* supportedAbis */   "arm64-v8a",
				/* enableLLVM */      false,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "armeabi-v7a;x86",
				/* enableLLVM */      true,
				/* usesAssemblyBlobs */ true,
			},
			new object[] {
				/* supportedAbis */   "armeabi-v7a;arm64-v8a;x86;x86_64",
				/* enableLLVM */      false,
				/* usesAssemblyBlobs */ true,
			},
			new object[] {
				/* supportedAbis */   "armeabi-v7a;arm64-v8a;x86;x86_64",
				/* enableLLVM */      true,
				/* usesAssemblyBlobs */ false,
			},
		};

		[Test]
		[TestCaseSource (nameof (AotChecks))]
		public void BuildAotApplicationWithNdkAndBundleAndÜmläüts (string supportedAbis, bool enableLLVM, bool usesAssemblyBlobs)
		{
			if (IsWindows)
				Assert.Ignore ("https://github.com/dotnet/runtime/issues/88625");

			var abisSanitized = supportedAbis.Replace (";", "").Replace ("-", "").Replace ("_", "");
			var path = Path.Combine ("temp", string.Format ("BuildAotNdk AndÜmläüts_{0}_{1}_{2}", abisSanitized, enableLLVM, usesAssemblyBlobs));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
				PackageName = "com.xamarin.buildaotappwithspecialchars",
			};

			proj.SetProperty ("AndroidNdkDirectory", AndroidNdkPath);
			proj.SetRuntimeIdentifiers (supportedAbis.Split (';'));
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			proj.SetProperty ("AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			bool checkMinLlvmPath = enableLLVM && (supportedAbis == "armeabi-v7a" || supportedAbis == "x86");
			if (checkMinLlvmPath) {
				// Set //uses-sdk/@android:minSdkVersion so that LLVM uses the right libc.so
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:minSdkVersion=""{Xamarin.Android.Tools.XABuildConfig.AndroidMinimumDotNetApiLevel}"" />
	<application android:label=""{proj.ProjectName}"">
	</application>
</manifest>";
			}
			using (var b = CreateApkBuilder (path)) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				foreach (var abi in supportedAbis.Split (new char [] { ';' })) {
					var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
					var aotNativeLibrary = Path.Combine (intermediate, AbiUtils.AbiToRuntimeIdentifier (abi), "aot", "UnnamedProject.dll.so");
					FileAssert.Exists (aotNativeLibrary);
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.OutputPath, $"{proj.PackageName}-Signed.apk");

					var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
					Assert.IsTrue (helper.Exists ($"assemblies/{abi}/UnnamedProject.dll"), $"{abi}/UnnamedProject.dll should be in {proj.PackageName}-Signed.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							$"lib/{0}/libaot-UnnamedProject.dll.so should be in the {proj.PackageName}-Signed.apk", abi);
					}
				}
				Assert.IsTrue (b.Build (proj), "Second Build should have succeeded.");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
					"the _BuildApkEmbed target should be skipped");
			}
		}

		[Test]
		[TestCaseSource (nameof (AotChecks))]
		public void BuildAotApplicationAndÜmläüts (string supportedAbis, bool enableLLVM, bool usesAssemblyBlobs)
		{
			if (IsWindows)
				Assert.Ignore ("https://github.com/dotnet/runtime/issues/88625");

			var abisSanitized = supportedAbis.Replace (";", "").Replace ("-", "").Replace ("_", "");
			var path = Path.Combine ("temp", string.Format ("BuildAot AndÜmläüts_{0}_{1}_{2}", abisSanitized, enableLLVM, usesAssemblyBlobs));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
				PackageName = "com.xamarin.buildaotappandbundlewithspecialchars",
			};
			proj.SetRuntimeIdentifiers (supportedAbis.Split (';'));
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			proj.SetProperty ("AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			using (var b = CreateApkBuilder (path)) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				foreach (var abi in supportedAbis.Split (new char [] { ';' })) {
					var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
					var aotNativeLibrary = Path.Combine (intermediate, AbiUtils.AbiToRuntimeIdentifier (abi), "aot", "UnnamedProject.dll.so");
					FileAssert.Exists (aotNativeLibrary);
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.OutputPath, $"{proj.PackageName}-Signed.apk");

					var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
					Assert.IsTrue (helper.Exists ($"assemblies/{abi}/UnnamedProject.dll"), $"{abi}/UnnamedProject.dll should be in {proj.PackageName}-Signed.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							$"lib/{0}/libaot-UnnamedProject.dll.so should be in the {proj.PackageName}-Signed.apk", abi);
					}
				}
				Assert.IsTrue (b.Build (proj), "Second Build should have succeeded.");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
					"the _BuildApkEmbed target should be skipped");
			}
		}

		[Test]
		[NonParallelizable]
		[Category ("XamarinBuildDownload")]
		public void BuildAMassiveApp ()
		{
			var testPath = Path.Combine ("temp", "BuildAMassiveApp");
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, testPath);
			var sb = new SolutionBuilder ("BuildAMassiveApp.sln") {
				SolutionPath = Path.Combine (Root, testPath),
			};
			var app1 = new XamarinFormsMapsApplicationProject {
				ProjectName = "App1",
				AotAssemblies = true,
				IsRelease = true,
			};
			//NOTE: BuildingInsideVisualStudio prevents the projects from being built as dependencies
			sb.BuildingInsideVisualStudio = false;
			app1.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_CheckAbis"" BeforeTargets=""_DefineBuildTargetAbis"">
	<PropertyGroup>
		<AndroidSupportedAbis>armeabi-v7a;x86</AndroidSupportedAbis>
		<AndroidSupportedAbis Condition=""Exists('$(MSBuildThisFileDirectory)..\..\..\..\Debug\lib\xamarin.android\xbuild\Xamarin\Android\lib\arm64-v8a\libmono-android.release.so')"">$(AndroidSupportedAbis);arm64-v8a</AndroidSupportedAbis>
		<AndroidSupportedAbis Condition=""Exists('$(MSBuildThisFileDirectory)..\..\..\..\Debug\lib\xamarin.android\xbuild\Xamarin\Android\lib\x86_64\libmono-android.release.so')"">$(AndroidSupportedAbis);x86_64</AndroidSupportedAbis>
	</PropertyGroup>
	<Message Text=""$(AndroidSupportedAbis)"" />
</Target>
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)" + Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
			});
			sb.Projects.Add (app1);
			var code = new StringBuilder ();
			code.AppendLine ("using System;");
			code.AppendLine ("namespace App1 {");
			code.AppendLine ("\tpublic class AppCode {");
			code.AppendLine ("\t\tpublic void Foo () {");
			for (int i = 0; i < 128; i++) {
				var libName = $"Lib{i}";
				var lib = new XamarinAndroidLibraryProject () {
					ProjectName = libName,
					IsRelease = true,
					OtherBuildItems = {
						new AndroidItem.AndroidAsset ($"Assets\\{libName}.txt") {
							TextContent = () => "Asset1",
							Encoding = Encoding.ASCII,
						},
						new AndroidItem.AndroidAsset ($"Assets\\subfolder\\{libName}.txt") {
							TextContent = () => "Asset2",
							Encoding = Encoding.ASCII,
						},
					},
					Sources = {
						new BuildItem.Source ($"{libName}.cs") {
							TextContent = () => @"using System;

namespace "+ libName + @" {

	public class " + libName + @" {
		public static void Foo () {
		}
	}
}"
						},
					}
				};
				var strings = lib.AndroidResources.First (x => x.Include () == "Resources\\values\\Strings.xml");
				strings.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""" + libName + @"_name"">" + libName + @"</string>
</resources>";
				sb.Projects.Add (lib);
				app1.References.Add (new BuildItem.ProjectReference ($"..\\{libName}\\{libName}.csproj", libName, lib.ProjectGuid));
				code.AppendLine ($"\t\t\t{libName}.{libName}.Foo ();");
			}
			code.AppendLine ("\t\t}");
			code.AppendLine ("\t}");
			code.AppendLine ("}");
			app1.Sources.Add (new BuildItem.Source ("Code.cs") {
				TextContent = () => code.ToString (),
			});
			Assert.IsTrue (sb.Build (new string [] { "Configuration=Release" }), "Solution should have built.");
			Assert.IsTrue (sb.BuildProject (app1, "SignAndroidPackage"), "Build of project should have succeeded");
			sb.Dispose ();
		}

		[Test]
		[Category ("LLVM")]
		public void NoSymbolsArgShouldReduceAppSize ([Values (false, true)] bool skipDebugSymbols)
		{
			if (IsWindows)
				Assert.Ignore ("https://github.com/dotnet/runtime/issues/88625");

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
			};
			var supportedAbi = "arm64-v8a";
			proj.SetAndroidSupportedAbis (supportedAbi);
			proj.SetProperty ("EnableLLVM", true.ToString ());

			var xaAssemblySize = 0;
			var xaAssemblySizeNoSymbol = 0;

			using (var b = CreateApkBuilder ()) {
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "First build should have succeeded.");
				var apkPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				FileAssert.Exists (apkPath);
				using (var apk = ZipHelper.OpenZip (apkPath)) {
					xaAssemblySize = ZipHelper.ReadFileFromZip (apk, $"lib/{supportedAbi}/libaot-Mono.Android.dll.so").Length;
				}

				string additionalArgs = "no-write-symbols";
				if (skipDebugSymbols) {
					additionalArgs += ",nodebug";
				}
				proj.SetProperty ("AndroidAotAdditionalArguments", additionalArgs);
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");
				FileAssert.Exists (apkPath);
				using (var apk = ZipHelper.OpenZip (apkPath)) {
					xaAssemblySizeNoSymbol = ZipHelper.ReadFileFromZip (apk, $"lib/{supportedAbi}/libaot-Mono.Android.dll.so").Length;
				}
				Assert.IsTrue (xaAssemblySize > 0 && xaAssemblySizeNoSymbol > 0, $"Mono.Android.dll.so size was not updated after first or second build. Before: '{xaAssemblySize}' After: '{xaAssemblySizeNoSymbol}'.");
				Assert.Less (xaAssemblySizeNoSymbol, xaAssemblySize, "Mono.Android.dll.so should have been smaller after 'no-write-symbols' build.");
			}
		}

		[Test]
		[Category ("AOT")]
		public void AotAssembliesInIDE ()
		{
			string supportedAbis = "arm64-v8a";
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
			};
			proj.SetAndroidSupportedAbis (supportedAbis);
			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.RunTarget (proj, target: "Build"));

			// .apk won't exist yet
			var apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			FileAssert.DoesNotExist (apk);

			Assert.IsTrue (b.RunTarget (proj, target: "SignAndroidPackage"));
			FileAssert.Exists (apk);

			using var zipFile = ZipHelper.OpenZip (apk);
			foreach (var abi in supportedAbis.Split (';')) {
				var path = $"lib/{abi}/libaot-Mono.Android.dll.so";
				var entry = ZipHelper.ReadFileFromZip (zipFile, path);
				Assert.IsNotNull (entry, $"{path} should be in {apk}", abi);
			}
		}

		[Test]
		public void CheckWhetherLibcAndLibmAreReferencedInAOTLibraries ()
		{
			if (IsWindows)
				Assert.Ignore ("https://github.com/dotnet/runtime/issues/88625");

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EmbedAssembliesIntoApk = true,
				AotAssemblies = true,
			};
			proj.SetProperty ("EnableLLVM", "True");

			var abis = new [] { "arm64-v8a", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);

			var libPaths = new List<string> ();
			libPaths.Add (Path.Combine ("android-arm64", "aot", "Mono.Android.dll.so"));
			libPaths.Add (Path.Combine ("android-x64", "aot", "Mono.Android.dll.so"));

			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				string objPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);

				foreach (string libPath in libPaths) {
					string lib = Path.Combine (objPath, libPath);

					Assert.IsTrue (File.Exists (lib), $"Library {lib} should exist on disk");
					Assert.IsTrue (ELFHelper.ReferencesLibrary (lib, "libc.so"), $"Library {lib} should reference libc.so");
					Assert.IsTrue (ELFHelper.ReferencesLibrary (lib, "libm.so"), $"Library {lib} should reference libm.so");
				}
			}
		}

	}
}
