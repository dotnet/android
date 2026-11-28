using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Xamarin.ProjectTools;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[TestCategory ("UsesDevice")]
	public class DebuggingTest : DeviceTest
	{
		const int DEBUGGER_MAX_CONNECTIONS = 100;
		const int DEBUGGER_CONNECTION_TIMEOUT = 3000;

		void SetTargetFrameworkAndManifest(XamarinAndroidApplicationProject proj, Builder builder, int? apiLevelOverride)
		{
			builder.LatestTargetFrameworkVersion (out string apiLevel);
			proj.SupportedOSPlatformVersion = "24";
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevelOverride?.ToString () ?? apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
		}

		int FindTextInFile (string file, string text)
		{
			int lineNumber = 1;
			foreach (var line in File.ReadAllLines (file)) {
				if (line.Contains (text)) {
					return lineNumber;
				}
				lineNumber++;
			}
			Console.WriteLine ($"Could not find '{text}' in '{file}'");
			return -1;
		}

		static IEnumerable<object []> ApplicationRunsWithoutDebuggerCases ()
		{
			foreach (var configuration in BaseTest.ValidRuntimeConfigurations) {
				foreach (var extractNativeLibs in new [] { false, true }) {
					foreach (var useEmbeddedDex in new [] { false, true }) {
						// Release normalizes embedded DEX to false; retain the existing NativeAOT skip cases.
						if (configuration.isRelease && configuration.runtime == AndroidRuntime.CoreCLR && useEmbeddedDex) {
							continue;
						}
						yield return [configuration, extractNativeLibs, useEmbeddedDex];
					}
				}
			}
		}

		[TestMethod]
		[DynamicData (nameof (ApplicationRunsWithoutDebuggerCases))]
		public void ApplicationRunsWithoutDebugger ((bool isRelease, AndroidRuntime runtime) configuration, bool extractNativeLibs, bool useEmbeddedDex)
		{
			var (isRelease, runtime) = configuration;

			if (runtime == AndroidRuntime.NativeAOT) {
				// TODO: Xamarin.Forms startup cannot find the ResourcesProvider constructor under NativeAOT.
				Assert.Inconclusive ("NativeAOT Xamarin.Forms startup throws MissingMethodException.");
			}

			SwitchUser ();

			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidEnableLegacyCompatibilityAssemblyFixups", "true");
			if (isRelease) {
				proj.SetRuntimeIdentifiers (new[] { DeviceAbi });
			}
			proj.SetDefaultTargetDevice ();
			if (isRelease) {
				// bundle tool does NOT support embeddedDex files it seems.
				useEmbeddedDex = false;
			}
			using (var b = CreateApkBuilder ()) {
				SetTargetFrameworkAndManifest (proj, b, null);
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" android:useEmbeddedDex=\"{useEmbeddedDex.ToString ().ToLowerInvariant ()}\" ");
				Assert.IsTrue (b.Install (proj), "Project should have installed.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				AssertExtractNativeLibs (manifest, extractNativeLibs);
				RunProjectAndAssert (proj, b);
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds), "Activity should have started.");
				b.BuildLogFile = "uninstall.log";
				Assert.IsTrue (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[TestMethod]
		[CombinatorialData]
		public void ClassLibraryMainLauncherRuns ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			SwitchUser ();

			var path = Path.Combine ("temp", TestName);

			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};
			app.SetRuntime (runtime);
			app.SetDefaultTargetDevice ();

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary"
			};
			lib.Sources.Add (new BuildItem.Source ("MainActivity.cs") {
				TextContent = () => lib.ProcessSourceTemplate (app.DefaultMainActivity).Replace ("${JAVA_PACKAGENAME}", app.JavaPackageName),
			});
			lib.AndroidResources.Clear ();
			foreach (var resource in app.AndroidResources) {
				lib.AndroidResources.Add (resource);
			}
			var reference = $"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj";
			app.References.Add (new BuildItem.ProjectReference (reference, lib.ProjectName, lib.ProjectGuid));

			// Remove the default MainActivity.cs & AndroidResources
			app.AndroidResources.Clear ();
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\foo.xml") {
				TextContent = () =>
@"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout
  xmlns:android=""http://schemas.android.com/apk/res/android""
  android:layout_width=""fill_parent""
  android:layout_height=""wrap_content""
/>"
			});
			app.Sources.Remove (app.GetItem ("MainActivity.cs"));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				SetTargetFrameworkAndManifest (app, appBuilder, null);
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.IsTrue (appBuilder.Install (app), "app should have installed.");
				RunProjectAndAssert (app, appBuilder);
				Assert.IsTrue (WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds), "Activity should have started.");
			}
		}

	}
}
