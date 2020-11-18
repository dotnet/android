using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] //These tests deploy to devices
	[Category ("Commercial"), Category ("UsesDevices")]
	public class InstantRunTest : BaseTest
	{
		[Test]
		public void InstantRunSimpleBuild ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinFormsAndroidApplicationProject {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

			var manifest = b.Output.GetIntermediaryAsText (BuildOutputFiles.AndroidManifest);
			Assert.IsTrue (File.Exists (b.Output.GetIntermediaryPath ("android/bin/dex/mono.android.dex")), "there should be mono.android.dex in the intermediaries.");

			using (var apk = ((AndroidApplicationBuildOutput) b.Output).OpenApk ()) {
				var dexFile = Path.GetTempFileName ();
				File.WriteAllBytes (dexFile, apk.GetRaw (ApkContents.ClassesDex));
				try {
					string className = "Lcom/xamarin/forms/platform/android/FormsViewGroup;";
					Assert.IsFalse (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should *not* include `{className}`!");
					className = "Lmono/MonoRuntimeProvider;";
					Assert.IsFalse (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
					className = "Lmono/MonoPackageManager;";
					Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
				} finally {
					File.Delete (dexFile);
				}
			}

			b.Dispose ();
		}

		[Test]
		public void TargetsSkipped ([Values(false, true)] bool useManagedResourceGenerator)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () { AndroidFastDeploymentType = "Assemblies:Dexes", UseLatestPlatformSdk = true };
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedResourceGenerator.ToString ());
			var b = CreateApkBuilder ($"temp/InstantRunTargetsSkipped_{useManagedResourceGenerator}", cleanupOnDispose: false);
			Assert.IsTrue (b.Build (proj), "1 build should have succeeded.");

			// unchanged build
			Assert.IsTrue (b.Build (proj, true, null, false), "2 build should have succeeded.");
			// _UpdateAndroidResgen should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "2 _UpdateAndroidResgen was not skipped");
			// CoreCompile should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("CoreCompile"), "2 CoreCompile was not skipped");
			// _CompileToDalvik should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvik"), "2 _CompileToDalvik was not skipped");

			// make insignificant changes in C# code and build
			proj.MainActivity = proj.DefaultMainActivity + "// extra";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (b.Build (proj, true, null, false), "3 build should have succeeded.");
			// _UpdateAndroidResgen should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "3 _UpdateAndroidResgen was not skipped");
			// CoreCompile should be built.
			Assert.IsTrue (!b.Output.IsTargetSkipped ("CoreCompile"), "3 CoreCompile was skipped");
			// _CompileToDalvik should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvik"), "3 _CompileToDalvik was not skipped");

			// make significant changes (but doesn't impact Resource.Designer.cs) in layout XML resource and build
			proj.LayoutMain = proj.LayoutMain.Replace ("LinearLayout", "RelativeLayout"); // without this, resource designer .cs will be identical and further tasks will be skipped.
			proj.Touch ("Resources\\layout\\Main.axml");
			Assert.IsTrue (b.Build (proj, true, null, false), "4 build should have succeeded.");
			// _UpdateAndroidResgen should be built.
			Assert.IsTrue (!b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "4 _UpdateAndroidResgen was skipped");
			// CoreCompile should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("CoreCompile"), "4 CoreCompile was not skipped");
			// _CompileToDalvik should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvik"), "4 _CompileToDalvik was not skipped");

			// make significant changes (but doesn't impact Resource.Designer.cs) in layout XML resource,
			// then call AndroidUpdateResource, then build (which is what our IDEs do).
			proj.LayoutMain = proj.LayoutMain.Replace ("RelativeLayout", "LinearLayout"); // change it again
			proj.Touch ("Resources\\layout\\Main.axml");
			b.Target = "Compile";
			var designTimeParams = new string [] { "BuildingInsideVisualStudio=true", "BuildProject=false" };
			Assert.IsTrue (b.Build (proj, true, designTimeParams, false), "5 update resources should have succeeded.");
			// _UpdateAndroidResgen should be built.
			if (useManagedResourceGenerator)
				Assert.IsTrue (!b.Output.IsTargetSkipped ("_ManagedUpdateAndroidResgen"), $"5 first _ManagedUpdateAndroidResgen was skipped");
			else
				Assert.IsTrue (!b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), $"5 first _UpdateAndroidResgen was skipped");
			b.Target = "Build";
			Assert.IsTrue (b.Build (proj, true, null, false), "5 build should have succeeded.");
			// _UpdateAndroidResgen should not be built.
			if (useManagedResourceGenerator) {
				Assert.IsFalse (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "5 second _UpdateAndroidResgen was skipped");
				// CoreCompile should be built.
				Assert.IsTrue (b.Output.IsTargetSkipped ("CoreCompile"), "5 CoreCompile was not skipped");
			} else {
				Assert.IsTrue (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "5 second _UpdateAndroidResgen was not skipped");
				// CoreCompile should not be built.
				Assert.IsTrue (b.Output.IsTargetSkipped ("CoreCompile"), "5 CoreCompile was not skipped");
			}

			// _CompileToDalvik should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvik"), "5 _CompileToDalvik should be skipped");

			b.Dispose ();
		}

		[Test]
		public void SimpleInstallAndUninstall ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			proj.SetDefaultTargetDevice ();
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Install (proj), "install should have succeeded.");
			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		[Test]
		public void SkipFastDevAlreadyInstalledFile ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			proj.SetDefaultTargetDevice ();
			proj.PackageReferences.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			proj.PackageReferences.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			proj.MainActivity = proj.DefaultMainActivity.Replace (": Activity", ": Android.Support.V7.App.AppCompatActivity");
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Install (proj), "install should have succeeded.");
			File.WriteAllLines (Path.Combine (Root, b.ProjectDirectory, b.BuildLogFile + ".bak"), b.LastBuildOutput);

			// slightly (but significantly) modify the sources that causes dll changes.
			proj.MainActivity = proj.MainActivity.Replace ("clicks", "CLICKS");
			proj.Touch ("MainActivity.cs");
			// make sure that the fastdev log tells that the relevant dll is updated but NOT for others.
			Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded.");
			Assert.IsFalse (b.Output.IsApkInstalled, "app apk was installed");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("UnnamedProject.dll") && l.Contains ("NotifySync CopyFile")), "app dll not uploaded");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("Xamarin.Android.Support.v4.dll") && l.Contains ("NotifySync SkipCopyFile")), "v4 should be skipped, but no relevant log line");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("Xamarin.Android.Support.v7.AppCompat.dll") && l.Contains ("NotifySync SkipCopyFile")), "v7 should be skipped, but no relevant log line");

			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		#pragma warning disable 414
		static object [] SkipFastDevAlreadyInstalledResourcesSource = new object [] {
			new object[] { new Package [0], null },
			new object[] { new Package [] { KnownPackages.AndroidSupportV4_27_0_2_1, KnownPackages.SupportV7AppCompat_27_0_2_1}, "Android.Support.V7.App.AppCompatActivity" },
		};
		#pragma warning restore 414

		[Test]
		[TestCaseSource ("SkipFastDevAlreadyInstalledResourcesSource")] // test for both cases that there is external resources or there are some.
		public void SkipFastDevAlreadyInstalledResources (Package [] packages, string baseActivityClass)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () { AndroidFastDeploymentType = "Assemblies:Dexes", UseLatestPlatformSdk = true };
			proj.SetDefaultTargetDevice ();
			foreach (var pkg in packages)
				proj.PackageReferences.Add (pkg);
			if (baseActivityClass != null)
					proj.MainActivity = proj.DefaultMainActivity.Replace (": Activity", ": " + baseActivityClass);
			var b = CreateApkBuilder ("temp/SkipFastDevAlreadyInstalledResources");
			Assert.IsTrue (b.Install (proj), "install should have succeeded.");

			// slightly (but significantly) modify the resources that causes packaged_resources changes.
			proj.LayoutMain = proj.LayoutMain.Replace ("LinearLayout", "RelativeLayout");
			proj.Touch ("Resources\\Layout\\Main.axml");
			Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded.");
			Assert.IsTrue (b.Output.IsApkInstalled, "app apk was installed");

			var axml = System.Text.Encoding.UTF8.GetString (ZipHelper.ReadFileFromZip (b.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "packaged_resources")), "res/layout/main.xml"));
			Assert.IsTrue (axml.Contains ("RelativeLayout"), "The packaged resources seem to be out of sync.");

			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		[Test]
		public void InstantRunResourceChange ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Install (proj), "install should have succeeded. 0");
				var logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (b.Output.IsApkInstalled, "app apk was installed");


				proj.LayoutMain = proj.LayoutMain.Replace ("LinearLayout", "RelativeLayout");
				proj.Touch ("Resources\\Layout\\Main.axml");
				Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded. 1");
				logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should not have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (b.Output.IsApkInstalled, "app apk was installed");
			}
		}

		[Test]
		public void InstantRunFastDevTypemaps ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Install (proj), "packaging should have succeeded. 0");
				var apk = Path.Combine (Root, b.ProjectDirectory,
					proj.IntermediateOutputPath, "android", "bin", "UnnamedProject.UnnamedProject.apk");
				Assert.IsNull (ZipHelper.ReadFileFromZip (apk, "Mono.Android.typemap"), $"Mono.Android.typemap should NOT be in {apk}.");
				var logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("Mono.Android.typemap")), "Mono.Android.typemap should have been uploaded");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("typemap.index")), "typemap.index should have been uploaded");
			}
		}

		[Test]
		public void InstantRunNativeLibrary ([Values ("dx", "d8")] string dexTool)
		{
			AssertDexToolSupported (dexTool);
			AssertCommercialBuild ();
			AssertHasDevices ();

			var nativeLib = new AndroidItem.AndroidNativeLibrary ($"foo\\{DeviceAbi}\\libtest.so") {
				BinaryContent = () => new byte [10],
				MetadataValues = $"Link=libs\\{DeviceAbi}\\libtest.so",
			};
			var proj = new XamarinAndroidApplicationProject () {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
				OtherBuildItems = {
					nativeLib,
				},
			};
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue (b.Install (proj), "install should have succeeded. 0");
				var logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("libtest.so")), "libtest.so should have been uploaded");

				nativeLib.BinaryContent = () => new byte [20];
				nativeLib.Timestamp = DateTime.UtcNow.AddSeconds(1);
				Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded. 1");
				logLines = b.LastBuildOutput;
				Assert.IsFalse (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should not have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("libtest.so")), "libtest.so should have been uploaded");
			}
		}
	}
}
