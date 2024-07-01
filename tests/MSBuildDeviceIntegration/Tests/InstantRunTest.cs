using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class InstantRunTest : DeviceTest
	{
		[Test]
		public void InstantRunSimpleBuild ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinFormsAndroidApplicationProject {
			};
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Clean (proj), "Clean should have succeeded.");
			Assert.IsTrue (b.Build (proj), "Build should have succeeded.");

			using (var apk = ((AndroidApplicationBuildOutput) b.Output).OpenApk ()) {
				var dexFile = Path.GetTempFileName ();
				File.WriteAllBytes (dexFile, apk.GetRaw (ApkContents.ClassesDex));
				try {
					string className = "Lcom/xamarin/forms/platform/android/FormsViewGroup;";
					Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
					className = "Lmono/MonoRuntimeProvider;";
					Assert.IsTrue (DexUtils.ContainsClass (className, dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}`!");
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

			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", useManagedResourceGenerator.ToString ());
			proj.SetProperty ("AndroidUseDesignerAssembly", "False");
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
		public void SimpleInstallAndUninstall ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject {
			};
			proj.SetDefaultTargetDevice ();
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Install (proj), "install should have succeeded.");
			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		[Test]
		public void SkipFastDevAlreadyInstalledFile ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject {
			};
			proj.SetDefaultTargetDevice ();
			proj.PackageReferences.Add (KnownPackages.AndroidXAppCompat);
			proj.MainActivity = proj.DefaultMainActivity.Replace (": Activity", ": AndroidX.AppCompat.App.AppCompatActivity");
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			b.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (b.Install (proj), "install should have succeeded.");
			File.WriteAllLines (Path.Combine (Root, b.ProjectDirectory, b.BuildLogFile + ".bak"), b.LastBuildOutput);

			// slightly (but significantly) modify the sources that causes dll changes.
			proj.MainActivity = proj.MainActivity.Replace ("clicks", "CLICKS");
			proj.Touch ("MainActivity.cs");
			// make sure that the fastdev log tells that the relevant dll is updated but NOT for others.
			Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded.");
			Assert.IsFalse (b.Output.IsApkInstalled, "app apk was installed");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("UnnamedProject.dll") && l.Contains ("NotifySync CopyFile")), "app dll not uploaded");

			var assemblies = new[] {
				"Xamarin.AndroidX.AppCompat.dll",
				"Xamarin.AndroidX.Core.dll",
			};
			foreach (var assembly in assemblies) {
				Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains (assembly) && l.Contains ("NotifySync SkipCopyFile")), $"{assembly} should be skipped, but no relevant log line");
			}

			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		#pragma warning disable 414
		static object [] SkipFastDevAlreadyInstalledResourcesSource = new object [] {
			new object[] { Array.Empty<Package> (), null },
			new object[] { new Package [] { KnownPackages.AndroidXAppCompat }, "AndroidX.AppCompat.App.AppCompatActivity" },
		};
		#pragma warning restore 414

		[Test]
		[TestCaseSource ("SkipFastDevAlreadyInstalledResourcesSource")] // test for both cases that there is external resources or there are some.
		public void SkipFastDevAlreadyInstalledResources (Package [] packages, string baseActivityClass)
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.SetDefaultTargetDevice ();
			foreach (var pkg in packages)
				proj.PackageReferences.Add (pkg);
			if (baseActivityClass != null)
					proj.MainActivity = proj.DefaultMainActivity.Replace (": Activity", ": " + baseActivityClass);
			var b = CreateApkBuilder ("temp/SkipFastDevAlreadyInstalledResources");
			b.Verbosity = LoggerVerbosity.Detailed;
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
		public void InstantRunResourceChange ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Detailed;
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
		public void InstantRunFastDevDexes ([Values (false, true)] bool useEmbeddedDex)
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.SetDefaultTargetDevice ();
			proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", $"<application android:useEmbeddedDex=\"{useEmbeddedDex.ToString ().ToLowerInvariant ()}\" ");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (b.Install (proj), "packaging should have succeeded. 0");
				var logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				ClearAdbLogcat ();
				RunProjectAndAssert (proj, b);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				b.BuildLogFile = "uninstall.log";
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}
	}
}
