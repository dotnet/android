using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture (AndroidRuntime.MonoVM)]
	[TestFixture (AndroidRuntime.CoreCLR)]
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
				AotAssemblies = false, // Release defaults to Profiled AOT for .NET 6
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
                        List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (
                                objPath,
                                String.Join (";", Abis),
                                true
                        );

                        EnvironmentHelper.IApplicationConfig app_config = EnvironmentHelper.ReadApplicationConfig (envFiles, runtime);

			Assert.That (app_config, Is.Not.Null, "application_config must be present in the environment files");

			bool ignoreSplitConfigs = runtime switch {
				AndroidRuntime.MonoVM  => ((EnvironmentHelper.ApplicationConfig_MonoVM)app_config).ignore_split_configs,
				AndroidRuntime.CoreCLR => ((EnvironmentHelper.ApplicationConfig_CoreCLR)app_config).ignore_split_configs,
				_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
			};
                        Assert.AreEqual (ignoreSplitConfigs, true, $"App config should indicate that split configs must be ignored");
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
			Assert.IsTrue (appBuilder.Install (app), "Install should have succeeded.");
			RunProjectAndAssert (app, appBuilder);
			Assert.True (
				WaitForActivityToStart (app.PackageName, "MainActivity", Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), 30),
				"Activity should have started."
			);
		}
	}
}
