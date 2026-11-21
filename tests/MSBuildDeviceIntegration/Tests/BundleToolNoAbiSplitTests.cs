using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[TestCategory ("UsesDevice")]
	public class BundleToolNoAbiSplitTests : DeviceTest
	{
		static readonly string [] Abis = ["arm64-v8a", "x86_64"];

		XamarinAndroidApplicationProject app;
		ProjectBuilder appBuilder;
		string intermediate;
		string bin;

		// Disable split by ABI
		const string BuildConfig = @"{
  ""compression"": { },
    ""optimizations"": {
      ""splitsConfig"": {
        ""splitDimension"": [
          {
            ""value"": ""ABI"",
            ""negate"": true
          }
        ],
      }
    }
}";
		AndroidItem.AndroidAsset MakeAndroidAsset (string include, string content, string? assetPack = null, string? deliveryType = null)
		{
			var ret = new AndroidItem.AndroidAsset (include) {
				TextContent = () => content,
			};

			if (!String.IsNullOrEmpty (assetPack)) {
				ret.Metadata["AssetPack"] = assetPack;
			}

			if (!String.IsNullOrEmpty (deliveryType)) {
				ret.Metadata["DeliveryType"] = deliveryType;
			}

			return ret;
		}

		void BuildApp (AndroidRuntime runtime)
		{
			var path = Path.Combine ("temp", TestName);

			app = new XamarinFormsMapsApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.bundletoolnoabisplittests",
			};
			app.SetRuntime (runtime);
			app.OtherBuildItems.Add (
				MakeAndroidAsset (
					include: "Assets\\Textures\\asset1.txt",
					content: "Asset1"
				)
			);
			app.OtherBuildItems.Add (
				MakeAndroidAsset (
					include: "Assets\\Textures#tcf_astc\\asset3.txt",
					content: "Asset3astc",
					assetPack: "assetpack1",
					deliveryType: "InstallTime"
				)
			);
			app.OtherBuildItems.Add (
				MakeAndroidAsset (
					include: "Assets\\Textures#tcf_paletted\\asset3.txt",
					content: "Asset3",
					assetPack: "assetpack1"
				)
			);
			app.OtherBuildItems.Add (
				MakeAndroidAsset (
					include: "Assets\\Textures#tcf_etc1\\asset3.txt",
					content: "Asset3etc1",
					assetPack: "assetpack1"
				)
			);
			app.OtherBuildItems.Add (
				MakeAndroidAsset (
					include: "Assets\\Textures#tcf_dxt1\\asset3.txt",
					content: "Asset3Dx1",
					assetPack: "assetpack1"
				)
			);
			app.OtherBuildItems.Add (new BuildItem ("None", "buildConfig.config") {
				TextContent = () => BuildConfig,
			});

			//NOTE: this is here to enable adb shell run-as
			app.AndroidManifest = app.AndroidManifest.Replace ("<application ", "<application android:debuggable=\"true\" ");
			app.SetProperty (app.ReleaseProperties, "AndroidPackageFormat", "aab");
			app.SetRuntimeIdentifiers (Abis);
			app.SetProperty ("AndroidBundleConfigurationFile", "buildConfig.config");
			app.SetProperty ("_FastDeploymentDiagnosticLogging", "true");

			appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName), cleanupOnDispose: true);
			Assert.IsTrue (appBuilder.Build (app), "App build should have succeeded.");

			var projectDir = Path.Combine (Root, appBuilder.ProjectDirectory);
			intermediate = Path.Combine (projectDir, app.IntermediateOutputPath);
			bin = Path.Combine (projectDir, app.OutputPath);

			string objPath = Path.Combine (Root, appBuilder.ProjectDirectory, app.IntermediateOutputPath);
			if (runtime == AndroidRuntime.NativeAOT) {
				EnvironmentHelper.ReadNativeAotEnvironmentVariables (objPath);
			} else {
				EnvironmentHelper.ApplicationConfig app_config = EnvironmentHelper.ReadCoreClrBootstrap (objPath).Config;
				Assert.IsTrue (app_config.ignore_split_configs, $"App config should indicate that split configs must be ignored");
			}
		}

		[TestCleanup]
		public void TearDown ()
		{
			if (TestContext.CurrentTestOutcome == UnitTestOutcome.Failed) {
				if (appBuilder != null)
					appBuilder.CleanupOnDispose = false;
			}
			appBuilder?.Dispose ();
		}

		[TestMethod]
		[DataRow (AndroidRuntime.CoreCLR)]
		[DataRow (AndroidRuntime.NativeAOT)]
		public void InstallAndRun (AndroidRuntime runtime)
		{
			BuildApp (runtime);

			if (runtime == AndroidRuntime.NativeAOT) {
				// TODO: Xamarin.Forms startup cannot find the ResourcesProvider constructor under NativeAOT.
				Assert.Inconclusive ("NativeAOT Xamarin.Forms startup throws MissingMethodException.");
			}
			Assert.IsTrue (appBuilder.Install (app), "Install should have succeeded.");
			RunProjectAndAssert (app, appBuilder);
			Assert.IsTrue (
				WaitForActivityToStart (app.PackageName, "MainActivity", Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds),
				"Activity should have started."
			);
		}
	}
}
