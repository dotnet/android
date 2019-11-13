using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable] //These tests deploy to devices
	[Category ("Commercial")]
	public class InstantRunTest : BaseTest
	{
		[Test]
		public void InstantRunSimpleBuild ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}

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
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}

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
			// _CompileToDalvikWithDx should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvikWithDx"), "2 _CompileToDalvikWithDx was not skipped");

			// make insignificant changes in C# code and build
			proj.MainActivity = proj.DefaultMainActivity + "// extra";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (b.Build (proj, true, null, false), "3 build should have succeeded.");
			// _UpdateAndroidResgen should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "3 _UpdateAndroidResgen was not skipped");
			// CoreCompile should be built.
			Assert.IsTrue (!b.Output.IsTargetSkipped ("CoreCompile"), "3 CoreCompile was skipped");
			// _CompileToDalvikWithDx should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvikWithDx"), "3 _CompileToDalvikWithDx was not skipped");

			// make significant changes (but doesn't impact Resource.Designer.cs) in layout XML resource and build
			proj.LayoutMain = proj.LayoutMain.Replace ("LinearLayout", "RelativeLayout"); // without this, resource designer .cs will be identical and further tasks will be skipped.
			proj.Touch ("Resources\\layout\\Main.axml");
			Assert.IsTrue (b.Build (proj, true, null, false), "4 build should have succeeded.");
			// _UpdateAndroidResgen should be built.
			Assert.IsTrue (!b.Output.IsTargetSkipped ("_UpdateAndroidResgen"), "4 _UpdateAndroidResgen was skipped");
			// CoreCompile should not be built.
			Assert.IsTrue (b.Output.IsTargetSkipped ("CoreCompile"), "4 CoreCompile was not skipped");
			// _CompileToDalvikWithDx should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvikWithDx"), "4 _CompileToDalvikWithDx was not skipped");

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

			// _CompileToDalvikWithDx should not be built either.
			Assert.IsTrue (b.Output.IsTargetSkipped ("_CompileToDalvikWithDx"), "5 _CompileToDalvikWithDx should be skipped");

			b.Dispose ();
		}

		[Test]
		public void SimpleInstallAndUninstall ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices)
				return;

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
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices)
				return;

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
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("packaged_resources") && l.Contains ("NotifySync SkipCopyFile")), "packaged_resources should be skipped, but no relevant log line");

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
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices)
				return;

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
			Assert.IsFalse (b.Output.IsApkInstalled, "app apk was installed");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("packaged_resources") && l.Contains ("NotifySync CopyFile")), "packaged_resources not uploaded");

			var axml = System.Text.Encoding.UTF8.GetString (ZipHelper.ReadFileFromZip (b.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "packaged_resources")), "res/layout/main.xml"));
			Assert.IsTrue (axml.Contains ("RelativeLayout"), "The packaged resources seem to be out of sync.");

			Assert.IsTrue (b.Uninstall (proj), "uninstall should have succeeded.");
			b.Dispose ();
		}

		[Test]
		public void InstantRunResourceChange ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}
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
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("packaged_resources")), "packaged_resources should have been uploaded");

				var layout = proj.AndroidResources.First (x => x.Include () == "Resources\\layout\\Main.axml");
				layout.Timestamp = DateTime.UtcNow;
				Assert.IsTrue (b.Install (proj, doNotCleanupOnUpdate: true, saveProject: false), "install should have succeeded. 1");
				logLines = b.LastBuildOutput;
				Assert.IsFalse (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should not have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("packaged_resources")), "packaged_resources should have been uploaded");
			}
		}

		[Test]
		public void InstantRunFastDevTypemaps ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}
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
				Assert.IsNull (ZipHelper.ReadFileFromZip (apk, "typemap.jm"), $"typemap.jm should NOT be in {apk}.");
				Assert.IsNull (ZipHelper.ReadFileFromZip (apk, "typemap.mj"), $"typemap.mj should NOT be in  {apk}.");
				var logLines = b.LastBuildOutput;
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_BuildApkFastDev\" completely.") ||
					l.Contains ("Target _BuildApkFastDev needs to be built")),
					"Apk should have been built");
				Assert.IsTrue (logLines.Any (l => l.Contains ("Building target \"_Upload\" completely")), "_Upload target should have run");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("typemap.jm")), "typemap.jm should have been uploaded");
				Assert.IsTrue (logLines.Any (l => l.Contains ("NotifySync CopyFile") && l.Contains ("typemap.mj")), "typemap.mj should have been uploaded");
			}
		}

		[Test]
		public void InstantRunNativeLibrary ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}
			var nativeLib = new AndroidItem.AndroidNativeLibrary ("foo\\x86\\libtest.so") {
				BinaryContent = () => new byte [10],
				MetadataValues = "Link=libs\\x86\\libtest.so",
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

		[Test]
		public void DisableInstantRunWhenAndroidManifestHasChanged ([Values ("dx", "d8")] string dexTool)
		{
			if (!CommercialBuildAvailable)
				Assert.Ignore ("Not required on Open Source Builds");

			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				AndroidFastDeploymentType = "Assemblies:Dexes",
				UseLatestPlatformSdk = true,
				DexTool = dexTool,
			};
			proj.SetDefaultTargetDevice ();
			var b = CreateApkBuilder (Path.Combine ("temp", TestName));
			Assert.IsTrue (b.Build (proj), "packaging should have succeeded. 0");

			// modify AndroidManifest.xml.
			// (Cannot simple rename from "UnnamedProject" because it is a template. Also cannot simply set ProjectName once we built it...)
			proj.AndroidManifest = proj.AndroidManifest.Replace ("${PROJECT_NAME}", "RenamedProject");
			proj.Touch ("Properties\\AndroidManifest.xml");
			// make sure that the fastdev log tells that it actually disabled fastdev.
			Assert.IsTrue (b.Build (proj, true, null, false), "packaging should have succeeded. 1");
			Assert.IsTrue (b.Output.AreTargetsAllBuilt ("_ExamineAndroidManifestFileUpdates"),
				"AndroidManifest update check did not run. 1");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("Triggered force apk reinstallation")), "failed to detect AndroidManifest updates. 1");

			// modify AndroidManifest.xml *to reference @app_name*.
			proj.AndroidManifest = proj.AndroidManifest.Replace ("android:label=\"RenamedProject\"", "android:label=\"@string/app_name\"");
			proj.Touch ("Properties\\AndroidManifest.xml");
			// make sure that the fastdev log tells that it actually disabled fastdev.
			Assert.IsTrue (b.Build (proj, true, null, false), "packaging should have succeeded. 2");
			Assert.IsTrue (b.Output.AreTargetsAllBuilt ("_ExamineAndroidManifestFileUpdates"),
				$"AndroidManifest update check did not run. 2. {string.Join (Environment.NewLine, b.LastBuildOutput )}");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("Triggered force apk reinstallation")), "failed to detect AndroidManifest updates. 2");

			// Change app_name in Resources/values/Strings.xml that should trigger AndroidManifest.xml updates.
			Assert.IsTrue (proj.StringsXml.Contains ("<string name=\"app_name\">${PROJECT_NAME}</string>"), "premise not met: StringsXml: " + proj.StringsXml);
			proj.StringsXml = proj.StringsXml.Replace ("<string name=\"app_name\">${PROJECT_NAME}</string>", "<string name=\"app_name\">UnnamedProjectChanged</string>");
			proj.Touch ("Resources\\values\\Strings.xml");
			// make sure that the fastdev log tells that it actually disabled fastdev.
			Assert.IsTrue (b.Build (proj, true, null, false), "install should have succeeded. 3");
			Assert.IsTrue (b.Output.AreTargetsAllSkipped ("_ExamineAndroidManifestFileUpdates"),
				"AndroidManifest file update check resulted in failure. 3");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("Triggered force apk reinstallation")), "failed to detect AndroidManifest updates. 3");
			Assert.IsTrue (b.LastBuildOutput.Any (l => l.Contains ("ShouldTriggerForceUpdates: True")), "failed to disable Instant Run. 3");
		}
	}
}
