using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixtureSource (nameof (Get_ConstructorParameters))]
	[Category ("UsesDevice")]
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
		readonly AndroidRuntime runtime;

		static Array Get_ConstructorParameters ()
		{
			return new[] { AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT };
		}

		public BundleToolNoAbiSplitTests (AndroidRuntime runtime)
		{
			this.runtime = runtime;
		}

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

		[OneTimeSetUp]
		public void OneTimeSetUp ()
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

		[TearDown]
		public void TearDown ()
		{
			var status = TestContext.CurrentContext.Result.Outcome.Status;
			if (status == NUnit.Framework.Interfaces.TestStatus.Failed) {
				if (appBuilder != null)
					appBuilder.CleanupOnDispose = false;
			}
		}

		[OneTimeTearDown]
		public void OneTimeTearDown ()
		{
			appBuilder?.Dispose ();
		}

		[Test]
		public void InstallAndRun ()
		{
			if (runtime == AndroidRuntime.NativeAOT) {
				// TODO: Xamarin.Forms startup cannot find the ResourcesProvider constructor under NativeAOT.
				Assert.Ignore ("NativeAOT Xamarin.Forms startup throws MissingMethodException.");
			}
			Assert.IsTrue (appBuilder.Install (app), "Install should have succeeded.");
			RunProjectAndAssert (app, appBuilder);
			Assert.True (
				WaitForActivityToStart (app.PackageName, "MainActivity", Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), ActivityStartTimeoutInSeconds),
				"Activity should have started."
			);
		}
	}
}
