﻿﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
		public void BuildInParallel ()
		{
			if (!IsWindows) {
				//TODO: one day we should fix the problems here, various MSBuild tasks step on each other when built in parallel
				Assert.Ignore ("Currently ignoring this test on non-Windows platforms.");
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			var packages = proj.Packages;
			packages.Add (KnownPackages.XamarinForms_3_0_0_561731);
			packages.Add (KnownPackages.Android_Arch_Core_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0);
			packages.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			packages.Add (KnownPackages.SupportCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			packages.Add (KnownPackages.SupportDesign_27_0_2_1);
			packages.Add (KnownPackages.SupportFragment_27_0_2_1);
			packages.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			packages.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);
			packages.Add (KnownPackages.SupportV7RecyclerView_27_0_2_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				//We don't want these things stepping on each other
				b.BuildLogFile = null;
				b.Save (proj, saveProject: true);
				proj.NuGetRestore (Path.Combine (Root, b.ProjectDirectory), b.PackagesDirectory);

				Parallel.For (0, 5, i => {
					try {
						//NOTE: things are going to break here
						b.Build (proj);
					} catch (Exception exc) {
						TestContext.WriteLine ("Expected error in {0}: {1}", nameof (BuildInParallel), exc);
					}
				});

				//The key here, is a build afterward should work
				b.BuildLogFile = "after.log";
				Assert.IsTrue (b.Build (proj), "The build after a parallel failed build should succeed!");
			}
		}

		[Test]
		public void CheckKeystoreIsCreated ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			using (var b = CreateApkBuilder ("temp/CheckKeystoreIsCreated", false, false)) {
				var file = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "debug.keystore");
				var p = new string [] {
					$"_ApkDebugKeyStore={file}",
				};
				Assert.IsTrue (b.Build (proj, parameters: p), "Build should have succeeded.");
				FileAssert.Exists (file, $"{file} should have been created.");
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
		public void DesignTimeBuildHasAndrodDefine ()
		{
			var proj = new XamarinAndroidApplicationProject () {

			};
			proj.Sources.Add (new BuildItem ("Compile", "IsAndroidDefined.cs") {
				TextContent = () => @"
namespace Xamarin.Android.Tests
{
	public class Foo {
		public void FooMethod () {
#if !__ANDROID__ || !__MOBILE__
			Compile Error please :)
#endif
		}
	}
}
",
			});
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName ))) {
				b.Target = "Compile";
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void SwitchBetweenDesignTimeBuild ()
		{
			var proj = new XamarinAndroidApplicationProject ();

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first *regular* build should have succeeded.");
				var build_props = b.Output.GetIntermediaryPath ("build.props");
				var designtime_build_props = b.Output.GetIntermediaryPath (Path.Combine ("designtime", "build.props"));
				FileAssert.Exists (build_props, "build.props should exist after a first `Build`.");
				FileAssert.DoesNotExist (designtime_build_props, "designtime/build.props should *not* exist after a first `Build`.");

				b.Target = "Compile";
				Assert.IsTrue (b.Build (proj, parameters: new [] { "DesignTimeBuild=True" }), "first design-time build should have succeeded.");
				FileAssert.Exists (build_props, "build.props should exist after a design-time build.");
				FileAssert.Exists (designtime_build_props, "designtime/build.props should exist after a design-time build.");

				b.Target = "Build";
				Assert.IsTrue (b.Build (proj), "second *regular* build should have succeeded.");
				FileAssert.Exists (build_props, "build.props should exist after the second `Build`.");
				FileAssert.Exists (designtime_build_props, "designtime/build.props should exist after the second `Build`.");

				//NOTE: none of these targets should run, since we have not actually changed anything!
				var targetsToBeSkipped = new [] {
					//TODO: We would like for this assertion to work, but the <Compile /> item group changes between DTB and regular builds
					//      $(IntermediateOutputPath)designtime\Resource.designer.cs -> Resources\Resource.designer.cs
					//      And so the built assembly changes between DTB and regular build, triggering `_LinkAssembliesNoShrink`
					//"_LinkAssembliesNoShrink",
					"_UpdateAndroidResgen",
					"_GenerateJavaDesignerForComponent",
					"_BuildLibraryImportsCache",
					"_CompileJava",
				};
				foreach (var targetName in targetsToBeSkipped) {
					Assert.IsTrue (b.Output.IsTargetSkipped (targetName), $"`{targetName}` should be skipped!");
				}

				b.Target = "Clean";
				Assert.IsTrue (b.Build (proj), "clean should have succeeded.");

				FileAssert.DoesNotExist (build_props, "build.props should *not* exist after `Clean`.");
				FileAssert.Exists (designtime_build_props, "designtime/build.props should exist after `Clean`.");
			}
		}

		[Test]
		public void BuildPropsBreaksConvertResourcesCases ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\drawable\\IMALLCAPS.png") {
						BinaryContent = () => XamarinAndroidApplicationProject.icon_binary_mdpi,
					},
					new AndroidItem.AndroidResource ("Resources\\layout\\test.axml") {
						TextContent = () => {
							return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ImageView xmlns:android=\"http://schemas.android.com/apk/res/android\" android:src=\"@drawable/IMALLCAPS\" />";
						}
					}
				}
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				//Invalidate build.props with newer timestamp, you could also modify anything in @(_PropertyCacheItems)
				var props = b.Output.GetIntermediaryPath("build.props");
				File.SetLastWriteTimeUtc(props, DateTime.UtcNow);
				File.SetLastAccessTimeUtc(props, DateTime.UtcNow);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
			}
		}

		[Test]
		public void TargetFrameworkMonikerAssemblyAttributesPath ()
		{
			const string filePattern = "MonoAndroid,Version=v*.AssemblyAttributes.cs";
			var proj = new XamarinAndroidApplicationProject {
				TargetFrameworkVersion = "v6.0",
			};
			proj.SetProperty ("AndroidUseLatestPlatformSdk", "True");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Build (proj), "build should have succeeded.");

				var intermediate = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				var old_assemblyattributespath = Path.Combine (intermediate, $"MonoAndroid,Version={proj.TargetFrameworkVersion}.AssemblyAttributes.cs");
				FileAssert.DoesNotExist (old_assemblyattributespath, "TargetFrameworkMonikerAssemblyAttributesPath should have the newer TargetFrameworkVersion.");

				var new_assemblyattributespath = Directory.EnumerateFiles (intermediate, filePattern).SingleOrDefault ();
				Assert.IsNotNull (new_assemblyattributespath, $"A *single* file of pattern {filePattern} should exist in `$(IntermediateOutputPath)`.");
				StringAssert.DoesNotContain (proj.TargetFrameworkVersion, File.ReadAllText (new_assemblyattributespath), $"`{new_assemblyattributespath}` should not contain `{proj.TargetFrameworkVersion}`!");
			}
		}

		[Test]
		public void CheckTimestamps ([Values (true, false)] bool isRelease)
		{
			var start = DateTime.UtcNow.AddSeconds (-1);
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				AndroidResources = {
					new AndroidItem.AndroidResource ("Resources\\layout\\Tabbar.axml") {
						TextContent = () => {
							return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<android.support.design.widget.TabLayout xmlns:android=\"http://schemas.android.com/apk/res/android\" xmlns:app=\"http://schemas.android.com/apk/res-auto\" android:id=\"@+id/sliding_tabs\" android:background=\"?attr/colorPrimary\" android:theme=\"@style/ThemeOverlay.AppCompat.Dark.ActionBar\" app:tabIndicatorColor=\"@android:color/white\" app:tabGravity=\"fill\" app:tabMode=\"fixed\" />";
						}
					}
				}
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			var packages = proj.Packages;
			packages.Add (KnownPackages.XamarinForms_3_0_0_561731);
			packages.Add (KnownPackages.Android_Arch_Core_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0);
			packages.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			packages.Add (KnownPackages.SupportCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			packages.Add (KnownPackages.SupportDesign_27_0_2_1);
			packages.Add (KnownPackages.SupportFragment_27_0_2_1);
			packages.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			packages.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);
			packages.Add (KnownPackages.SupportV7RecyclerView_27_0_2_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				//To be sure we are at a clean state
				var projectDir = Path.Combine (Root, b.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, true);

				var intermediate = Path.Combine (projectDir, proj.IntermediateOutputPath);
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");

				//Absolutely non of these files should be *older* than the starting time of this test!
				var files = Directory.EnumerateFiles (intermediate, "*", SearchOption.AllDirectories).ToList ();
				foreach (var file in files) {
					var info = new FileInfo (file);
					Assert.IsTrue (info.LastWriteTimeUtc > start, $"`{file}` is older than `{start}`, with a timestamp of `{info.LastWriteTimeUtc}`!");
				}

				//Build again after a code change (renamed Java.Lang.Object subclass), checking a few files
				proj.MainActivity = proj.DefaultMainActivity.Replace ("MainActivity", "MainActivity2");
				proj.Touch ("MainActivity.cs");
				start = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");

				foreach (var file in new [] { "typemap.mj", "typemap.jm" }) {
					var info = new FileInfo (Path.Combine (intermediate, "android", file));
					Assert.IsTrue (info.LastWriteTimeUtc > start, $"`{file}` is older than `{start}`, with a timestamp of `{info.LastWriteTimeUtc}`!");
				}

				//One last build with no changes
				Assert.IsTrue (b.Build (proj), "third build should have succeeded.");
				var targetsToBeSkipped = new [] {
					isRelease ? "_LinkAssembliesShrink" : "_LinkAssembliesNoShrink",
					"_UpdateAndroidResgen",
					"_GenerateJavaDesignerForComponent",
					"_BuildLibraryImportsCache",
					"_CompileJava",
				};
				foreach (var targetName in targetsToBeSkipped) {
					Assert.IsTrue (b.Output.IsTargetSkipped (targetName), $"`{targetName}` should be skipped!");
				}
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
					var ignoreFiles = new string [] {
						"TemporaryGeneratedFile",
						"CopyComplete"
					};
					Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
					var fileCount = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath), "*", SearchOption.AllDirectories)
						.Where (x => !ignoreFiles.Any (i => !Path.GetFileName (x).Contains (i))).Count ();
					Assert.AreEqual (0, fileCount, "{0} should be Empty", proj.IntermediateOutputPath);
					fileCount = Directory.GetFiles (Path.Combine (Root, b.ProjectDirectory, proj.OutputPath), "*", SearchOption.AllDirectories)
						.Where (x => !ignoreFiles.Any (i => !Path.GetFileName (x).Contains (i))).Count ();
					Assert.AreEqual (0, fileCount, "{0} should be Empty", proj.OutputPath);
				}
			}
		}

		[Test]
		public void BuildIncrementingAssemblyVersion ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.Sources.Add (new BuildItem ("Compile", "AssemblyInfo.cs") { 
				TextContent = () => "[assembly: System.Reflection.AssemblyVersion (\"1.0.0.*\")]" 
			});

			using (var b = CreateApkBuilder ("temp/BuildIncrementingAssemblyVersion")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var acwmapPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "acw-map.txt");
				var assemblyPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath, "UnnamedProject.dll");
				var firstAssemblyVersion = AssemblyName.GetAssemblyName (assemblyPath).Version;
				var expectedAcwMap = File.ReadAllText (acwmapPath);

				b.Target = "Rebuild";
				b.BuildLogFile = "rebuild.log";
				Assert.IsTrue (b.Build (proj), "Rebuild should have succeeded.");

				var secondAssemblyVersion = AssemblyName.GetAssemblyName (assemblyPath).Version;
				Assert.AreNotEqual (firstAssemblyVersion, secondAssemblyVersion);
				var actualAcwMap = File.ReadAllText (acwmapPath);
				Assert.AreEqual (expectedAcwMap, actualAcwMap);
			}
		}

		[Test]
		public void BuildIncrementingClassName ()
		{
			int count = 0;
			var source = new BuildItem ("Compile", "World.cs") {
				TextContent = () => {
					int current = ++count;
					return $"namespace Hello{current} {{ public class World{current} : Java.Lang.Object {{ }} }}";
				}
			};
			var proj = new XamarinAndroidApplicationProject ();
			proj.Sources.Add (source);

			using (var b = CreateApkBuilder ("temp/BuildIncrementingClassName")) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var classesZipPath = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "bin", "classes.zip");
				FileAssert.Exists (classesZipPath);
				var expectedBuilder = new StringBuilder ();
				using (var zip = ZipHelper.OpenZip (classesZipPath)) {
					foreach (var file in zip) {
						expectedBuilder.AppendLine (file.FullName);
					}
				}
				var expectedZip = expectedBuilder.ToString ();

				source.Timestamp = null; //Force the file to re-save w/ new Timestamp
				Assert.IsTrue (b.Build (proj), "Second build should have succeeded.");

				var actualBuilder = new StringBuilder ();
				using (var zip = ZipHelper.OpenZip (classesZipPath)) {
					foreach (var file in zip) {
						actualBuilder.AppendLine (file.FullName);
					}
				}
				var actualZip = actualBuilder.ToString ();
				Assert.AreNotEqual (expectedZip, actualZip);

				//Build with no changes
				Assert.IsTrue (b.Build (proj), "Third build should have succeeded.");
				FileAssert.Exists (classesZipPath);

				//Clean
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
				FileAssert.DoesNotExist (classesZipPath);
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
				//NOTE: Windows has shortened paths such as: C:\Users\myuser\ANDROI~3\ndk\PLATFO~1\AN3971~1\arch-x86\usr\lib\libc.so
				if (checkMinLlvmPath && !IsWindows) {
					// LLVM passes a direct path to libc.so, and we need to use the libc.so
					// which corresponds to the *minimum* SDK version specified in AndroidManifest.xml
					// Since we overrode minSdkVersion=10, that means we should use libc.so from android-9.
					var rightLibc   = new Regex (@"^\s*\[AOT\].*cross-.*--llvm.*,ld-flags=.*android-9.arch-.*.usr.lib.libc\.so", RegexOptions.Multiline);
					var m           = rightLibc.Match (string.Join ("\n",b.LastBuildOutput));
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
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
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
					b.Output.IsTargetSkipped ("_CompileJava"),
					"the _CompileJava target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_BuildApkEmbed"),
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

				if (isRelease && enableProguard) {
					var proguardProjectPrimary = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "proguard", "proguard_project_primary.cfg");
					FileAssert.Exists (proguardProjectPrimary);
					StringAssertEx.ContainsText (File.ReadAllLines (proguardProjectPrimary), "-keep class md52d9cf6333b8e95e8683a477bc589eda5.MainActivity");
				}
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
			string intermediateDir = proj.IntermediateOutputPath;
			if (IsWindows) {
				proj.SetProperty ("AppendTargetFrameworkToIntermediateOutputPath", "True");
			}

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				proj.TargetFrameworkVersion = b.LatestTargetFrameworkVersion ();
				if (IsWindows) {
					intermediateDir = Path.Combine (intermediateDir, proj.TargetFrameworkAbbreviated);
				}
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (File.Exists (Path.Combine (Root, b.ProjectDirectory, intermediateDir,  "android/bin/classes.dex")),
					"multidex-ed classes.zip exists");
				var multidexKeepPath  = Path.Combine (Root, b.ProjectDirectory, intermediateDir, "multidex.keep");
				Assert.IsTrue (File.Exists (multidexKeepPath), "multidex.keep exists");
				Assert.IsTrue (File.ReadAllLines (multidexKeepPath).Length > 1, "multidex.keep must contain more than one line.");
				Assert.IsTrue (b.LastBuildOutput.ContainsText (Path.Combine (proj.TargetFrameworkVersion, "mono.android.jar")), proj.TargetFrameworkVersion + "/mono.android.jar should be used.");
				Assert.IsFalse (b.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");
			}
		}

		[Test]
		public void BuildAfterMultiDexIsNotRequired ()
		{
			var proj = CreateMultiDexRequiredApplication ();
			proj.SetProperty ("AndroidEnableMultiDex", "True");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				string intermediateDir = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath);
				string androidBinDir = Path.Combine (intermediateDir, "android", "bin");
				string apkPath = Path.Combine (androidBinDir, "UnnamedProject.UnnamedProject.apk");

				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				FileAssert.Exists (Path.Combine (androidBinDir, "classes.dex"));
				FileAssert.Exists (Path.Combine (androidBinDir, "classes2.dex"));
				FileAssert.Exists (Path.Combine (androidBinDir, "classes3.dex"));

				using (var zip = ZipHelper.OpenZip (apkPath)) {
					var entries = zip.Select (e => e.FullName).ToList ();
					Assert.IsTrue (entries.Contains ("classes.dex"), "APK must contain `classes.dex`.");
					Assert.IsTrue (entries.Contains ("classes2.dex"), "APK must contain `classes2.dex`.");
					Assert.IsTrue (entries.Contains ("classes3.dex"), "APK must contain `classes3.dex`.");
				}

				//Now build project again after it no longer requires multidex, remove the *HUGE* AndroidJavaSource build items
				while (proj.OtherBuildItems.Count > 1)
					proj.OtherBuildItems.RemoveAt (proj.OtherBuildItems.Count - 1);

				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				FileAssert.Exists (Path.Combine (androidBinDir, "classes.dex"));
				FileAssert.DoesNotExist (Path.Combine (androidBinDir, "classes2.dex"));
				FileAssert.DoesNotExist (Path.Combine (androidBinDir, "classes3.dex"));

				using (var zip = ZipHelper.OpenZip (apkPath)) {
					var entries = zip.Select (e => e.FullName).ToList ();
					Assert.IsTrue (entries.Contains ("classes.dex"), "APK must contain `classes.dex`.");
					Assert.IsFalse (entries.Contains ("classes2.dex"), "APK must *not* contain `classes2.dex`.");
					Assert.IsFalse (entries.Contains ("classes3.dex"), "APK must *not* contain `classes3.dex`.");
				}
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
				Assert.IsFalse (b.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");
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
					b.Output.IsTargetSkipped ("_Sign"),
					"the _Sign target should not run");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_StripEmbeddedLibraries"),
					"the _StripEmbeddedLibraries target should not run");
				proj.AndroidResources.Last ().Timestamp = null;
				Assert.IsTrue (b.Build (proj), "third build failed");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_Sign"),
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
					b.Output.IsTargetSkipped ("_Sign"),
					"the _Sign target should not run");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_StripEmbeddedLibraries"),
					"the _StripEmbeddedLibraries target should not run");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_LinkAssembliesShrink"),
					"the _LinkAssembliesShrink target should not run");
				foo.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj), "third build failed");
				Assert.IsFalse (b.Output.IsTargetSkipped ("CoreCompile"),
					"the Core Compile target should run");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_Sign"),
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
					b.Output.IsTargetSkipped ("_CopyMdbFiles"),
					"the _CopyMdbFiles target should be skipped");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_CopyPdbFiles"),
					"the _CopyPdbFiles target should be skipped");
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
			//DebugType=Full produces mdbs on Windows
			if (IsWindows) {
				lib.DebugProperties.Add (new Property ("", "DebugType", "portable"));
				proj.DebugProperties.Add (new Property ("", "DebugType", "portable"));
			}
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
					Assert.IsFalse (
						File.Exists (linkDst),
						"Library1.pdb should not be copied to linkdst directory because it has no Abstrsact methods to fix up.");
					Assert.IsTrue (
						File.Exists (linkSrc),
						"Library1.pdb must be copied to linksrc directory");
					var outputPath = Path.Combine (Root, b.ProjectDirectory, proj.OutputPath);
					using (var apk = ZipHelper.OpenZip (Path.Combine (outputPath, proj.PackageName + "-Signed.apk"))) {
						var data = ZipHelper.ReadFileFromZip (apk, "assemblies/Library1.pdb");
						if (data == null)
							data = File.ReadAllBytes (assetsPdb);
						var filedata = File.ReadAllBytes (linkSrc);
						Assert.AreEqual (filedata.Length, data.Length, "Library1.pdb in the apk should match {0}", linkSrc);
					}
					linkDst = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linkdst", "App1.pdb");
					linkSrc = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linksrc", "App1.pdb");
					Assert.IsTrue (
						File.Exists (linkDst),
						"App1.pdb should be copied to linkdst directory because it has Abstrsact methods to fix up.");
					Assert.IsTrue (
						File.Exists (linkSrc),
						"App1.pdb must be copied to linksrc directory");
					FileAssert.AreEqual (linkSrc, linkDst, "{0} and {1} should not differ.", linkSrc, linkDst);
					linkDst = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linkdst", "App1.dll");
					linkSrc = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "linksrc", "App1.dll");
					FileAssert.AreEqual (linkSrc, linkDst, "{0} and {1} should match.", linkSrc, linkDst);

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
					b.Output.IsTargetSkipped ("_CopyMdbFiles"),
					"the _CopyMdbFiles target should be skipped");
				var lastTime = File.GetLastWriteTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_CopyMdbFiles"),
					"the _CopyMdbFiles target should not be skipped");
				Assert.Less (lastTime,
					File.GetLastWriteTimeUtc (pdbToMdbPath),
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
					b.Output.IsTargetSkipped ("_CopyConfigFiles"),
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
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_AddStaticResources"),
					"The _AddStaticResources should have been run");
				Assert.IsTrue (b.Build (proj), "Build should not have failed");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_AddStaticResources"),
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
				StringAssertEx.Contains ("Resource entry Common.None is already defined", b.LastBuildOutput);
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
					StringAssertEx.Contains (text, b.LastBuildOutput);
				} else
					StringAssertEx.Contains ("TestMe.java(1,8): javac.exe error :  error: class, interface, or enum expected", b.LastBuildOutput);
				StringAssertEx.Contains ("TestMe2.java(1,41): error :  error: ';' expected", b.LastBuildOutput);
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
				StringAssertEx.Contains ("XA5213", b.LastBuildOutput);
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

		/// <summary>
		/// Works around a bug in lint.bat on Windows: https://issuetracker.google.com/issues/68753324
		/// - We may want to remove this if a future Android SDK tools, no longer has this issue
		/// </summary>
		void FixLintOnWindows ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				var userProfile = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				var androidSdkTools = Path.Combine (userProfile, "android-toolchain", "sdk", "tools");
				if (Directory.Exists (androidSdkTools)) {
					Environment.SetEnvironmentVariable ("JAVA_OPTS", $"\"-Dcom.android.tools.lint.bindir={androidSdkTools}\"", EnvironmentVariableTarget.Process);
				}
			}
		}

		[Test]
		public void CheckLintErrorsAndWarnings ()
		{
			FixLintOnWindows ();

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
				StringAssertEx.DoesNotContain ("XA0102", b.LastBuildOutput);
				Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			}
		}

		[Test]
		public void CheckLintConfigMerging ()
		{
			FixLintOnWindows ();

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
				StringAssertEx.Contains (expected, b.LastBuildOutput,
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
						StringAssertEx.DoesNotContain ("XA9002", b.LastBuildOutput, "XA9002 should not have been raised");
					else
						StringAssertEx.Contains ("XA9002", b.LastBuildOutput, "XA9002 should have been raised");
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
						Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "warning XA4301: Apk already contains the item lib/armeabi-v7a/libRSSupport.so; ignoring."),
							"warning about skipping libRSSupport.so should have been raised");
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
		public void BuildWithNativeLibraryUnknownAbi ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary ("not-a-real-abi\\libtest.so") {
						BinaryContent = () => new byte[10],
					},
				}
			};
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, "armeabi-v7a;x86");

			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (proj), "Build should have failed.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"error XA4301: Cannot determine abi of native library not-a-real-abi{Path.DirectorySeparatorChar}libtest.so."),
					"error about libtest.so should have been raised");
			}
		}

		[Test]
		public void BuildWithExternalJavaLibrary ()
		{
			var path = Path.Combine ("temp", "BuildWithExternalJavaLibrary");
			var binding = new XamarinAndroidBindingProject () {
				ProjectName = "BuildWithExternalJavaLibraryBinding",
				AndroidClassParser = "class-parse",
			};
			using (var bbuilder = CreateDllBuilder (Path.Combine (path, "BuildWithExternalJavaLibraryBinding"))) {
				string multidex_path = bbuilder.RunningMSBuild ? @"$(MSBuildExtensionsPath)" : @"$(MonoDroidInstallDirectory)\lib\xamarin.android\xbuild";
				string multidex_jar = $@"{multidex_path}\Xamarin\Android\android-support-multidex.jar";
				binding.Jars.Add (new AndroidItem.InputJar (() => multidex_jar));

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
				StringAssertEx.Contains ("AssetMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidAsset");
				StringAssertEx.Contains ("ResourceMetaDataOK", builder.LastBuildOutput, "Metadata was not copied for AndroidResource");
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
		public void AarContentExtraction ([Values (false, true)] bool useAapt2)
		{
			var aar = new AndroidItem.AndroidAarLibrary ("Jars\\android-crop-1.0.1.aar") {
				WebContent = "https://jcenter.bintray.com/com/soundcloud/android/android-crop/1.0.1/android-crop-1.0.1.aar"
			};
			var proj = new XamarinAndroidApplicationProject () {
				OtherBuildItems = {
					aar,
				},
			};
			proj.SetProperty ("AndroidUseAapt2", useAapt2.ToString ());
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName), false, false)) {
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				var assemblyMap = builder.Output.GetIntermediaryPath (Path.Combine ("lp", "map.cache"));
				var cache = builder.Output.GetIntermediaryPath ("libraryprojectimports.cache");
				Assert.IsTrue (File.Exists (assemblyMap), $"{assemblyMap} should exist.");
				Assert.IsTrue (File.Exists (cache), $"{cache} should exist.");
				var assemblyIdentityMap = new List<string> ();
				foreach (var s in File.ReadLines (assemblyMap)) {
					assemblyIdentityMap.Add (s);
				}
				FileAssert.Exists (Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "lp",
					assemblyIdentityMap.IndexOf ("android-crop-1.0.1").ToString (), "jl", "classes.jar"),
					"classes.jar was not extracted from the aar.");
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"),
					"_ResolveLibraryProjectImports should not have run.");

				var doc = XDocument.Load (cache);
				var expectedCount = doc.Elements ("Paths").Elements ("ResolvedResourceDirectories").Count ();

				aar.Timestamp = DateTime.UtcNow.Add (TimeSpan.FromMinutes (2));
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"),
					"_ResolveLibraryProjectImports should have run.");

				doc = XDocument.Load (cache);
				var count = doc.Elements ("Paths").Elements ("ResolvedResourceDirectories").Count ();
				Assert.AreEqual (expectedCount, count, "The same number of resource directories should have been resolved.");

			}
		}

		[Test]
		public void CheckTargetFrameworkVersion ([Values (true, false)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetProperty ("AndroidUseLatestPlatformSdk", "False");
			proj.SetProperty ("TargetFrameworkVersion", "v2.3");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				if (!Directory.Exists (Path.Combine (builder.FrameworkLibDirectory, "xbuild-frameworks", "MonoAndroid", "v2.3")))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion=v2.3"), "TargetFrameworkVerson should be v2.3");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "TargetFrameworkVersion=v4.4" }), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion=v4.4"), "TargetFrameworkVerson should be v4.4");

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
					Assert.AreEqual (warningExpected, builder.LastBuildOutput.ContainsText ("warning BG8504"), "warning BG8504 is expected: " + warningExpected);
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
						Assert.IsFalse (builder.LastBuildOutput.ContainsText ("warning BG850"), "warning BG850* is NOT expected");
					else
						Assert.IsTrue (builder.LastBuildOutput.ContainsText ("warning " + expectedWarning), "warning " + expectedWarning + " is expected.");
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
		public void BuildApplicationWithSpacesInPath ([Values (true, false)] bool isRelease, [Values (true, false)] bool enableProguard, [Values (true, false)] bool enableMultiDex)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				AotAssemblies = isRelease,
				EnableProguard = enableProguard,
			};
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "Hello (World).jar") { BinaryContent = () => Convert.FromBase64String (@"
UEsDBBQACAgIAMl8lUsAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAAA
AAAAFBLAwQUAAgICADJfJVLAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803My0
xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEG5oaKWj4FyUm56QqOOcXFeQXJZYA1Wv
ycvFyAQBQSwcIbrokAkQAAABFAAAAUEsDBBQACAgIAIJ8lUsAAAAAAAAAAAAAAAASAAAAc2FtcGxl
L0hlbGxvLmNsYXNzO/Vv1z4GBgYTBkEuBhYGXg4GPnYGfnYGAUYGNpvMvMwSO0YGZg3NMEYGFuf8l
FRGBn6fzLxUv9LcpNSikMSkHKAIa3l+UU4KI4OIhqZPVmJZon5OYl66fnBJUWZeujUjA1dwfmlRcq
pbJkgtl0dqTk6+HkgZDwMrAxvQFrCIIiMDT3FibkFOqj6Yz8gggDDKPykrNbmEQZGBGehCEGBiYAR
pBpLsQJ4skGYE0qxa2xkYNwIZjAwcQJINIggkORm4oEqloUqZhZg2oClkB5LcYLN5AFBLBwjQMrpO
0wAAABMBAABQSwECFAAUAAgICADJfJVLAAAAAAIAAAAAAAAACQAEAAAAAAAAAAAAAAAAAAAATUVUQ
S1JTkYv/soAAFBLAQIUABQACAgIAMl8lUtuuiQCRAAAAEUAAAAUAAAAAAAAAAAAAAAAAD0AAABNRV
RBLUlORi9NQU5JRkVTVC5NRlBLAQIUABQACAgIAIJ8lUvQMrpO0wAAABMBAAASAAAAAAAAAAAAAAA
AAMMAAABzYW1wbGUvSGVsbG8uY2xhc3NQSwUGAAAAAAMAAwC9AAAA1gEAAAAA") });
			if (enableMultiDex)
				proj.SetProperty ("AndroidEnableMultiDex", "True");

			if (isRelease) {
				proj.Imports.Add (new Import ("foo.targets") {
					TextContent = () => @"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
<Target Name=""_Foo"" AfterTargets=""_SetLatestTargetFrameworkVersion"">
	<PropertyGroup>
		<AotAssemblies Condition=""!Exists('$(MonoAndroidBinDirectory)" + Path.DirectorySeparatorChar + @"cross-arm')"">False</AotAssemblies>
	</PropertyGroup>
	<Message Text=""$(AotAssemblies)"" />
</Target>
</Project>
",
				});
			}
			using (var b = CreateApkBuilder (Path.Combine ("temp", $"BuildReleaseAppWithA InIt({isRelease}{enableProguard}{enableMultiDex})"))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsFalse (b.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");
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

		[Test]
		public void BuildWithResolveAssembliesFailure ([Values (true, false)] bool usePackageReference)
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				}
			};
			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { void EventHubs () { Microsoft.Azure.EventHubs.EventHubClient c; } }"
					},
				}
			};
			if (usePackageReference)
				lib.PackageReferences.Add (KnownPackages.Microsoft_Azure_EventHubs);
			else
				lib.Packages.Add (KnownPackages.Microsoft_Azure_EventHubs);
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName), false))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				if (usePackageReference) {
					//NOTE: <PackageReference /> not working under xbuild
					if (!libBuilder.RunningMSBuild)
						Assert.Ignore ("This test requires MSBuild.");

					libBuilder.Target = "Restore";
					Assert.IsTrue (libBuilder.Build (lib), "Restore should have succeeded.");
					libBuilder.Target = "Build";
				}
				Assert.IsTrue (libBuilder.Build (lib), "Build should have succeeded.");

				appBuilder.ThrowOnBuildFailure = false;
				Assert.IsFalse (appBuilder.Build (app), "Build should have failed.");

				const string error = "error XA2002: Can not resolve reference:";

				//NOTE: we get a different message when using <PackageReference /> due to automatically getting the Microsoft.Azure.Amqp (and many other) transient dependencies
				if (usePackageReference) {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.EventHubs`, referenced by `MyLibrary`. Please add a NuGet package or assembly reference for `Microsoft.Azure.EventHubs`, or remove the reference to `MyLibrary`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.EventHubs`!");
				} else {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Amqp`, referenced by `MyLibrary` > `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Amqp`, or remove the reference to `MyLibrary`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.Amqp`!");
				}

				//Now add the PackageReference to the app to see a different error message
				if (usePackageReference) {
					app.PackageReferences.Add (KnownPackages.Microsoft_Azure_EventHubs);
					appBuilder.Target = "Restore";
					Assert.IsTrue (appBuilder.Build (app), "Restore should have succeeded.");
					appBuilder.Target = "Build";
				} else {
					app.Packages.Add (KnownPackages.Microsoft_Azure_EventHubs);
				}
				Assert.IsFalse (appBuilder.Build (app), "Build should have failed.");

				//NOTE: we get a different message when using <PackageReference /> due to automatically getting the Microsoft.Azure.Amqp (and many other) transient dependencies
				if (usePackageReference) {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Services.AppAuthentication`, referenced by `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Services.AppAuthentication`, or remove the reference to `Microsoft.Azure.EventHubs`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.Services.AppAuthentication`!");
				} else {
					Assert.IsTrue (appBuilder.LastBuildOutput.ContainsText ($"{error} `Microsoft.Azure.Amqp`, referenced by `Microsoft.Azure.EventHubs`. Please add a NuGet package or assembly reference for `Microsoft.Azure.Amqp`, or remove the reference to `Microsoft.Azure.EventHubs`."),
						$"Should recieve '{error}' regarding `Microsoft.Azure.Services.Amqp`!");
				}
			}
		}

		static object [] TlsProviderTestCases =
		{
			// androidTlsProvider, isRelease, extpected
			new object[] { "", true, true, },
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
				string build_props = b.Output.GetIntermediaryPath ("build.props");
				FileAssert.Exists (build_props, "build.props should exist after first build.");
				proj.Packages.Add (KnownPackages.SupportV7CardView_24_2_1);
				foreach (var reference in KnownPackages.SupportV7CardView_24_2_1.References) {
					reference.Timestamp = DateTimeOffset.Now;
					proj.References.Add (reference);
				}
				b.Save (proj, doNotCleanupOnUpdate: true);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				var doc = File.ReadAllText (Path.Combine (b.Root, b.ProjectDirectory, proj.IntermediateOutputPath, "resourcepaths.cache"));
				Assert.IsTrue (doc.Contains (Path.Combine ("Xamarin.Android.Support.v7.CardView", "24.2.1")), "CardView should be resolved as a reference.");
				FileAssert.Exists (build_props, "build.props should exist after second build.");

				proj.MainActivity = proj.DefaultMainActivity.Replace ("clicks", "CLICKS");
				proj.Touch ("MainActivity.cs");
				Assert.IsTrue (b.Build (proj), "third build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_CleanIntermediateIfNuGetsChange"), "A build with no changes to NuGets should *not* trigger `_CleanIntermediateIfNuGetsChange`!");
				FileAssert.Exists (build_props, "build.props should exist after third build.");
			}
		}

		//This test validates the _CleanIntermediateIfNuGetsChange target
		[Test]
		public void BuildAfterUpgradingNuget ([Values (false, true)] bool usePackageReference)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			var packages = usePackageReference ? proj.PackageReferences : proj.Packages;
			packages.Add (KnownPackages.XamarinForms_2_3_4_231);
			packages.Add (KnownPackages.AndroidSupportV4_25_4_0_1);
			packages.Add (KnownPackages.SupportCompat_25_4_0_1);
			packages.Add (KnownPackages.SupportCoreUI_25_4_0_1);
			packages.Add (KnownPackages.SupportCoreUtils_25_4_0_1);
			packages.Add (KnownPackages.SupportDesign_25_4_0_1);
			packages.Add (KnownPackages.SupportFragment_25_4_0_1);
			packages.Add (KnownPackages.SupportMediaCompat_25_4_0_1);
			packages.Add (KnownPackages.SupportV7AppCompat_25_4_0_1);
			packages.Add (KnownPackages.SupportV7CardView_25_4_0_1);
			packages.Add (KnownPackages.SupportV7MediaRouter_25_4_0_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				if (usePackageReference) {
					b.RequiresMSBuild = true;
					b.Target = "Restore,Build";
				}
				//[TearDown] will still delete if test outcome successful, I need logs if assertions fail but build passes
				b.CleanupAfterSuccessfulBuild =
					b.CleanupOnDispose = false;
				var projectDir = Path.Combine (Root, b.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, true);
				Assert.IsTrue (b.Build (proj), "first build should have succeeded.");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_CleanIntermediateIfNuGetsChange"), "`_CleanIntermediateIfNuGetsChange` should have run!");

				var nugetStamp = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, proj.ProjectName + ".nuget.stamp");
				FileAssert.Exists (nugetStamp, "`_CleanIntermediateIfNuGetsChange` did not create stamp file!");
				string build_props = b.Output.GetIntermediaryPath ("build.props");
				FileAssert.Exists (build_props, "build.props should exist after first build.");

				if (!usePackageReference) {
					foreach (var p in proj.Packages) {
						foreach (var r in p.References) {
							proj.References.Remove (r);
						}
					}
				}
				packages.Clear ();
				packages.Add (KnownPackages.XamarinForms_3_0_0_561731);
				packages.Add (KnownPackages.Android_Arch_Core_Common_26_1_0);
				packages.Add (KnownPackages.Android_Arch_Lifecycle_Common_26_1_0);
				packages.Add (KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0);
				packages.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
				packages.Add (KnownPackages.SupportCompat_27_0_2_1);
				packages.Add (KnownPackages.SupportCoreUI_27_0_2_1);
				packages.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
				packages.Add (KnownPackages.SupportDesign_27_0_2_1);
				packages.Add (KnownPackages.SupportFragment_27_0_2_1);
				packages.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
				packages.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
				packages.Add (KnownPackages.SupportV7CardView_27_0_2_1);
				packages.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);
				packages.Add (KnownPackages.SupportV7RecyclerView_27_0_2_1);
				b.Save (proj, doNotCleanupOnUpdate: true);
				Assert.IsTrue (b.Build (proj), "second build should have succeeded.");
				Assert.IsFalse (b.Output.IsTargetSkipped ("_CleanIntermediateIfNuGetsChange"), "`_CleanIntermediateIfNuGetsChange` should have run!");
				FileAssert.Exists (nugetStamp, "`_CleanIntermediateIfNuGetsChange` did not create stamp file!");
				Assert.IsFalse (StringAssertEx.ContainsText (b.LastBuildOutput, "Xamarin.Android.Support.v4.dll: extracted files are up to date"), "`ResolveLibraryProjectImports` should not skip `Xamarin.Android.Support.v4.dll`!");
				FileAssert.Exists (build_props, "build.props should exist after second build.");

				proj.MainActivity = proj.MainActivity.Replace ("clicks", "CLICKS");
				proj.Touch ("MainActivity.cs");
				Assert.IsTrue (b.Build (proj), "third build should have succeeded.");
				Assert.IsTrue (b.Output.IsTargetSkipped ("_CleanIntermediateIfNuGetsChange"), "A build with no changes to NuGets should *not* trigger `_CleanIntermediateIfNuGetsChange`!");
				FileAssert.Exists (build_props, "build.props should exist after third build.");
			}
		}

		[Test]
		public void CheckTargetFrameworkVersion ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("AndroidUseLatestPlatformSdk", "False");
			proj.SetProperty ("TargetFrameworkVersion", "v2.3");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				if (!Directory.Exists (Path.Combine (builder.FrameworkLibDirectory, "xbuild-frameworks", "MonoAndroid", "v2.3")))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion=v2.3"), "TargetFrameworkVerson should be v2.3");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "TargetFrameworkVersion=v4.4" }), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"Output Property: TargetFrameworkVersion=v4.4"), "TargetFrameworkVerson should be v4.4");
			}
		}

		[Test]
		public void BuildBasicApplicationCheckPdb ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.ActiveConfigurationProperties, "DebugType", "portable");
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
				b.BuildLogFile = "build1.log";
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build failed");
				Assert.IsTrue (
					b.Output.IsTargetSkipped ("_CopyMdbFiles"),
					"the _CopyMdbFiles target should be skipped");
				b.BuildLogFile = "build2.log";
				var lastTime = File.GetLastWriteTimeUtc (pdbToMdbPath);
				pdb.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "third build failed");
				Assert.IsFalse (
					b.Output.IsTargetSkipped ("_CopyMdbFiles"),
					"the _CopyMdbFiles target should not be skipped");
				Assert.Less (lastTime,
					File.GetLastWriteTimeUtc (pdbToMdbPath),
					"{0} should have been updated", pdbToMdbPath);
			}
		}

		[Test]
		public void BuildInDesignTimeMode ([Values(false, true)] bool useManagedParser)
		{
			var path = Path.Combine ("temp", TestContext.CurrentContext.Test.Name);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedParser.ToString ());
			using (var builder = CreateApkBuilder (path, false ,false)) {
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				builder.Target = "UpdateAndroidResources";
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been run.");
				var librarycache = Path.Combine (Root, path, proj.IntermediateOutputPath, "designtime", "libraryprojectimports.cache");
				Assert.IsTrue (File.Exists (librarycache), $"'{librarycache}' should exist.");
				librarycache = Path.Combine (Root, path, proj.IntermediateOutputPath, "libraryprojectimports.cache");
				Assert.IsFalse (File.Exists (librarycache), $"'{librarycache}' should not exist.");
				builder.Build (proj, parameters: new string[] { "DesignTimeBuild=true" });
				Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreatePropertiesCache"), "target \"_CreatePropertiesCache\" should have been run.");
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_ResolveLibraryProjectImports"), "target \"_ResolveLibraryProjectImports\' should have been skipped.");
				Assert.IsTrue (builder.Clean (proj), "Clean Should have succeeded");
				builder.Target = "_CleanDesignTimeIntermediateDir";
				Assert.IsTrue (builder.Build (proj), "_CleanDesignTimeIntermediateDir should have succeeded");
				librarycache = Path.Combine (Root, path, proj.IntermediateOutputPath, "designtime", "libraryprojectimports.cache");
				Assert.IsFalse (File.Exists (librarycache), $"'{librarycache}' should not exist.");
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
					if (useShortFileNames) {
						Assert.IsFalse (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
							"The __library_projects__ directory should not exist, due to IncrementalClean.");
					} else {
						Assert.IsTrue (Directory.Exists (Path.Combine (Root, path, proj.ProjectName, proj.IntermediateOutputPath, "__library_projects__")),
							"The __library_projects__ directory should exist.");
					}
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

		[Test]
		public void ResolveLibraryImportsWithInvalidZip ()
		{
			var proj = new XamarinAndroidApplicationProject {
				PackageReferences = {
					KnownPackages.PCLCrypto_Alpha,
				},
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				b.RequiresMSBuild = true;
				b.Target = "Restore";
				Assert.IsTrue (b.Build (proj), "Restore should have succeeded.");
				b.Target = "Build";
				b.ThrowOnBuildFailure = false;
				if (b.Build (proj)) {
					//NOTE: `:` in a file path should fail on Windows, but passes on macOS
					if (IsWindows)
						Assert.Fail ("Build should have failed.");
				} else {
					Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "error XA4303: Error extracting resources from"), "Should receive XA4303 error.");
				}
			}
		}

		[Test]
		public void ResolveLibraryImportsWithReadonlyFiles ()
		{
			//NOTE: doesn't need to be a full Android Wear app
			var proj = new XamarinAndroidApplicationProject {
				Packages = {
					KnownPackages.AndroidWear_2_2_0,
					KnownPackages.Android_Arch_Core_Common_26_1_0,
					KnownPackages.Android_Arch_Lifecycle_Common_26_1_0,
					KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0,
					KnownPackages.SupportCompat_27_0_2_1,
					KnownPackages.SupportCoreUI_27_0_2_1,
					KnownPackages.SupportPercent_27_0_2_1,
					KnownPackages.SupportV7RecyclerView_27_0_2_1,
				},
			};
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.RequiresMSBuild = true;
				b.Target = "Restore";
				Assert.IsTrue (b.Build (proj), "Restore should have succeeded.");
				b.Target = "Build";
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
			}
		}

		[Test]
		public void AndroidLibraryProjectsZipWithOddPaths ()
		{
			var proj = new XamarinAndroidLibraryProject ();
			proj.Imports.Add (new Import ("foo.props") {
				TextContent = () => $@"
					<Project>
					  <PropertyGroup>
						<IntermediateOutputPath>$(MSBuildThisFileDirectory)../{TestContext.CurrentContext.Test.Name}/obj/$(Configuration)/foo/</IntermediateOutputPath>
					  </PropertyGroup>
					</Project>"
			});
			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\foo.xml") {
				TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?><resources><string name=""foo"">bar</string></resources>",
			});
			using (var b = CreateDllBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

				var zipFile = Path.Combine (Root, b.ProjectDirectory, b.Output.IntermediateOutputPath, "foo", "__AndroidLibraryProjects__.zip");
				FileAssert.Exists (zipFile);
				using (var zip = ZipHelper.OpenZip (zipFile)) {
					Assert.IsTrue (zip.ContainsEntry ("library_project_imports/res/values/foo.xml"), $"{zipFile} should contain a library_project_imports/res/values/foo.xml entry");
				}
			}
		}

#pragma warning disable 414
		static object [] validateJavaVersionTestCases = new object [] {
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v6.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.8.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.7.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.0",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v5.0",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.0_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v7.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "1.6.x_101",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ true,
			},
			new object [] {
				/*targetFrameworkVersion*/ "v8.1",
				/*buildToolsVersion*/ "24.0.1",
				/*JavaVersion*/ "9.0.4",
				/*latestSupportedJavaVersion*/ "1.8.0",
				/*expectedResult*/ false,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource ("validateJavaVersionTestCases")]
		public void ValidateJavaVersion (string targetFrameworkVersion, string buildToolsVersion, string javaVersion, string latestSupportedJavaVersion, bool expectedResult) 
		{
			var path = Path.Combine ("temp", $"ValidateJavaVersion_{targetFrameworkVersion}_{buildToolsVersion}_{latestSupportedJavaVersion}_{javaVersion}");
			string javaExe = "java";
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "JavaSDK"), javaVersion, out javaExe, out javacExe);
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
					$"JavacToolExe={javacExe}",
					$"AndroidSdkBuildToolsVersion={buildToolsVersion}",
					$"AndroidSdkDirectory={AndroidSdkDirectory}",
					$"LatestSupportedJavaVersion={latestSupportedJavaVersion}",
				}), string.Format ("Build should have {0}", expectedResult ? "succeeded" : "failed"));
			}
			Directory.Delete (javaPath, recursive: true);
			Directory.Delete (AndroidSdkDirectory, recursive: true);
		}

		[Test]
		public void IfAndroidJarDoesNotExistThrowXA5207 ()
		{
			var path = Path.Combine ("temp", TestName);
			var AndroidSdkDirectory = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "24.0.1");
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "v8.1",
				UseLatestPlatformSdk = false,
			};
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				if (!Directory.Exists (Path.Combine (builder.FrameworkLibDirectory, "xbuild-frameworks", "MonoAndroid", "v8.1")))
					Assert.Ignore ("This is a Pull Request Build. Ignoring test.");
				builder.ThrowOnBuildFailure = false;
				builder.Verbosity = LoggerVerbosity.Diagnostic;
				builder.Target = "AndroidPrepareForBuild";
				Assert.IsFalse (builder.Build (proj, parameters: new string [] {
					$"AndroidSdkBuildToolsVersion=24.0.1",
					$"AndroidSdkDirectory={AndroidSdkDirectory}",
					$"_AndroidApiLevel=27",
				}), "Build should have failed");
				Assert.IsTrue (builder.LastBuildOutput.ContainsText ("error XA5207:"), "XA5207 should have been raised.");
				Assert.IsTrue (builder.LastBuildOutput.ContainsText ("Could not find android.jar for API Level 27"), "XA5207 should have had a good error message.");
			}
			Directory.Delete (AndroidSdkDirectory, recursive: true);
		}

		[Test]
		public void ValidateUseLatestAndroid ()
		{
			var apis = new ApiInfo [] {
				new ApiInfo () { Id = "23", Level = 23, Name = "Marshmallow", FrameworkVersion = "v6.0", Stable = true },
				new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				new ApiInfo () { Id = "P", Level = 28, Name = "P", FrameworkVersion="v8.99", Stable = false },
			};
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"),
					"23.0.6", apis);
			var referencesPath = CreateFauxReferencesDirectory (Path.Combine (path, "xbuild-frameworks"), apis);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetFrameworkVersion = "v8.0",
				UseLatestPlatformSdk = false,
			};
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
			};
			var envVar = new Dictionary<string, string>  {
				{ "XBUILD_FRAMEWORK_FOLDERS_PATH", referencesPath },
			};
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), false, false)) {
				builder.ThrowOnBuildFailure = false;
				builder.Target = "_SetLatestTargetFrameworkVersion";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("First Build should have succeeded"));

				//NOTE: these are generally of this form, from diagnostic log output:
				//    Task Parameter:TargetFrameworkVersion=v8.0
				//    ...
				//    Output Property: TargetFrameworkVersion=v8.0
				// ValidateJavaVersion and ResolveAndroidTooling take input, ResolveAndroidTooling has final output

				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.0", 2), "TargetFrameworkVersion should initially be v8.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.0", 1), "TargetFrameworkVersion should be v8.0");

				proj.TargetFrameworkVersion = "v8.0";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Second Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.0", 2), "TargetFrameworkVersion should initially be v8.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.0", 1), "TargetFrameworkVersion should be v8.0");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v8.1";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Third Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.1", 2), "TargetFrameworkVersion should initially be v8.1");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.1", 1), "TargetFrameworkVersion should be v8.1");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v8.99";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Third Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v8.99", 2), "TargetFrameworkVersion should initially be v8.99");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.99", 1), "TargetFrameworkVersion should be v8.99");

				proj.UseLatestPlatformSdk = true;
				proj.TargetFrameworkVersion = "v6.0";
				Assert.True (builder.Build (proj, parameters: parameters, environmentVariables: envVar),
					string.Format ("Forth Build should have succeeded"));
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Task Parameter:TargetFrameworkVersion=v6.0", 2), "TargetFrameworkVersion should initially be v6.0");
				Assert.IsTrue (builder.LastBuildOutput.ContainsOccurances ("Output Property: TargetFrameworkVersion=v8.1", 1), "TargetFrameworkVersion should be v8.1");
			}
			Directory.Delete (referencesPath, recursive: true);
		}

		[Test]
		[NonParallelizable]
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
			//NOTE: BuildingInsideVisualStudio prevents the projects from being built as dependencies
			app1.SetProperty ("BuildingInsideVisualStudio", "False");
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
				StringAssertEx.Contains ($"error XA4", builder.LastBuildOutput, "Error should be XA4212");
				StringAssertEx.Contains ($"Type `UnnamedProject.MyBadJavaObject` implements `Android.Runtime.IJavaObject`", builder.LastBuildOutput, "Error should mention MyBadJavaObject");
				Assert.IsTrue (builder.Build (proj, parameters: new [] { "AndroidErrorOnCustomJavaObject=False" }), "Build should have succeeded.");
				StringAssertEx.Contains ($"warning XA4", builder.LastBuildOutput, "warning XA4212");
			}
		}

		[Test]
		public void RunXABuildInParallel ()
		{
			var xabuild = new ProjectBuilder ("temp/RunXABuildInParallel").XABuildExe;
			var psi     = new ProcessStartInfo (xabuild, "/version") {
				CreateNoWindow         = true,
				RedirectStandardOutput = true,
				RedirectStandardError  = true,
				WindowStyle            = ProcessWindowStyle.Hidden,
				UseShellExecute        = false,
			};

			Parallel.For (0, 10, i => {
				using (var p = Process.Start (psi)) {
					p.WaitForExit ();
					Assert.AreEqual (0, p.ExitCode);
				}
			});
		}

		[Test]
		[TestCaseSource ("DesugarChecks")]
		public void Desugar (bool isRelease, bool enableDesugar, bool enableProguard)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				EnableDesugar = enableDesugar,
				EnableProguard = enableProguard,
			};
			/* The source is simple:
			 * 
				public class Lambda
				{
				    public void foo()
				    {
				        Runnable r = () -> System.out.println("whee");
					r.run();
				    }
				}
			 *
			 * We wanted to use AndroidJavaSource to simply compile it, but with
			 * android.jar as bootclasspath, it is impossible to compile lambdas.
			 * Therefore we compiled it without android.jar (javac Lambda.java)
			 * and then manually archived it (jar cvf Lambda.jar Lambda.class).
			 */

			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "Lambda.jar") { BinaryContent = () => Convert.FromBase64String (@"
UEsDBBQACAgIAECRZ0sAAAAAAAAAAAAAAAAJAAQATUVUQS1JTkYv/soAAAMAUEsHCAAAAAACAAAA
AAAAAFBLAwQUAAgICABBkWdLAAAAAAAAAAAAAAAAFAAAAE1FVEEtSU5GL01BTklGRVNULk1G803M
y0xLLS7RDUstKs7Mz7NSMNQz4OVyLkpNLElN0XWqBAlY6BnEGxobKmj4FyUm56QqOOcXFeQXJZYA
1WvycvFyAQBQSwcIUQoqTEQAAABFAAAAUEsDBBQACAgIACWRZ0sAAAAAAAAAAAAAAAAMAAAATGFt
YmRhLmNsYXNznVNdTxNBFD1DaafdLrQWip+gxaJdEIrfDzU+2MRIUoVYwvu0HWBhO9PszmL6s/RB
Iw/+AONvMt7pEotWiXEf5sz9OOfeuTvz9fvpFwCP8NRBFpdKtF/I4zKu5HAV17K47uAGFjmWOG4y
ZJ75yjfPGVI1b49huql7kqHQ8pV8E/c7MtwVnYA8qX2tGdxA9Ds9USWjusngtHUcduVL32bkW6PY
xpE4ES5ycBiKL7Q2kQnF4LU0h7oXFTK4VYRDUHGxjNscVYsOx4qLO7hLDbw7lJKj5sLDKrWXiJKU
la0HQh3UtztHsmscrOGeA451ai6MFcNCzWuNs97GStnWGwylSe8vgu1hZGSfZHRsGMqJiK/rO6Gv
TNuEUvRJZe4PbgY+sFZA5cu1c9Up7KuDhrfHseGijocuZu1ElscpvjrRx7KeHJDmI/ZF1+hwSJPs
jy2Ox3YKWh/HA5r/llIybAYiimTE8O18yTO9ZNKvhOoFMqomxMZkZ38j7g4H8v+CScmLud5ktCmC
oO0b2eB4wrDyT+dhWLo4DxW6GFnYLwVmLyOtebIWCRlhevUT2Hva0ExpzYycNjTzM3WdcIpw5tRC
a+0zUgxjyiwpkw5RM2TzomN/8Bm1MiICuQ+YLqU/IvN7pTSRC4RTKOIBoSVu0pO9T0/UPliX7DnK
mUcZ8z8AUEsHCLuHtAn+AQAA0QMAAFBLAQIUABQACAgIAECRZ0sAAAAAAgAAAAAAAAAJAAQAAAAA
AAAAAAAAAAAAAABNRVRBLUlORi/+ygAAUEsBAhQAFAAICAgAQZFnS1EKKkxEAAAARQAAABQAAAAA
AAAAAAAAAAAAPQAAAE1FVEEtSU5GL01BTklGRVNULk1GUEsBAhQAFAAICAgAJZFnS7uHtAn+AQAA
0QMAAAwAAAAAAAAAAAAAAAAAwwAAAExhbWJkYS5jbGFzc1BLBQYAAAAAAwADALcAAAD7AgAAAAA=
				") });
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestContext.CurrentContext.Test.Name))) {
				builder.ThrowOnBuildFailure = enableDesugar;
				Assert.AreEqual (enableDesugar, builder.Build (proj), "Unexpected build result");
				Assert.IsFalse (builder.LastBuildOutput.ContainsText ("Duplicate zip entry"), "Should not get warning about [META-INF/MANIFEST.MF]");
				
				if (enableDesugar) {
					var className = "Lmono/MonoRuntimeProvider;";
					var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
					Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, builder.AndroidSdkDirectory), $"`{dexFile}` should include `{className}`!");
				}
			}
		}
	}
}

