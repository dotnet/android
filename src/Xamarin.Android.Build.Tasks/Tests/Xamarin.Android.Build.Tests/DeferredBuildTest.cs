using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using System.IO;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	public class DeferredBuildTest : BaseTest
	{
		[TestMethod]
		[CombinatorialData]
		public void SelectivelyRunUpdateAndroidResources ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};

			app.SetRuntime (runtime);
			app.SetProperty ("AndroidUseIntermediateDesignerFile", "True");

			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				appBuilder.Verbosity = LoggerVerbosity.Detailed;
				Assert.IsTrue (appBuilder.DesignTimeBuild (app, parameters: new string[]{
					"BuildingInsideVisualStudio=true",
					"DeferredBuildSupported=true",
					// The DTB passes down these two properties
					"SkipCompilerExecution=true",
					"ProvideCommandLineArgs=true",
				}), "first app build should have succeeded.");

				Assert.IsTrue (appBuilder.Output.IsTargetSkipped ("UpdateAndroidResources", defaultIfNotUsed: true), $"`UpdateAndroidResources` should be skipped for DTB when deferred build is supported!");

				// The background build would run our UpdateAndroidResources in its DeferredBuildDependsOn
				Assert.IsTrue (appBuilder.RunTarget (app, "UpdateAndroidResources", parameters: new string[]{
					"BuildingInsideVisualStudio=true",
					// DeferredBuild targets set these two
					"DeferredBuild=true",
					"DeferredBuildSupported=true",
					// The DTB passes down these two properties
					"SkipCompilerExecution=true",
					"ProvideCommandLineArgs=true",
				}), "background build should have succeeded.");

				Assert.IsFalse (appBuilder.Output.IsTargetSkipped ("UpdateAndroidResources", defaultIfNotUsed: false), $"`UpdateAndroidResources` should *not* be skipped in the deferred build!");

				// Run the real build now
				Assert.IsTrue (appBuilder.Build(app, parameters: new string[]{
					"DesignTimeBuild=false",
					"BuildingInsideVisualStudio=true",
					"DeferredBuildSupported=true",
				}), "real build should have succeeded.");
			}
		}

		[TestMethod]
		[CombinatorialData]
		public void RunUpdateAndroidResourcesIfBackgroundBuildNotSupported ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};

			app.SetRuntime (runtime);
			app.SetProperty ("AndroidUseIntermediateDesignerFile", "True");

			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.IsTrue (appBuilder.DesignTimeBuild (app, parameters: new string[]{
					"BuildingInsideVisualStudio=true",
					// The DTB passes down these two properties
					"SkipCompilerExecution=true",
					"ProvideCommandLineArgs=true",
				}), "first app build should have succeeded.");

				Assert.IsFalse (appBuilder.Output.IsTargetSkipped ("UpdateAndroidResources"), $"`UpdateAndroidResources` should be not skipped for DTB when deferred build is not supported!");
			}
		}
	}
}
