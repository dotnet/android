using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Xamarin.Android.Build;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-2"), Category ("AOT")]
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
			string filename = Builder.UseDotNet ? "dotnet" : "startup";
			StringAssertEx.ContainsRegex (@$"Using profile data file.*{filename}\.aotprofile", b.LastBuildOutput, "Should use default AOT profile", RegexOptions.IgnoreCase);
			StringAssertEx.ContainsRegex (@$"Method.*emitted at", b.LastBuildOutput, "Should contain verbose AOT compiler output", RegexOptions.IgnoreCase);
		}

		[Test, Category ("SmokeTests"), Category ("ProfiledAOT")]
		public void BuildBasicApplicationReleaseProfiledAot ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AndroidEnableProfiledAot = true,
			};
			proj.SetProperty (proj.ActiveConfigurationProperties, "AndroidExtraAotOptions", "--verbose");
			using var b = CreateApkBuilder ();
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			AssertProfiledAotBuildMessages (b);
		}

		[Test, Category ("SmokeTests"), Category ("ProfiledAOT")]
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
			string filename = Builder.UseDotNet ? "dotnet" : "startup";
			StringAssertEx.DoesNotContainRegex (@$"Using profile data file.*{filename}\.aotprofile", b.LastBuildOutput, "Should not use default AOT profile", RegexOptions.IgnoreCase);
		}

		static object [] AotChecks () => new object [] {
			new object[] {
				/* supportedAbis */   "armeabi-v7a",
				/* enableLLVM */      false,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "armeabi-v7a",
				/* enableLLVM */      false,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ true,
			},
			new object[] {
				/* supportedAbis */   "armeabi-v7a",
				/* enableLLVM */      true,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "arm64-v8a",
				/* enableLLVM */      false,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "arm64-v8a",
				/* enableLLVM */      true,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "x86",
				/* enableLLVM */      false,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "x86",
				/* enableLLVM */      true,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "x86_64",
				/* enableLLVM */      false,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
			new object[] {
				/* supportedAbis */   "x86_64",
				/* enableLLVM */      true,
				/* expectedResult */  true,
				/* usesAssemblyBlobs */ false,
			},
		};

		[Test]
		[TestCaseSource (nameof (AotChecks))]
		public void BuildAotApplicationAndÜmläüts (string supportedAbis, bool enableLLVM, bool expectedResult, bool usesAssemblyBlobs)
		{
			var path = Path.Combine ("temp", string.Format ("BuildAotApplication AndÜmläüts_{0}_{1}_{2}_{3}", supportedAbis, enableLLVM, expectedResult, usesAssemblyBlobs));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				BundleAssemblies = false,
				AotAssemblies = true,
				PackageName = "com.xamarin.buildaotappwithspecialchars",
			};
			proj.SetProperty (KnownProperties.TargetFrameworkVersion, "v5.1");
			proj.SetAndroidSupportedAbis (supportedAbis);
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			proj.SetProperty ("AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			bool checkMinLlvmPath = enableLLVM && (supportedAbis == "armeabi-v7a" || supportedAbis == "x86");
			if (checkMinLlvmPath) {
				// Set //uses-sdk/@android:minSdkVersion so that LLVM uses the right libc.so
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:minSdkVersion=""{Xamarin.Android.Tools.XABuildConfig.NDKMinimumApiAvailable}"" />
	<application android:label=""{proj.ProjectName}"">
	</application>
</manifest>";
			}
			using (var b = CreateApkBuilder (path)) {
				if (!b.CrossCompilerAvailable (supportedAbis))
					Assert.Ignore ($"Cross compiler for {supportedAbis} was not available");
				if (!b.GetSupportedRuntimes ().Any (x => supportedAbis == x.Abi))
					Assert.Ignore ($"Runtime for {supportedAbis} was not available.");
				b.ThrowOnBuildFailure = false;
				Assert.AreEqual (expectedResult, b.Build (proj), "Build should have {0}.", expectedResult ? "succeeded" : "failed");
				if (!expectedResult)
					return;
				//NOTE: Windows has shortened paths such as: C:\Users\myuser\ANDROI~3\ndk\PLATFO~1\AN3971~1\arch-x86\usr\lib\libc.so
				if (checkMinLlvmPath && !IsWindows && !Builder.UseDotNet) {
					Xamarin.Android.Tasks.NdkTools ndk = Xamarin.Android.Tasks.NdkTools.Create (AndroidNdkPath);
					bool ndk22OrNewer = ndk.Version.Main.Major >= 22;

					// LLVM passes a direct path to libc.so, and we need to use the libc.so
					// which corresponds to the *minimum* SDK version specified in AndroidManifest.xml
					// Since we overrode minSdkVersion=16, that means we should use libc.so from android-16.
					if (ndk22OrNewer) {
						// NDK r22 or newer store libc in [toolchain]/sysroot/usr/lib/[ARCH]/[API]/libc.so
						StringAssertEx.ContainsRegex (@"\s*\[aot-compiler stdout].*sysroot.*.usr.lib.*16.libc\.so", b.LastBuildOutput, "AOT+LLVM should use libc.so from minSdkVersion!");
					} else {
						StringAssertEx.ContainsRegex (@"\s*\[aot-compiler stdout].*android-16.arch-.*.usr.lib.libc\.so", b.LastBuildOutput, "AOT+LLVM should use libc.so from minSdkVersion!");
					}
				}
				foreach (var abi in supportedAbis.Split (new char [] { ';' })) {
					var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
					var libapp = Path.Combine (intermediate, "bundles", abi, "libmonodroid_bundle_app.so");
					FileAssert.DoesNotExist (libapp);
					var aotNativeLibrary = Builder.UseDotNet ?
						Path.Combine (intermediate, AbiUtils.AbiToRuntimeIdentifier (abi), "aot", "UnnamedProject.dll.so") :
						Path.Combine (intermediate, "aot", abi, "libaot-UnnamedProject.dll.so");
					FileAssert.Exists (aotNativeLibrary);
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.OutputPath, $"{proj.PackageName}-Signed.apk");

					var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
					Assert.IsTrue (helper.Exists ("assemblies/UnnamedProject.dll"), $"UnnamedProject.dll should be in the {proj.PackageName}-Signed.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							$"lib/{0}/libaot-UnnamedProject.dll.so should be in the {proj.PackageName}-Signed.apk", abi);
					}
				}
				Assert.AreEqual (expectedResult, b.Build (proj), "Second Build should have {0}.", expectedResult ? "succeeded" : "failed");
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
		[Category ("Minor"), Category ("MkBundle")]
		public void BuildAotApplicationAndBundleAndÜmläüts (string supportedAbis, bool enableLLVM, bool expectedResult, bool usesAssemblyBlobs)
		{
			var path = Path.Combine ("temp", string.Format ("BuildAotApplicationAndBundle AndÜmläüts_{0}_{1}_{2}_{3}", supportedAbis, enableLLVM, expectedResult, usesAssemblyBlobs));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				BundleAssemblies = true,
				AotAssemblies = true,
				PackageName = "com.xamarin.buildaotappandbundlewithspecialchars",
			};
			proj.SetProperty ("AndroidNdkDirectory", AndroidNdkPath);
			proj.SetProperty (KnownProperties.TargetFrameworkVersion, "v5.1");
			proj.SetAndroidSupportedAbis (supportedAbis);
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			proj.SetProperty ("AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			using (var b = CreateApkBuilder (path)) {
				if (!b.CrossCompilerAvailable (supportedAbis))
					Assert.Ignore ("Cross compiler was not available");
				if (!b.GetSupportedRuntimes ().Any (x => supportedAbis == x.Abi))
					Assert.Ignore ($"Runtime for {supportedAbis} was not available.");
				b.ThrowOnBuildFailure = false;
				Assert.AreEqual (expectedResult, b.Build (proj), "Build should have {0}.", expectedResult ? "succeeded" : "failed");
				if (!expectedResult)
					return;
				foreach (var abi in supportedAbis.Split (new char [] { ';' })) {
					var libapp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"bundles", abi, "libmonodroid_bundle_app.so");
					Assert.IsTrue (File.Exists (libapp), abi + " libmonodroid_bundle_app.so does not exist");
					var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"aot", abi, "libaot-UnnamedProject.dll.so");
					Assert.IsTrue (File.Exists (assemblies), "{0} libaot-UnnamedProject.dll.so does not exist", abi);
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.OutputPath, $"{proj.PackageName}-Signed.apk");
					var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
					Assert.IsFalse (helper.Exists ("assemblies/UnnamedProject.dll"), $"UnnamedProject.dll should not be in the {proj.PackageName}-Signed.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							$"lib/{0}/libaot-UnnamedProject.dll.so should be in the {proj.PackageName}-Signed.apk", abi);
					}
				}
				Assert.AreEqual (expectedResult, b.Build (proj), "Second Build should have {0}.", expectedResult ? "succeeded" : "failed");
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
		[Category ("SmokeTests")]
		public void BuildAMassiveApp ()
		{
			var testPath = Path.Combine ("temp", "BuildAMassiveApp");
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = Path.Combine (Root, testPath);
			var sb = new SolutionBuilder ("BuildAMassiveApp.sln") {
				SolutionPath = Path.Combine (Root, testPath),
			};
			var app1 = new XamarinFormsMapsApplicationProject {
				TargetFrameworkVersion = sb.LatestTargetFrameworkVersion (),
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
					TargetFrameworkVersion = sb.LatestTargetFrameworkVersion (),
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
		[Category ("HybridAOT")]
		public void HybridAOT ([Values ("armeabi-v7a;arm64-v8a", "armeabi-v7a", "arm64-v8a")] string abis)
		{
			// There's no point in testing all of the ABIs with and without assembly blobs, let's test just one of them this way
			bool usesAssemblyBlobs = String.Compare ("arm64-v8a", abis, StringComparison.Ordinal) == 0;
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
			};
			proj.SetProperty ("AndroidAotMode", "Hybrid");
			// So we can use Mono.Cecil to open assemblies directly
			proj.SetProperty ("AndroidEnableAssemblyCompression", "False");
			proj.SetProperty ("AndroidUseAssemblyStore", usesAssemblyBlobs.ToString ());
			proj.SetAndroidSupportedAbis (abis);

			using (var b = CreateApkBuilder ()) {

				if (abis == "armeabi-v7a") {
					proj.SetProperty ("_AndroidAotModeValidateAbi", "False");
					b.Build (proj);
					proj.SetProperty ("_AndroidAotModeValidateAbi", () => null);
				}

				if (abis.Contains ("armeabi-v7a")) {
					b.ThrowOnBuildFailure = false;
					Assert.IsFalse (b.Build (proj), "Build should have failed.");
					string error = b.LastBuildOutput
							.SkipWhile (x => !x.StartsWith ("Build FAILED.", StringComparison.Ordinal))
							.FirstOrDefault (x => x.Contains ("error XA1025:"));
					Assert.IsNotNull (error, "Build should have failed with XA1025.");
					return;
				}

				b.Build (proj);

				var apk = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				FileAssert.Exists (apk);
				var helper = new ArchiveAssemblyHelper (apk, usesAssemblyBlobs);
				Assert.IsTrue (helper.Exists ($"assemblies/{proj.ProjectName}.dll"), $"{proj.ProjectName}.dll should exist in apk!");

				using (var stream = helper.ReadEntry ($"assemblies/{proj.ProjectName}.dll")) {
					stream.Position = 0;
					using (var assembly = AssemblyDefinition.ReadAssembly (stream)) {
						var type = assembly.MainModule.GetType ($"{proj.ProjectName}.MainActivity");
						var method = type.Methods.First (m => m.Name == "OnCreate");
						Assert.LessOrEqual (method.Body.Instructions.Count, 1, "OnCreate should have stripped method bodies!");
					}
				}
			}
		}

		[Test]
		[Category ("LLVM")]
		public void NoSymbolsArgShouldReduceAppSize ([Values ("", "Hybrid")] string androidAotMode, [Values (false, true)] bool skipDebugSymbols)
		{
			AssertAotModeSupported (androidAotMode);

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
			};
			var supportedAbi = "arm64-v8a";
			proj.SetAndroidSupportedAbis (supportedAbi);
			proj.SetProperty ("EnableLLVM", true.ToString ());
			if (!string.IsNullOrEmpty (androidAotMode))
				proj.SetProperty ("AndroidAotMode", androidAotMode);

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

	}
}
