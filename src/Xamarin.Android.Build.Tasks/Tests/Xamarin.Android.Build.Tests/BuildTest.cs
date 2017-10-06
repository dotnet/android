﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public partial class BuildTest : BaseTest
	{
		[Test]
		public void BuildBasicApplication ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplication")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BuildBasicApplicationRelease ()
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationRelease")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[Category ("Minor")]
		public void BuildBasicApplicationFSharp ()
		{
			var proj = new XamarinAndroidApplicationProject () { Language = XamarinAndroidProjectLanguage.FSharp };
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationFSharp")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[Category ("Minor")]
		public void BuildBasicApplicationReleaseFSharp ()
		{
			var proj = new XamarinAndroidApplicationProject () { Language = XamarinAndroidProjectLanguage.FSharp, IsRelease = true };
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationReleaseFSharp")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void FSharpAppHasAndroidDefine ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				Language  = XamarinAndroidProjectLanguage.FSharp,
			};
			proj.Sources.Add (new BuildItem ("Compile", "IsAndroidDefined.fs") {
				TextContent = () => @"
module Xamarin.Android.Tests
// conditional compilation; can we elicit a compile-time error?
let x =
#if __ANDROID__
  42
#endif  // __ANDROID__

printf ""%d"" x
",
			});
			using (var b = CreateApkBuilder ("temp/" + nameof (FSharpAppHasAndroidDefine))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BuildApplicationAndClean ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				var files = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories)
					.Where (x => !Path.GetFileName (x).StartsWith ("TemporaryGeneratedFile"));
				Assert.AreEqual (0, files.Count (), "{0} should be Empty. Found {1}", proj.IntermediateOutputPath, string.Join (Environment.NewLine, files));
				files = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.OutputPath), "*", SearchOption.AllDirectories)
					.Where (x => !Path.GetFileName (x).StartsWith ("TemporaryGeneratedFile"));
				Assert.AreEqual (0, files.Count (), "{0} should be Empty. Found {1}", proj.OutputPath, string.Join (Environment.NewLine, files));
			}
		}

		[Test]
		public void BuildApplicationWithLibraryAndClean ([Values (false, true)] bool isRelease)
		{
			var lib = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
				ProjectName = "Library1",
				OtherBuildItems = {
					new AndroidItem.AndroidAsset ("Assets\\somefile.txt") {
						TextContent =  () => "some readonly file...",
						Attributes = FileAttributes.ReadOnly,
					},
				},
			};
			for (int i = 0; i < 1000; i++) {
				lib.OtherBuildItems.Add (new AndroidItem.AndroidAsset (string.Format ("Assets\\somefile{0}.txt", i)) {
					TextContent = () => "some readonly file...",
					Attributes = FileAttributes.ReadOnly | FileAttributes.Normal,
				});
				lib.AndroidResources.Add (new AndroidItem.AndroidResource (string.Format ("Resources\\values\\Strings{0}.xml", i)) {
					TextContent = () => string.Format (@"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello{0}"">Hello World, Click Me! {0}</string>
</resources>", i++),
					Attributes = FileAttributes.ReadOnly | FileAttributes.Normal,
				});
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				ProjectName = "App1",
				References = { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj") },
			};
			var projectPath = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			using (var libb = CreateDllBuilder (Path.Combine (projectPath, lib.ProjectName), false, false)) {
				Assert.IsTrue (libb.Build (lib), "Build of library should have succeeded");
				using (var b = CreateApkBuilder (Path.Combine (projectPath, proj.ProjectName), false, false)) {
					Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
					//var fi = new FileInfo (Path.Combine (b.ProjectDirectory, proj.IntermediateOutputPath,
					//	"__library_projects__", "Library1", "library_project_imports", ""));
					//fi.Attributes != FileAttributes.ReadOnly;
					Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
					var fileCount = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories)
						.Where (x => !Path.GetFileName (x).StartsWith ("TemporaryGeneratedFile")).Count ();
					Assert.AreEqual (0, fileCount, "{0} should be Empty", proj.IntermediateOutputPath);
					fileCount = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.OutputPath), "*", SearchOption.AllDirectories)
						.Where (x => !Path.GetFileName (x).StartsWith ("TemporaryGeneratedFile")).Count ();
					Assert.AreEqual (0, fileCount, "{0} should be Empty", proj.OutputPath);
				}
			}
		}

		[Test]
		public void BuildMkBundleApplicationRelease ()
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true, BundleAssemblies = true };
			using (var b = CreateApkBuilder ("temp/BuildMkBundleApplicationRelease", false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					"bundles", "armeabi-v7a", "assemblies.o");
				Assert.IsTrue (File.Exists (assemblies), "assemblies.o does not exist");
				var libapp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
					"bundles", "armeabi-v7a", "libmonodroid_bundle_app.so");
				Assert.IsTrue (File.Exists (libapp), "libmonodroid_bundle_app.so does not exist");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
						"lib/armeabi-v7a/libmonodroid_bundle_app.so"),
						"lib/armeabi-v7a/libmonodroid_bundle_app.so should be in the UnnamedProject.UnnamedProject.apk");
					Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
						Path.Combine ("assemblies", "UnnamedProject.dll")),
						"UnnamedProject.dll should not be in the UnnamedProject.UnnamedProject.apk");
				}
			}
		}

		[Test]
		[Category ("Minor")]
		public void BuildMkBundleApplicationReleaseAllAbi ()
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true, BundleAssemblies = true };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			using (var b = CreateApkBuilder ("temp/BuildMkBundleApplicationReleaseAllAbi", false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				foreach (var abi in new string [] { "armeabi-v7a", "x86" }) {
					var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"bundles", abi, "assemblies.o");
					Assert.IsTrue (File.Exists (assemblies), abi + " assemblies.o does not exist");
					var libapp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"bundles", abi, "libmonodroid_bundle_app.so");
					Assert.IsTrue (File.Exists (libapp), abi + " libmonodroid_bundle_app.so does not exist");
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"lib/" + abi + "/libmonodroid_bundle_app.so"),
							"lib/{0}/libmonodroid_bundle_app.so should be in the UnnamedProject.UnnamedProject.apk", abi);
						Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
							Path.Combine ("assemblies", "UnnamedProject.dll")),
							"UnnamedProject.dll should not be in the UnnamedProject.UnnamedProject.apk");
					}
				}
			}
		}

		[Test]
		[TestCaseSource ("AotChecks")]
		[Category ("Minor")]
		public void BuildAotApplication (string supportedAbis, bool enableLLVM, bool expectedResult)
		{
			var path = Path.Combine ("temp", string.Format ("BuildAotApplication_{0}_{1}_{2}", supportedAbis, enableLLVM, expectedResult));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				BundleAssemblies = false,
				AotAssemblies = true,
			};
			proj.SetProperty (KnownProperties.TargetFrameworkVersion, "v5.1");
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, supportedAbis);
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
			bool checkMinLlvmPath = enableLLVM && (supportedAbis == "armeabi-v7a" || supportedAbis == "x86");
			if (checkMinLlvmPath) {
				// Set //uses-sdk/@android:minSdkVersion so that LLVM uses the right libc.so
				proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:minSdkVersion=""10"" />
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
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.AreEqual (expectedResult, b.Build (proj), "Build should have {0}.", expectedResult ? "succeeded" : "failed");
				if (!expectedResult)
					return;
				if (checkMinLlvmPath) {
					// LLVM passes a direct path to libc.so, and we need to use the libc.so
					// which corresponds to the *minimum* SDK version specified in AndroidManifest.xml
					// Since we overrode minSdkVersion=10, that means we should use libc.so from android-9.
					var rightLibc   = new Regex (@"^\s*\[AOT\].*cross-.*--llvm.*,ld-flags=.*android-9.arch-.*.usr.lib.libc\.so", RegexOptions.Multiline);
					var m           = rightLibc.Match (b.LastBuildOutput);
					Assert.IsTrue (m.Success, "AOT+LLVM should use libc.so from minSdkVersion!");
				}
				foreach (var abi in supportedAbis.Split (new char [] { ';' })) {
					var libapp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"bundles", abi, "libmonodroid_bundle_app.so");
					Assert.IsFalse (File.Exists (libapp), abi + " libmonodroid_bundle_app.so should not exist");
					var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
						"aot", abi, "libaot-UnnamedProject.dll.so");
					Assert.IsTrue (File.Exists (assemblies), "{0} libaot-UnnamedProject.dll.so does not exist", abi);
					var apk = Path.Combine (Root, b.ProjectDirectory,
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							"lib/{0}/libaot-UnnamedProject.dll.so should be in the UnnamedProject.UnnamedProject.apk", abi);
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/UnnamedProject.dll"),
							"UnnamedProject.dll should be in the UnnamedProject.UnnamedProject.apk");
					}
				}
				Assert.AreEqual (expectedResult, b.Build (proj), "Second Build should have {0}.", expectedResult ? "succeeded" : "failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CompileJava\" because"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_BuildApkEmbed\" because"),
					"the _BuildApkEmbed target should be skipped");
			}
		}

		[Test]
		[TestCaseSource ("AotChecks")]
		[Category ("Minor")]
		public void BuildAotApplicationAndBundle (string supportedAbis, bool enableLLVM, bool expectedResult)
		{
			var path = Path.Combine ("temp", string.Format ("BuildAotApplicationAndBundle_{0}_{1}_{2}", supportedAbis, enableLLVM, expectedResult));
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				BundleAssemblies = true,
				AotAssemblies = true,
			};
			proj.SetProperty (KnownProperties.TargetFrameworkVersion, "v5.1");
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, supportedAbis);
			proj.SetProperty ("EnableLLVM", enableLLVM.ToString ());
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
						proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
					using (var zipFile = ZipHelper.OpenZip (apk)) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
							"lib/{0}/libaot-UnnamedProject.dll.so should be in the UnnamedProject.UnnamedProject.apk", abi);
						Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/UnnamedProject.dll"),
							"UnnamedProject.dll should not be in the UnnamedProject.UnnamedProject.apk");
					}
				}
				Assert.AreEqual (expectedResult, b.Build (proj), "Second Build should have {0}.", expectedResult ? "succeeded" : "failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CompileJava\" because"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_BuildApkEmbed\" because"),
					"the _BuildApkEmbed target should be skipped");
			}
		}

		[Test]
		[TestCaseSource ("ProguardChecks")]
		public void BuildProguardEnabledProject (bool isRelease, bool enableProguard, bool useLatestSdk)
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = isRelease, EnableProguard = enableProguard, UseLatestPlatformSdk = useLatestSdk, TargetFrameworkVersion = useLatestSdk ? "v7.1" : "v5.0" };
			using (var b = CreateApkBuilder (Path.Combine ("temp", $"BuildProguard Enabled Project(1){isRelease}{enableProguard}{useLatestSdk}"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		XamarinAndroidApplicationProject CreateMultiDexRequiredApplication (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
		{
			var proj = new XamarinAndroidApplicationProject (debugConfigurationName, releaseConfigurationName);
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods.java") {
				TextContent = () => "public class ManyMethods { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods2.java") {
				TextContent = () => "public class ManyMethods2 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "}",
				Encoding = Encoding.ASCII
			});
			return proj;
		}

		[Test]
		[Category ("Minor")]
		public void BuildApplicationOver65536Methods ()
		{
			var proj = CreateMultiDexRequiredApplication ();
			using (var b = CreateApkBuilder ("temp/BuildApplicationOver65536Methods")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Without MultiDex option, build should fail");
				b.Clean (proj);
			}
		}

		[Test]
		public void CreateMultiDexWithSpacesInConfig ()
		{
			var proj = CreateMultiDexRequiredApplication (releaseConfigurationName: "Test Config");
			proj.IsRelease = true;
			proj.SetProperty ("AndroidEnableMultiDex", "True");
			using (var b = CreateApkBuilder ("temp/CreateMultiDexWithSpacesInConfig")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		[TestCaseSource ("JackFlagAndFxVersion")]
		public void BuildMultiDexApplication (bool useJackAndJill, string fxVersion)
		{
			var proj = CreateMultiDexRequiredApplication ();
			proj.UseLatestPlatformSdk = false;
			proj.SetProperty ("AndroidEnableMultiDex", "True");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				proj.TargetFrameworkVersion = b.LatestTargetFrameworkVersion ();
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/bin/classes.dex")),
					"multidex-ed classes.zip exists");
				var multidexKeepPath  = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "multidex.keep");
				Assert.IsTrue (File.Exists (multidexKeepPath), "multidex.keep exists");
				Assert.IsTrue (File.ReadAllLines (multidexKeepPath).Length > 1, "multidex.keep must contain more than one line.");
				Assert.IsTrue (b.LastBuildOutput.Contains (Path.Combine (proj.TargetFrameworkVersion, "mono.android.jar")), proj.TargetFrameworkVersion + "/mono.android.jar should be used.");
			}
		}

		[Test]
		public void MultiDexCustomMainDexFileList ()
		{
			var proj = CreateMultiDexRequiredApplication ();
			proj.SetProperty ("AndroidEnableMultiDex", "True");
			proj.OtherBuildItems.Add (new BuildItem ("MultiDexMainDexList", "mymultidex.keep") { TextContent = () => "MyTest", Encoding = Encoding.ASCII });
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaSource", "MyTest.java") { TextContent = () => "public class MyTest {}", Encoding = Encoding.ASCII });
			var b = CreateApkBuilder ("temp/MultiDexCustomMainDexFileList");
			b.ThrowOnBuildFailure = false;
			Assert.IsTrue (b.Build (proj), "build should succeed. Run will fail.");
			Assert.AreEqual ("MyTest", File.ReadAllText (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "multidex.keep")), "unexpected multidex.keep content");
			b.Clean (proj);
			b.Dispose ();
		}

		[Test]
		public void CustomApplicationClassAndMultiDex ()
		{
			var proj = CreateMultiDexRequiredApplication ();
			proj.SetProperty ("AndroidEnableMultiDex", "True");
			proj.Sources.Add (new BuildItem ("Compile", "CustomApp.cs") { TextContent = () => @"
using System;
using Android.App;
using Android.Runtime;
namespace UnnamedProject {
    [Application(Name = ""com.foxsports.test.CustomApp"")]
    public class CustomApp : Application
    {
        public CustomApp(IntPtr handle, JniHandleOwnership ownerShip) :
			base(handle, ownerShip)
		{


        }

        public override void OnCreate()
        {
            base.OnCreate();
        }
    }
}" });
			using (var b = CreateApkBuilder ("temp/CustomApplicationClassAndMultiDex")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BasicApplicationRepetitiveBuild ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BasicApplicationRepetitiveBuild", cleanupAfterSuccessfulBuild: false)) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var firstBuildTime = b.LastBuildTime;
				Assert.IsTrue (b.Build (proj), "second build failed");
				Assert.IsTrue (
					firstBuildTime > b.LastBuildTime, "Second build ({0}) should have been faster than the first ({1})",
					b.LastBuildTime, firstBuildTime
				);
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"),
					"the _Sign target should not run");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_StripEmbeddedLibraries\" because"),
					"the _StripEmbeddedLibraries target should not run");
				proj.AndroidResources.First ().Timestamp = null;
				Assert.IsTrue (b.Build (proj), "third build failed");
				Assert.IsFalse (
					b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"),
					"the _Sign target should run");
			}
		}

		[Test]
		public void BasicApplicationRepetitiveReleaseBuild ()
		{
			var proj = new XamarinAndroidApplicationProject () { IsRelease = true };
			using (var b = CreateApkBuilder ("temp/BasicApplicationRepetitiveReleaseBuild", cleanupAfterSuccessfulBuild: false)) {
				var foo = new BuildItem.Source ("Foo.cs") {
					TextContent = () => @"using System;
	namespace UnnamedProject {
		public class Foo {
		}
	}"
				};
				proj.Sources.Add (foo);
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "first build failed");
				var firstBuildTime = b.LastBuildTime;
				Assert.IsTrue (b.Build (proj), "second build failed");
				Assert.IsTrue (
					firstBuildTime > b.LastBuildTime, "Second build ({0}) should have been faster than the first ({1})",
					b.LastBuildTime, firstBuildTime
				);
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"),
					"the _Sign target should not run");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_StripEmbeddedLibraries\" because"),
					"the _StripEmbeddedLibraries target should not run");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_LinkAssembliesShrink\" because"),
					"the _LinkAssembliesShrink target should not run");
				foo.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj), "third build failed");
				Assert.IsTrue (b.LastBuildOutput.Contains ("Target CoreCompile needs to be built as input file ") ||
		    b.LastBuildOutput.Contains ("Building target \"CoreCompile\" completely."),
					"the Core Compile target should run");
				Assert.IsFalse (
					b.LastBuildOutput.Contains ("Skipping target \"_Sign\" because"),
					"the _Sign target should run");
			}
		}

		[Test]
		public void BuildBasicApplicationCheckMdb ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckMdb", false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.dll.mdb")) ||
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.pdb")),
					"UnnamedProject.dll.mdb must be copied to the Intermediate directory");
			}
		}

		[Test]
		public void BuildBasicApplicationCheckMdbRepeatBuild ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckMdbRepeatBuild", false)) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.dll.mdb")) ||
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.pdb")),
					"UnnamedProject.dll.mdb must be copied to the Intermediate directory");
				Assert.IsTrue (b.Build (proj), "second build failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should be skipped");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.dll.mdb")) ||
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.pdb")),
					"UnnamedProject.dll.mdb must be copied to the Intermediate directory");
			}
		}

		[Test]
		public void BuildAppCheckDebugSymbols ()
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var lib = new XamarinAndroidLibraryProject () {
				IsRelease = false,
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("Class1.cs") {
						TextContent = () => @"using System;
namespace Library1 {
	public class Class1 : Java.Lang.Object, global::Android.Views.View.IOnClickListener {
		void global::Android.Views.View.IOnClickListener.OnClick(global::Android.Views.View v)
		{
		}
	}
}
",
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
				ProjectName = "App1",
				References = { new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj") },
				Sources = {
					new BuildItem.Source ("Class2.cs") {
						TextContent= () => @"
using System;
namespace App1
{
	public class Class2
	{
		Library1.Class1 c;
		public Class2 ()
		{
		}
	}
}"
					},
				},
			};
			proj.SetProperty (KnownProperties.AndroidLinkMode, AndroidLinkMode.None.ToString ());
			using (var libb = CreateDllBuilder (Path.Combine (path, "Library1"))) {
				Assert.IsTrue (libb.Build (lib), "Library1 Build should have succeeded.");
				using (var b = CreateApkBuilder (Path.Combine (path, "App1"))) {
					Assert.IsTrue (b.Build (proj), "App1 Build should have succeeded.");
					var assetsPdb = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "Library1.pdb");
					var linkDst = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linkdst", "Library1.pdb");
					var linkSrc = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linksrc", "Library1.pdb");
					Assert.IsTrue (
						File.Exists (assetsPdb),
						"Library1.pdb must be copied to Intermediate directory");
					Assert.IsTrue (
						File.Exists (linkDst),
						"Library1.pdb must be copied to linkdst directory");
					Assert.IsTrue (
						File.Exists (linkSrc),
						"Library1.pdb must be copied to linksrc directory");
					FileAssert.AreEqual (linkDst, assetsPdb, $"Library1.pdb in {assetsPdb} should match {linkDst}");
				}
			}
		}

		[Test]
		public void BuildBasicApplicationCheckMdbAndPortablePdb ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckMdbAndPortablePdb")) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				var reference = new BuildItem.Reference ("PdbTestLibrary.dll") {
					WebContent = "https://www.dropbox.com/s/s4br29kvuy8ygz1/PdbTestLibrary.dll?dl=1"
				};
				proj.References.Add (reference);
				var pdb = new BuildItem.NoActionResource ("PdbTestLibrary.pdb") {
					WebContent = "https://www.dropbox.com/s/033jif54ma0e01m/PdbTestLibrary.pdb?dl=1"
				};
				proj.References.Add (pdb);
				var netStandardRef = new BuildItem.Reference ("NetStandard16.dll") {
					WebContent = "https://www.dropbox.com/s/g7v0d4irzvaw5pl/NetStandard16.dll?dl=1"
				};
				proj.References.Add (netStandardRef);
				var netStandardpdb = new BuildItem.NoActionResource ("NetStandard16.pdb") {
					WebContent = "https://www.dropbox.com/s/m898ix2m2il631y/NetStandard16.pdb?dl=1"
				};
				proj.References.Add (netStandardpdb);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var pdbToMdbPath = Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.dll.mdb");
				Assert.IsTrue (
					File.Exists (pdbToMdbPath),
					"PdbTestLibrary.dll.mdb must be generated next to the .pdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "UnnamedProject.dll.mdb")) ||
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "UnnamedProject.pdb")),
					"UnnamedProject.dll.mdb/UnnamedProject.pdb must be copied to the Intermediate directory");
				Assert.IsFalse (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.pdb")),
					"PdbTestLibrary.pdb must not be copied to Intermediate directory");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.dll.mdb")),
					"PdbTestLibrary.dll.mdb must be copied to Intermediate directory");
				FileAssert.AreNotEqual (pdbToMdbPath,
					Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.pdb"),
					"The .pdb should NOT match the .mdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "NetStandard16.pdb")),
					"NetStandard16.pdb must be copied to Intermediate directory");
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should be skipped");
				var lastTime = File.GetLastAccessTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.IsFalse (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should not be skipped");
				Assert.Less (lastTime,
					File.GetLastAccessTimeUtc (pdbToMdbPath),
					"{0} should have been updated", pdbToMdbPath);
			}
		}

		[Test]
		public void BuildBasicApplicationCheckConfigFiles ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckConfigFiles", false)) {
				b.Verbosity = Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;
				var config = new BuildItem.NoActionResource ("UnnamedProject.dll.config") {
					TextContent = () => {
						return "<?xml version='1.0' ?><configuration/>";
					},
					Metadata = {
						{ "CopyToOutputDirectory", "PreserveNewest"},
					}
				};
				proj.References.Add (config);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android/assets/UnnamedProject.dll.config")),
					"UnnamedProject.dll.config was must be copied to Intermediate directory");
				Assert.IsTrue (b.Build (proj), "second build failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyConfigFiles\" because"),
					"the _CopyConfigFiles target should be skipped");
			}
		}

		public void BuildApplicationCheckItEmitsAWarningWithContentItems ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildApplicationCheckItEmitsAWarningWithContentItems")) {
				b.ThrowOnBuildFailure = false;
				proj.AndroidResources.Add (new BuildItem.Content ("TestContent.txt") {
					TextContent = () => "Test Content"
				});
				proj.AndroidResources.Add (new BuildItem.Content ("TestContent1.txt") {
					TextContent = () => "Test Content 1"
				});
				Assert.IsTrue (b.Build (proj), "Build should have built successfully");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("TestContent.txt:  warning XA0101: @(Content) build action is not supported"),
					"Build Output did not contain the correct error message");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("TestContent1.txt:  warning XA0101: @(Content) build action is not supported"),
					"Build Output did not contain the correct error message");
			}
		}

		[Test]
		public void BuildApplicationCheckThatAddStaticResourcesTargetDoesNotRerun ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildApplicationCheckThatAddStaticResourcesTargetDoesNotRerun", false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should not have failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Target _AddStaticResources needs to be built as output file") ||
		    b.LastBuildOutput.Contains ("Building target \"_AddStaticResources\" completely."),
					"The _AddStaticResources should have been run");
				Assert.IsTrue (b.Build (proj), "Build should not have failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_AddStaticResources\" because"),
					"The _AddStaticResources should NOT have been run");
			}
		}

		[Test]
		[Ignore ("Re enable when MergeResources work is complete")]
		public void AaptErrorWhenDuplicateStringEntry ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationAaptErrorWithDuplicateEntry")) {
				// Add a library project so that aapt gets multiple resource directory to include
				proj.Packages.Add (KnownPackages.SupportV7CardView);
				proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\ExtraStrings.xml") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?><resources><string name=""Common.None"">None</string><string name=""Common.None"">None</string></resources>",
				});

				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should fail with an aapt error about duplicated string res entries");
				StringAssert.Contains ("Resource entry Common.None is already defined", b.LastBuildOutput);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded");
			}
		}

		[Test]
		public void CheckJavaError ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe.java") {
				TextContent = () => "public classo TestMe { }",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe2.java") {
				TextContent = () => "public class TestMe2 {" +
					"public vod Test ()" +
					"}",
				Encoding = Encoding.ASCII
			});
			using (var b = CreateApkBuilder ("temp/CheckJavaError")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				if (b.IsUnix) {
					var text = "TestMe.java(1,8):  javacerror :  error: class, interface, or enum expected";
					if (b.RunningMSBuild)
						text = "TestMe.java(1,8): javac error :  error: class, interface, or enum expected";
					StringAssert.Contains (text, b.LastBuildOutput);
				} else
					StringAssert.Contains ("TestMe.java(1,8): javac.exe error :  error: class, interface, or enum expected", b.LastBuildOutput);
				StringAssert.Contains ("TestMe2.java(1,41): error :  error: ';' expected", b.LastBuildOutput);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		/// <summary>
		/// Based on issue raised in
		/// https://bugzilla.xamarin.com/show_bug.cgi?id=28224
		/// </summary>
		public void XA5213IsRaisedWhenOutOfMemoryErrorIsThrown ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods.java") {
				TextContent = () => "public class ManyMethods { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods2.java") {
				TextContent = () => "public class ManyMethods2 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "\n}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods3.java") {
				TextContent = () => "public class ManyMethods3 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "\n}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods4.java") {
				TextContent = () => "public class ManyMethods4 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "\n}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods5.java") {
				TextContent = () => "public class ManyMethods5 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "\n}",
				Encoding = Encoding.ASCII
			});
			proj.OtherBuildItems.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "ManyMethods6.java") {
				TextContent = () => "public class ManyMethods6 { \n"
					+ string.Join (Environment.NewLine, Enumerable.Range (0, 32768).Select (i => "public void method" + i + "() {}"))
					+ "\n}",
				Encoding = Encoding.ASCII
			});
			proj.Packages.Add (KnownPackages.AndroidSupportV4_21_0_3_0);
			proj.Packages.Add (KnownPackages.SupportV7AppCompat_21_0_3_0);
			proj.Packages.Add (KnownPackages.SupportV7MediaRouter_21_0_3_0);
			proj.Packages.Add (KnownPackages.GooglePlayServices_22_0_0_2);
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			proj.SetProperty ("AndroidEnableMultiDex", "True");
			proj.SetProperty (proj.DebugProperties, "JavaMaximumHeapSize", "64m");
			proj.SetProperty (proj.ReleaseProperties, "JavaMaximumHeapSize", "64m");
			using (var b = CreateApkBuilder ("temp/XA5213IsRaisedWhenOutOfMemoryErrorIsThrown")) {
				b.ThrowOnBuildFailure = false;
				Assert.IsFalse (b.Build (proj), "Build should have failed.");
				StringAssert.Contains ("XA5213", b.LastBuildOutput);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		/// <summary>
		/// Based on issue raised in
		/// https://bugzilla.xamarin.com/show_bug.cgi?id=28721
		/// </summary>
		public void DuplicateValuesInResourceCaseMap ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				TextContent = () => {
					return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<LinearLayout xmlns:android=\"http://schemas.android.com/apk/res/android\"\n    android:orientation=\"vertical\"\n    android:layout_width=\"fill_parent\"\n    android:layout_height=\"fill_parent\"\n    />";
				}
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				MetadataValues = "Link=Resources\\layout-xhdpi\\test.axml"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
				MetadataValues = "Link=Resources\\layout-xhdpi\\Test.axml"
			});
			using (var b = CreateApkBuilder ("temp/DuplicateValuesInResourceCaseMap")) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				b.ThrowOnBuildFailure = false;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckLintErrorsAndWarnings ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.UseLatestPlatformSdk = true;
			proj.SetProperty ("AndroidLintEnabled", true.ToString ());
			proj.SetProperty ("AndroidLintDisabledIssues", "StaticFieldLeak,ObsoleteSdkInt");
			proj.SetProperty ("AndroidLintEnabledIssues", "");
			proj.SetProperty ("AndroidLintCheckIssues", "");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", @"
		[IntentFilter (new[] { Android.Content.Intent.ActionView },
			Categories = new [] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable },
			DataHost = ""mydomain.com"",
			DataScheme = ""http""
		)]
		public class MainActivity : Activity
			");
			using (var b = CreateApkBuilder ("temp/CheckLintErrorsAndWarnings")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssert.DoesNotContain ("XA0102", b.LastBuildOutput);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckLintConfigMerging ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidLintEnabled", true.ToString ());
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLintConfig ("lint1.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<lint>
	<issue id=""NewApi"" severity=""warning"" />
</lint>"
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidLintConfig ("lint2.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<lint>
	<issue id=""MissingApplicationIcon"" severity=""ignore"" />
</lint>"
			});
			using (var b = CreateApkBuilder ("temp/CheckLintConfigMerging", false, false)) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var lintFile = Path.Combine (Root, "temp", "CheckLintConfigMerging", proj.IntermediateOutputPath, "lint.xml");
				Assert.IsTrue (File.Exists (lintFile), "{0} should have been created.", lintFile);
				var doc = XDocument.Load (lintFile);
				Assert.IsNotNull (doc, "Document should have loaded successfully.");
				Assert.IsNotNull (doc.Element ("lint"), "The xml file should have a lint element.");
				Assert.IsNotNull (doc.Element ("lint")
					.Elements ()
					.Any (x => x.Name == "Issue" && x.Attribute ("id").Value == "MissingApplicationIcon"), "Element is missing");
				Assert.IsNotNull (doc.Element ("lint")
					.Elements ()
					.Any (x => x.Name == "Issue" && x.Attribute ("id").Value == "NewApi"), "Element is missing");
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				Assert.IsFalse (File.Exists (lintFile), "{0} should have been deleted on clean.", lintFile);
			}
		}

		[Test]
		/// <summary>
		/// Reference https://bugzilla.xamarin.com/show_bug.cgi?id=29568
		/// </summary>
		public void BuildLibraryWhichUsesResources ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinAndroidLibraryProject () { IsRelease = isRelease };
			proj.Packages.Add (KnownPackages.AndroidSupportV4_22_1_1_1);
			proj.Packages.Add (KnownPackages.SupportV7AppCompat_22_1_1_1);
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Styles.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<resources>
	<style name=""AppTheme"" parent=""Theme.AppCompat.Light.NoActionBar"" />
</resources>"
			});
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			proj.SetProperty ("AndroidResgenClass", "Resource");
			proj.SetProperty ("AndroidResgenFile", () => "Resources\\Resource.designer" + proj.Language.DefaultExtension);
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

#pragma warning disable 414
		static object [] AndroidStoreKeyTests = new object [] {
						// isRelease, AndroidKeyStore, ExpectedResult
			new object[] { false    , "False"        , "debug.keystore"},
			new object[] { true     , "False"        , "debug.keystore"},
			new object[] { false    , "True"         , "-keystore test.keystore"},
			new object[] { true     , "True"         , "-keystore test.keystore"},
			new object[] { false    , ""             , "debug.keystore"},
			new object[] { true     , ""             , "debug.keystore"},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("AndroidStoreKeyTests")]
		public void TestAndroidStoreKey (bool isRelease, string androidKeyStore, string expected)
		{
			byte [] data;
			using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.test.keystore")) {
				data = new byte [stream.Length];
				stream.Read (data, 0, (int)stream.Length);
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease
			};
			proj.SetProperty ("AndroidKeyStore", androidKeyStore);
			proj.SetProperty ("AndroidSigningKeyStore", "test.keystore");
			proj.SetProperty ("AndroidSigningStorePass", "android");
			proj.SetProperty ("AndroidSigningKeyAlias", "mykey");
			proj.SetProperty ("AndroidSigningKeyPass", "android");
			proj.OtherBuildItems.Add (new BuildItem (BuildActions.None, "test.keystore") {
				BinaryContent = () => data
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				StringAssert.Contains (expected, b.LastBuildOutput,
					"The Wrong keystore was used to sign the apk");
			}
		}

#pragma warning disable 414
		static object [] BuildApplicationWithJavaSourceChecks = new object [] {
			new object[] {
				/* isRelease */           false,
				/* expectedResult */      true,
			},
			new object[] {
				/* isRelease */           true,
				/* expectedResult */      true,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("BuildApplicationWithJavaSourceChecks")]
		public void BuildApplicationWithJavaSource (bool isRelease, bool expectedResult)
		{
			var path = String.Format ("temp/BuildApplicationWithJavaSource_{0}_{1}",
				isRelease, expectedResult);
			try {
				var proj = new XamarinAndroidApplicationProject () {
					IsRelease = isRelease,
					OtherBuildItems = {
						new BuildItem (AndroidBuildActions.AndroidJavaSource, "TestMe.java") {
							TextContent = () => "public class TestMe { }",
							Encoding = Encoding.ASCII
						},
					}
				};
				proj.SetProperty ("TargetFrameworkVersion", "v5.0");
				using (var b = CreateApkBuilder (path)) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					b.ThrowOnBuildFailure = false;
					Assert.AreEqual (expectedResult, b.Build (proj), "Build should have {0}", expectedResult ? "succeeded" : "failed");
					if (expectedResult)
						StringAssert.DoesNotContain ("XA9002", b.LastBuildOutput, "XA9002 should not have been raised");
					else
						StringAssert.Contains ("XA9002", b.LastBuildOutput, "XA9002 should have been raised");
					Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				}
			} finally {
			}
		}

		[Test]
		[TestCaseSource ("RuntimeChecks")]
		public void CheckWhichRuntimeIsIncluded (string[] supportedAbi, bool debugSymbols, string debugType, bool? optimize, bool? embedassebmlies, string expectedRuntime) {
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugSymbols", debugSymbols);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugType", debugType);
			if (optimize.HasValue)
				proj.SetProperty (proj.ActiveConfigurationProperties, "Optimize", optimize.Value);
			else
				proj.RemoveProperty (proj.ActiveConfigurationProperties, "Optimize");
			if (embedassebmlies.HasValue)
				proj.SetProperty (proj.ActiveConfigurationProperties, "EmbedAssembliesIntoApk", embedassebmlies.Value);
			else
				proj.RemoveProperty (proj.ActiveConfigurationProperties, "EmbedAssembliesIntoApk");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				var runtimeInfo = b.GetSupportedRuntimes ();
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apkPath = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath,"android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var apk = ZipHelper.OpenZip (apkPath)) {
					foreach (var abi in supportedAbi) {
						var runtime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Runtime == expectedRuntime);
						Assert.IsNotNull (runtime, "Could not find the expected runtime.");
						var inApk = ZipHelper.ReadFileFromZip (apk, String.Format ("lib/{0}/{1}", abi, runtime.Name));
						var inApkRuntime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Size == inApk.Length);
						Assert.IsNotNull (inApkRuntime, "Could not find the actual runtime used.");
						Assert.AreEqual (runtime.Size, inApkRuntime.Size, "expected {0} got {1}", expectedRuntime, inApkRuntime.Runtime);
						inApk = ZipHelper.ReadFileFromZip (apk, string.Format ("lib/{0}/libmono-profiler-log.so", abi));
						if (string.Compare (expectedRuntime, "debug", StringComparison.OrdinalIgnoreCase) == 0) {
							if (inApk == null)
								Assert.Fail ("libmono-profiler-log.so should exist in the apk.");
						} else {
							if (inApk != null)
								Assert.Fail ("libmono-profiler-log.so should not exist in the apk.");
						}
					}
				}
			}
		}

		[Test]
		[TestCaseSource ("SequencePointChecks")]
		public void CheckSequencePointGeneration (bool isRelease, bool monoSymbolArchive, bool aotAssemblies,
			bool debugSymbols, string debugType, bool embedMdb, string expectedRuntime)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				AotAssemblies = aotAssemblies
			};
			var abis = new string [] { "armeabi-v7a", "x86" };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			proj.SetProperty (proj.ActiveConfigurationProperties, "MonoSymbolArchive", monoSymbolArchive);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugSymbols", debugSymbols);
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugType", debugType);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				if (aotAssemblies && !b.CrossCompilerAvailable (string.Join (";", abis)))
					Assert.Ignore ("Cross compiler was not available");
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				var msymarchive = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, proj.PackageName + ".apk.mSYM");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					var mdbExits = ZipHelper.ReadFileFromZip (zipFile, "assemblies/UnnamedProject.dll.mdb") != null ||
						ZipHelper.ReadFileFromZip (zipFile, "assemblies/UnnamedProject.pdb") != null;
					Assert.AreEqual (embedMdb, mdbExits,
						"assemblies/UnnamedProject.dll.mdb or assemblies/UnnamedProject.pdb should{0}be in the UnnamedProject.UnnamedProject.apk", embedMdb ? " " : " not ");
					if (aotAssemblies) {
						foreach (var abi in abis) {
							var assemblies = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
								"aot", abi, "libaot-UnnamedProject.dll.so");
							var shouldExist = monoSymbolArchive && debugSymbols && debugType == "PdbOnly";
							var symbolicateFile = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath,
								"aot", abi), "UnnamedProject.dll.msym", SearchOption.AllDirectories).FirstOrDefault ();
							if (shouldExist)
								Assert.IsNotNull (symbolicateFile, "UnnamedProject.dll.msym should exist");
							else
								Assert.IsNull (symbolicateFile, "{0} should not exist", symbolicateFile);
							if (shouldExist) {
								var foundMsyms = Directory.GetFiles (Path.Combine (msymarchive), "UnnamedProject.dll.msym", SearchOption.AllDirectories).Any ();
								Assert.IsTrue (foundMsyms, "UnnamedProject.dll.msym should exist in the archive {0}", msymarchive);
							}
							Assert.IsTrue (File.Exists (assemblies), "{0} libaot-UnnamedProject.dll.so does not exist", abi);
							Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
								string.Format ("lib/{0}/libaot-UnnamedProject.dll.so", abi)),
								"lib/{0}/libaot-UnnamedProject.dll.so should be in the UnnamedProject.UnnamedProject.apk", abi);
							Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
								"assemblies/UnnamedProject.dll"),
								"UnnamedProject.dll should be in the UnnamedProject.UnnamedProject.apk");
						}
					}
					var runtimeInfo = b.GetSupportedRuntimes ();
					foreach (var abi in abis) {
						var runtime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Runtime == expectedRuntime);
						Assert.IsNotNull (runtime, "Could not find the expected runtime.");
						var inApk = ZipHelper.ReadFileFromZip (apk, String.Format ("lib/{0}/{1}", abi, runtime.Name));
						var inApkRuntime = runtimeInfo.FirstOrDefault (x => x.Abi == abi && x.Size == inApk.Length);
						Assert.IsNotNull (inApkRuntime, "Could not find the actual runtime used.");
						Assert.AreEqual (runtime.Size, inApkRuntime.Size, "expected {0} got {1}", expectedRuntime, inApkRuntime.Runtime);
					}
				}
				b.Clean (proj);
				Assert.IsTrue (!Directory.Exists (msymarchive), "{0} should have been deleted on Clean", msymarchive);
			}
		}

		[Test]
		public void BuildApplicationWithMonoEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				OtherBuildItems = { new AndroidItem.AndroidEnvironment ("Mono.env") {
						TextContent = () => "MONO_DEBUG=soft-breakpoints"
					},
				},
			};
			proj.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					var data = ZipHelper.ReadFileFromZip (zipFile, "environment");
					Assert.IsNotNull (data, "environment should exist in the apk.");
					var env = Encoding.ASCII.GetString (data);
					var lines = env.Split (new char [] { '\n' });

					Assert.IsTrue (lines.Any (x => x.Contains ("MONO_DEBUG") &&
						x.Contains ("soft-breakpoints") &&
						string.IsNullOrEmpty (sequencePointsMode) ? true : x.Contains ("gen-compact-seq-points")),
						"The values from Mono.env should have been merged into environment");
				}
			}
		}

		[Test]
		public void CheckMonoDebugIsAddedToEnvironment ([Values ("", "Normal", "Offline")] string sequencePointsMode)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("_AndroidSequencePointsMode", sequencePointsMode);
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					var data = ZipHelper.ReadFileFromZip (zipFile, "environment");
					Assert.IsNotNull (data, "environment should exist in the apk.");
					var env = Encoding.ASCII.GetString (data);
					var lines = env.Split (new char [] { '\n' });

					Assert.IsTrue (lines.Any (x =>
						string.IsNullOrEmpty (sequencePointsMode)
							? !x.Contains ("MONO_DEBUG")
							: x.Contains ("MONO_DEBUG") && x.Contains ("gen-compact-seq-points")),
						"environment {0} contain MONO_DEBUG=gen-compact-seq-points",
						string.IsNullOrEmpty (sequencePointsMode) ? "should not" : "should");
				}
			}
		}

		[Test]
		public void BuildWithNativeLibraries ([Values (true, false)] bool isRelease)
		{
			var dll = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.EmbeddedNativeLibrary ("foo\\armeabi-v7a\\libtest.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\armeabi-v7a\\libtest.so",
					},
					new AndroidItem.EmbeddedNativeLibrary ("foo\\x86\\libtest.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\x86\\libtest.so",
					},
				},
			};
			var dll2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.EmbeddedNativeLibrary ("foo\\armeabi-v7a\\libtest1.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\armeabi-v7a\\libtest1.so",
					},
					new AndroidItem.EmbeddedNativeLibrary ("foo\\x86\\libtest1.so") {
						BinaryContent = () => new byte[10],
						MetadataValues = "Link=libs\\x86\\libtest1.so",
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference","..\\Library2\\Library2.csproj"),
				},
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary ("armeabi-v7a\\libRSSupport.so") {
						BinaryContent = () => new byte[10],
					},
				},
				Packages = {
					KnownPackages.Xamarin_Android_Support_v8_RenderScript_23_1_1_0,
				}
			};
			proj.SetProperty ("TargetFrameworkVersion", "v7.1");
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");
			var path = Path.Combine (Root, "temp", string.Format ("BuildWithNativeLibraries_{0}", isRelease));
			using (var b1 = CreateDllBuilder (Path.Combine (path, dll2.ProjectName))) {
				b1.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b1.Build (dll2), "Build should have succeeded.");
				using (var b = CreateDllBuilder (Path.Combine (path, dll.ProjectName))) {
					b.Verbosity = LoggerVerbosity.Diagnostic;
					Assert.IsTrue (b.Build (dll), "Build should have succeeded.");
					using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName))) {
						builder.Verbosity = LoggerVerbosity.Diagnostic;
						Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
						var apk = Path.Combine (Root, builder.ProjectDirectory,
							proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
						StringAssert.Contains ("XA4301", builder.LastBuildOutput, "warning about skipping libRSSupport.so should have been raised");
						using (var zipFile = ZipHelper.OpenZip (apk)) {
							var data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libtest.so");
							Assert.IsNotNull (data, "libtest.so for x86 should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libtest.so");
							Assert.IsNotNull (data, "libtest.so for armeabi-v7a should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/x86/libtest1.so");
							Assert.IsNotNull (data, "libtest1.so for x86 should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libtest1.so");
							Assert.IsNotNull (data, "libtest1.so for armeabi-v7a should exist in the apk.");
							data = ZipHelper.ReadFileFromZip (zipFile, "lib/armeabi-v7a/libRSSupport.so");
							Assert.IsNotNull (data, "libRSSupport.so for armeabi should exist in the apk.");
						}
					}
				}
			}
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void BuildWithExternalJavaLibrary ()
		{
			var path = Path.Combine ("temp", "BuildWithExternalJavaLibrary");
			string multidex_jar = @"$(MonoDroidInstallDirectory)\lib\xamarin.android\xbuild\Xamarin\Android\android-support-multidex.jar";
			var binding = new XamarinAndroidBindingProject () {
				ProjectName = "BuildWithExternalJavaLibraryBinding",
				Jars = { new AndroidItem.InputJar (() => multidex_jar), },
				AndroidClassParser = "class-parse",
			};
			using (var bbuilder = CreateDllBuilder (Path.Combine (path, "BuildWithExternalJavaLibraryBinding"))) {
				Assert.IsTrue (bbuilder.Build (binding));
				var proj = new XamarinAndroidApplicationProject () {
					References = { new BuildItem ("ProjectReference", "..\\BuildWithExternalJavaLibraryBinding\\BuildWithExternalJavaLibraryBinding.csproj"), },
					OtherBuildItems = { new BuildItem ("AndroidExternalJavaLibrary", multidex_jar) },
					Sources = { new BuildItem ("Compile", "Foo.cs") {
							TextContent = () => "public class Foo { public void X () { new Android.Support.Multidex.MultiDexApplication (); } }"
						} },
				};
				using (var builder = CreateApkBuilder (Path.Combine (path, "BuildWithExternalJavaLibrary"))) {
					Assert.IsTrue (builder.Build (proj));
				}
			}
		}

		[Test]
		public void CheckItemMetadata ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				Imports = {
					new Import (() => "My.Test.target") {
						TextContent = () => @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	<Target Name=""CustomTarget"" AfterTargets=""UpdateAndroidAssets"" BeforeTargets=""UpdateAndroidInterfaceProxies"" >
		<Message Text=""Foo""/>
		<Message Text=""@(_AndroidAssetsDest->'%(CustomData)')"" />
	</Target>
	<Target Name=""CustomTarget2"" AfterTargets=""UpdateAndroidResources"" >
		<Message Text=""@(_AndroidResourceDest->'%(CustomData)')"" />
	</Target>
</Project>
						"
					},
				},
				OtherBuildItems = {
					new AndroidItem.AndroidAsset (() => "Assets\\foo.txt") {
						TextContent = () => "Foo",
						MetadataValues = "CustomData=AssetMetaDataOK"
					},
				}
			};

			var mainAxml = proj.AndroidResources.First (x => x.Include () == "Resources\\layout\\Main.axml");
			mainAxml.MetadataValues = "CustomData=ResourceMetaDataOK";

			using (var builder = CreateApkBuilder (string.Format ("temp/CheckItemMetadata_{0}", isRelease))) {
				builder.Build (proj);
				StringAssert.Contains ("AssetMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidAsset");
				StringAssert.Contains ("ResourceMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidResource");
			}
		}

		// Context https://bugzilla.xamarin.com/show_bug.cgi?id=29706
		[Test]
		public void CheckLogicalNamePathSeperators ([Values (false, true)] bool isRelease)
		{
			var illegalSeperator = IsWindows ? "/" : @"\";
			var dll = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				IsRelease = isRelease,
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\Test\\Test2.png") {
						BinaryContent = () => XamarinAndroidApplicationProject.icon_binary_mdpi,
						MetadataValues = string.Format ("LogicalName=drawable{0}foo2.png", illegalSeperator)
					},
				},
			};
			var proj = new XamarinAndroidApplicationProject () {
				ProjectName = "Application1",
				IsRelease = isRelease,
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\Test\\Test.png") {
						BinaryContent = () => XamarinAndroidApplicationProject.icon_binary_mdpi,
						MetadataValues = string.Format ("LogicalName=drawable{0}foo.png", illegalSeperator)
					},
				},
				References = {
					new BuildItem ("ProjectReference","..\\Library1\\Library1.csproj"),
				},
			};
			var path = Path.Combine ("temp", string.Format ("CheckLogicalNamePathSeperators_{0}", isRelease));
			using (var b = CreateDllBuilder (Path.Combine (path, dll.ProjectName))) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				Assert.IsTrue (b.Build (dll), "Build should have succeeded.");
				using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), isRelease)) {
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
					StringAssert.Contains ("public const int foo = ", File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "Resources", "Resource.designer.cs")));
					StringAssert.Contains ("public const int foo2 = ", File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "Resources", "Resource.designer.cs")));
				}
			}
		}

		[Test]
		public void ApplicationJavaClassProperties ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidApplicationJavaClass", "android.test.mock.MockApplication");
			var builder = CreateApkBuilder ("temp/ApplicationJavaClassProperties");
			builder.Build (proj);
			var appsrc = File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "obj", "Debug", "android", "AndroidManifest.xml"));
			Assert.IsTrue (appsrc.Contains ("android.test.mock.MockApplication"), "app class");
			builder.Dispose ();
		}

		[Test]
		public void ApplicationIdPlaceholder ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AndroidManifest = proj.AndroidManifest.Replace ("</application>", "<provider android:name='${applicationId}' android:authorities='example' /></application>");
			var builder = CreateApkBuilder ("temp/ApplicationIdPlaceholder");
			builder.Build (proj);
			var appsrc = File.ReadAllText (Path.Combine (Root, builder.ProjectDirectory, "obj", "Debug", "android", "AndroidManifest.xml"));
			Assert.IsTrue (appsrc.Contains ("<provider android:name=\"UnnamedProject.UnnamedProject\""), "placeholder not replaced");
			builder.Dispose ();
		}

		[Test]
		public void ResourceExtraction ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				Packages = {
					KnownPackages.AndroidSupportV4_23_1_1_0,
					KnownPackages.SupportV7AppCompat_23_1_1_0,
				},
			};
			proj.SetProperty ("TargetFrameworkVersion", "v5.0");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				var targetAar = Path.Combine (CachePath, "Xamarin.Android.Support.v7.AppCompat", "23.1.1.0",
					"content", "m2repository", "com", "android", "support", "appcompat-v7", "23.1.1", "appcompat-v7-23.1.1.aar");
				if (File.Exists (targetAar)) {
					File.Delete (targetAar);
				}
				var embedded = Path.Combine (CachePath, "Xamarin.Android.Support.v7.AppCompat", "23.1.1.0", "embedded");
				if (Directory.Exists (embedded)) {
					Directory.Delete (embedded, recursive: true);
				}
				Assert.IsTrue (builder.Build (proj), "Second Build should have succeeded");
			}
		}

		[Test]
		public void CheckTargetFrameworkVersion ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty ("TargetFrameworkVersion", "v2.3");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v2.3", builder.LastBuildOutput, "TargetFrameworkVerson should be v2.3");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "TargetFrameworkVersion=v4.4" }), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v4.4", builder.LastBuildOutput, "TargetFrameworkVerson should be v4.4");

			}
		}

#pragma warning disable 414
		public static object [] GeneratorValidateEventNameArgs = new object [] {
			new object [] { false, true, string.Empty, string.Empty },
			new object [] { false, false, "<attr path=\"/api/package/class[@name='Test']/method[@name='setOn123Listener']\" name='eventName'>OneTwoThree</attr>", string.Empty },
			new object [] { true, true, string.Empty, "String s" },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("GeneratorValidateEventNameArgs")]
		public void GeneratorValidateEventName (bool failureExpected, bool warningExpected, string metadataFixup, string methodArgs)
		{
			string java = @"
package com.xamarin.testing;

public class Test
{
	public void setOnAbcListener (OnAbcListener listener)
	{
	}

	public void setOn123Listener (On123Listener listener)
	{
	}

	public interface OnAbcListener
	{
		public void onAbc ();
	}

	public interface On123Listener
	{
		public void onAbc (%%ARGS%%);
	}
}
".Replace ("%%ARGS%%", methodArgs);
			var path = Path.Combine (Root, "temp", $"GeneratorValidateEventName{failureExpected}{warningExpected}");
			var javaDir = Path.Combine (path, "java", "com", "xamarin", "testing");
			if (Directory.Exists (javaDir))
				Directory.Delete (javaDir, true);
			Directory.CreateDirectory (javaDir);
			var proj = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			proj.MetadataXml = "<metadata>" + metadataFixup + "</metadata>";
			proj.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "test.jar")) {
				BinaryContent = new JarContentBuilder () {
					BaseDirectory = Path.Combine (path, "java"),
					JarFileName = "test.jar",
					JavaSourceFileName = Path.Combine ("com", "xamarin", "testing", "Test.java"),
					JavaSourceText = java
				}.Build
			});
			using (var builder = CreateDllBuilder (path, false, false)) {
				bool result = false;
				try {
					result = builder.Build (proj);
					Assert.AreEqual (warningExpected, builder.LastBuildOutput.Contains ("warning BG8504"), "warning BG8504 is expected: " + warningExpected);
				} catch (FailedBuildException) {
					if (!failureExpected)
						throw;
				}
				Assert.AreEqual (failureExpected, !result, "Should build fail?");
			}
		}

#pragma warning disable 414
		public static object [] GeneratorValidateMultiMethodEventNameArgs = new object [] {
			new object [] { false, "BG8505", string.Empty, string.Empty },
			new object [] { false, null, "<attr path=\"/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']\" name='eventName'>One23</attr>", string.Empty },
			new object [] { false, null, @"
					<attr path=""/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']"" name='eventName'>One23</attr>
					<attr path=""/api/package/interface[@name='Test.OnFooListener']/method[@name='on123']"" name='argsType'>OneTwoThreeEventArgs</attr>
				", "String s" },
			new object [] { true, "BG8504", string.Empty, "String s" },
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("GeneratorValidateMultiMethodEventNameArgs")]
		public void GeneratorValidateMultiMethodEventName (bool failureExpected, string expectedWarning, string metadataFixup, string methodArgs)
		{
			string java = @"
package com.xamarin.testing;

public class Test
{
	public void setOnFooListener (OnFooListener listener)
	{
	}

	public interface OnFooListener
	{
		public void onAbc ();
		public void on123 (%%ARGS%%);
	}
}
".Replace ("%%ARGS%%", methodArgs);
			var path = Path.Combine (Root, "temp", $"GeneratorValidateMultiMethodEventName{failureExpected}{expectedWarning}{methodArgs}");
			var javaDir = Path.Combine (path, "java", "com", "xamarin", "testing");
			if (Directory.Exists (javaDir))
				Directory.Delete (javaDir, true);
			Directory.CreateDirectory (javaDir);
			var proj = new XamarinAndroidBindingProject () {
				AndroidClassParser = "class-parse",
			};
			proj.MetadataXml = "<metadata>" + metadataFixup + "</metadata>";
			proj.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "test.jar")) {
				BinaryContent = new JarContentBuilder () {
					BaseDirectory = Path.Combine (path, "java"),
					JarFileName = "test.jar",
					JavaSourceFileName = Path.Combine ("com", "xamarin", "testing", "Test.java"),
					JavaSourceText = java
				}.Build
			});
			using (var builder = CreateDllBuilder (path, false, false)) {
				try {
					builder.Build (proj);
					if (failureExpected)
						Assert.Fail ("Build should fail.");
					if (expectedWarning == null)
						Assert.IsFalse (builder.LastBuildOutput.Contains ("warning BG850"), "warning BG850* is NOT expected");
					else
						Assert.IsTrue (builder.LastBuildOutput.Contains ("warning " + expectedWarning), "warning " + expectedWarning + " is expected.");
				} catch (FailedBuildException) {
					if (!failureExpected)
						throw;
				}
			}
		}

		[Test]
		public void BuildReleaseApplication ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BuildReleaseApplicationWithSpacesInPath ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				AotAssemblies = true,
			};
			proj.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)"+ Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", "BuildReleaseAppWithA InIt(1)"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void BuildReleaseApplicationWithNugetPackages ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				Packages = {
					KnownPackages.AndroidSupportV4_21_0_3_0,
				},
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				DirectoryAssert.Exists (Path.Combine (Root, "temp","packages", "Xamarin.Android.Support.v4.21.0.3.0"),
										"Nuget Package Xamarin.Android.Support.v4.21.0.3.0 should have been restored.");
			}
		}

		static object [] TlsProviderTestCases =
		{
			// androidTlsProvider, isRelease, extpected
			new object[] { "", true, false, },
			new object[] { "default", true, false, },
			new object[] { "legacy", true, false, },
			new object[] { "btls", true, true, }
		};


		[Test]
		[TestCaseSource ("TlsProviderTestCases")]
		public void BuildWithTlsProvider (string androidTlsProvider, bool isRelease, bool expected)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", $"BuildWithTlsProvider_{androidTlsProvider}_{isRelease}_{expected}"))) {
				proj.SetProperty ("AndroidTlsProvider", androidTlsProvider);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var apk = Path.Combine(Root, b.ProjectDirectory,
					proj.IntermediateOutputPath,"android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					if (expected) {
						Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
						   "lib/armeabi-v7a/libmono-btls-shared.so"),
						   "lib/armeabi-v7a/libmono-btls-shared.so should exist in the apk.");
					}
					else {
						Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
						   "lib/armeabi-v7a/libmono-btls-shared.so"),
						   "lib/armeabi-v7a/libmono-btls-shared.so should not exist in the apk.");
					}
				}
			}
		}

		[Test]
		public void BuildAfterAddingNuget ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "7.1",
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				proj.Packages.Add (KnownPackages.SupportV7CardView_24_2_1);
				b.Save (proj, doNotCleanupOnUpdate: true);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				var doc = File.ReadAllText (Path.Combine (b.Root, b.ProjectDirectory, proj.IntermediateOutputPath, "resourcepaths.cache"));
				Assert.IsTrue (doc.Contains ("Xamarin.Android.Support.v7.CardView/24.2.1"), "CardView should be resolved as a reference.");
			}
		}

		[Test]
		public void CheckTargetFrameworkVersion ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("TargetFrameworkVersion", "v2.3");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v2.3", builder.LastBuildOutput, "TargetFrameworkVerson should be v2.3");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "TargetFrameworkVersion=v4.4" }), "Build should have succeeded.");
				StringAssert.Contains ($"TargetFrameworkVersion: v4.4", builder.LastBuildOutput, "TargetFrameworkVerson should be v4.4");
			}
		}

		[Test]
		public void BuildBasicApplicationCheckPdb ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("EmbedAssembliesIntoApk", true.ToString ());
			proj.SetProperty ("AndroidUseSharedRuntime", false.ToString ());
			using (var b = CreateApkBuilder ("temp/BuildBasicApplicationCheckPdb", false, false)) {
				b.Verbosity = LoggerVerbosity.Diagnostic;
				var reference = new BuildItem.Reference ("PdbTestLibrary.dll") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/PdbTestLibrary.dll"
				};
				proj.References.Add (reference);
				var pdb = new BuildItem.NoActionResource ("PdbTestLibrary.pdb") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/PdbTestLibrary.pdb"
				};
				proj.References.Add (pdb);
				var netStandardRef = new BuildItem.Reference ("NetStandard16.dll") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/NetStandard16.dll"
				};
				proj.References.Add (netStandardRef);
				var netStandardpdb = new BuildItem.NoActionResource ("NetStandard16.pdb") {
					WebContent = "https://dl.dropboxusercontent.com/u/18881050/Xamarin/NetStandard16.pdb"
				};
				proj.References.Add (netStandardpdb);
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				var pdbToMdbPath = Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.dll.mdb");
				Assert.IsTrue (
					File.Exists (pdbToMdbPath),
					"PdbTestLibrary.dll.mdb must be generated next to the .pdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "UnnamedProject.pdb")),
					"UnnamedProject.pdb must be copied to the Intermediate directory");
				Assert.IsFalse (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.pdb")),
					"PdbTestLibrary.pdb must not be copied to Intermediate directory");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "PdbTestLibrary.dll.mdb")),
					"PdbTestLibrary.dll.mdb must be copied to Intermediate directory");
				FileAssert.AreNotEqual (pdbToMdbPath,
					Path.Combine (Root, b.ProjectDirectory, "PdbTestLibrary.pdb"),
					"The .pdb should NOT match the .mdb");
				Assert.IsTrue (
					File.Exists (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "NetStandard16.pdb")),
					"NetStandard16.pdb must be copied to Intermediate directory");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				using (var zipFile = ZipHelper.OpenZip (apk)) {
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/NetStandard16.pdb"),
							"assemblies/NetStandard16.pdb should exist in the apk.");
					Assert.IsNotNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.dll.mdb"),
							"assemblies/PdbTestLibrary.dll.mdb should exist in the apk.");
					Assert.IsNull (ZipHelper.ReadFileFromZip (zipFile,
							"assemblies/PdbTestLibrary.pdb"),
							"assemblies/PdbTestLibrary.pdb should not exist in the apk.");
				}
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build failed");
				Assert.IsTrue (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should be skipped");
				var lastTime = File.GetLastAccessTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.IsFalse (
					b.LastBuildOutput.Contains ("Skipping target \"_CopyMdbFiles\" because"),
					"the _CopyMdbFiles target should not be skipped");
				Assert.Less (lastTime,
					File.GetLastAccessTimeUtc (pdbToMdbPath),
					"{0} should have been updated", pdbToMdbPath);
			}
		}

		[Test]
		public void BuildInDesignTimeMode ([Values(false, true)] bool useManagedParser)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name), false ,false)) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				builder.Target = "UpdateAndroidResources";
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been run.");
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been skipped.");
			}
		}

		[Test]
		public void CheckLibraryImportsUpgrade ([Values(false, true)] bool useShortFileNames)
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var libproj = new XamarinAndroidLibraryProject () {
				IsRelease = true,
				ProjectName = "Library1"
			};
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				ProjectName = "App1",
			};
			proj.References.Add (new BuildItem ("ProjectReference", $"..\\Library1\\Library1.csproj"));
			proj.SetProperty ("_AndroidLibrayProjectIntermediatePath", Path.Combine (proj.IntermediateOutputPath, "__library_projects__"));
			proj.SetProperty (proj.ActiveConfigurationProperties, "UseShortFileNames", useShortFileNames);
			using (var libb = CreateDllBuilder (Path.Combine (path, libproj.ProjectName), false, false)) {
				Assert.IsTrue (libb.Build (libproj), "Build should have succeeded.");
				using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
					builder.Verbosity = LoggerVerbosity.Diagnostic;
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
						"The __library_projects__ directory should exist.");
					proj.RemoveProperty ("_AndroidLibrayProjectIntermediatePath");
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
						"The __library_projects__ directory should exist.");
					Assert.IsTrue (libb.Clean (libproj), "Clean should have succeeded.");
					Assert.IsTrue (libb.Build (libproj), "Build should have succeeded.");
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
					var zipFile = libb.Output.GetIntermediaryPath ("__AndroidLibraryProjects__.zip");
					Assert.IsTrue (File.Exists (zipFile));
					using (var zip = ZipHelper.OpenZip (zipFile)) {
						Assert.IsTrue (zip.ContainsEntry ("library_project_imports/__res_name_case_map.txt"), $"{zipFile} should contain a library_project_imports/__res_name_case_map.txt entry");
					}
					if (!useShortFileNames) {
						Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
							"The __library_projects__ directory should exist.");
					} else {
						Assert.IsFalse (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
							"The __library_projects__ directory should not exist.");
						Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "lp")),
							"The lp directory should exist.");
					}

				}
			}
		}

#pragma warning disable 414
		static object [] validateJavaVersionTestCases = new object [] {
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.7.0_101",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.x_101",
				/*expectedResult*/ true,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("validateJavaVersionTestCases")]
		public void ValidateJavaVersion (string targetFrameworkVersion, string buildToolsVersion, string javaVersion, bool expectedResult) 
		{
			var path = Path.Combine ("temp", $"ValidateJavaVersion_{targetFrameworkVersion}_{buildToolsVersion}_{javaVersion}");
			string javaExe = "java";
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "JavaSDK"), javaVersion, out javaExe);
			var AndroidSdkDirectory = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), buildToolsVersion);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = targetFrameworkVersion,
				UseLatestPlatformSdk = false,
			};
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				if (!Directory.Exists (Path.Combine (builder.FrameworkLibDirectory, "xbuild-frameworks", "MonoAndroid", targetFrameworkVersion)))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				builder.ThrowOnBuildFailure = false;
				builder.Target = "_SetLatestTargetFrameworkVersion";
				Assert.AreEqual (expectedResult, builder.Build (proj, parameters: new string[] {
					$"JavaSdkDirectory={javaPath}",
					$"JavaToolExe={javaExe}",
					$"AndroidSdkBuildToolsVersion={buildToolsVersion}",
					$"AndroidSdkDirectory={AndroidSdkDirectory}",
				}), string.Format ("Build should have {0}", expectedResult ? "succeeded" : "failed"));
			}
			Directory.Delete (javaPath, recursive: true);
			Directory.Delete (AndroidSdkDirectory, recursive: true);
		}

		[Test]
		public void BuildAMassiveApp()
		{
			var testPath = Path.Combine("temp", "BuildAMassiveApp");
			TestContext.CurrentContext.Test.Properties ["Output"] = new string [] { Path.Combine (Root, testPath) };
			var sb = new SolutionBuilder("BuildAMassiveApp.sln") {
				SolutionPath = Path.Combine(Root, testPath),
				Verbosity = LoggerVerbosity.Diagnostic,
			};
			var app1 = new XamarinAndroidApplicationProject() {
				ProjectName = "App1",
				AotAssemblies = true,
				IsRelease = true,
				Packages = {
					KnownPackages.AndroidSupportV4_21_0_3_0,
					KnownPackages.GooglePlayServices_22_0_0_2,
				},
			};
			app1.Imports.Add (new Import ("foo.targets") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_CheckAbis"" BeforeTargets=""_DefineBuildTargetAbis"">
	<PropertyGroup>
		<AndroidSupportedAbis>armeabi-v7a;x86</AndroidSupportedAbis>
		<AndroidSupportedAbis Condition=""Exists('$(MSBuildThisFileDirectory)..\..\..\..\Debug\lib\xamarin.android\xbuild\Xamarin\Android\lib\armeabi\libmono-android.release.so')"">$(AndroidSupportedAbis);armeabi</AndroidSupportedAbis>
		<AndroidSupportedAbis Condition=""Exists('$(MSBuildThisFileDirectory)..\..\..\..\Debug\lib\xamarin.android\xbuild\Xamarin\Android\lib\arm64-v8a\libmono-android.release.so')"">$(AndroidSupportedAbis);arm64-v8a</AndroidSupportedAbis>
		<AndroidSupportedAbis Condition=""Exists('$(MSBuildThisFileDirectory)..\..\..\..\Debug\lib\xamarin.android\xbuild\Xamarin\Android\lib\x86_64\libmono-android.release.so')"">$(AndroidSupportedAbis);x86_64</AndroidSupportedAbis>
	</PropertyGroup>
	<Message Text=""$(AndroidSupportedAbis)"" />
</Target>
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)"+ Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
			});
			app1.SetProperty(KnownProperties.AndroidUseSharedRuntime, "False");
			sb.Projects.Add(app1);
			var code = new StringBuilder();
			code.AppendLine("using System;");
			code.AppendLine("namespace App1 {");
			code.AppendLine("\tpublic class AppCode {");
			code.AppendLine("\t\tpublic void Foo () {");
			for (int i = 0; i < 128; i++) {
				var libName = $"Lib{i}";
				var lib = new XamarinAndroidLibraryProject() {
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
				var strings = lib.AndroidResources.First(x => x.Include() == "Resources\\values\\Strings.xml");
				strings.TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""" + libName + @"_name"">" + libName + @"</string>
</resources>";
				sb.Projects.Add(lib);
				app1.References.Add(new BuildItem.ProjectReference($"..\\{libName}\\{libName}.csproj", libName, lib.ProjectGuid));
				code.AppendLine($"\t\t\t{libName}.{libName}.Foo ();");
			}
			code.AppendLine("\t\t}");
			code.AppendLine("\t}");
			code.AppendLine("}");
			app1.Sources.Add(new BuildItem.Source("Code.cs") {
				TextContent = ()=> code.ToString (),
			});
			Assert.IsTrue(sb.Build(new string[] { "Configuration=Release" }), "Solution should have built.");
			Assert.IsTrue(sb.BuildProject(app1, "SignAndroidPackage"), "Build of project should have succeeded");
			sb.Dispose();
		}

		[Test]
		public void XA4212 ()
		{
			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.Sources.Add (new BuildItem ("Compile", "MyBadJavaObject.cs") { TextContent = () => @"
using System;
using Android.Runtime;
namespace UnnamedProject {
    public class MyBadJavaObject : IJavaObject
    {
        public IntPtr Handle {
			get {return IntPtr.Zero;}
        }

        public void Dispose ()
        {
        }
    }
}" });
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed with XA4212.");
				StringAssert.Contains ($"error XA4", builder.LastBuildOutput, "Error should be XA4212");
				StringAssert.Contains ($"Type `UnnamedProject.MyBadJavaObject` implements `Android.Runtime.IJavaObject`", builder.LastBuildOutput, "Error should mention MyBadJavaObject");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "AndroidErrorOnCustomJavaObject=False" }), "Build should have succeeded.");
				StringAssert.Contains ($"warning XA4", builder.LastBuildOutput, "warning XA4212");
			}
		}
	}
}

