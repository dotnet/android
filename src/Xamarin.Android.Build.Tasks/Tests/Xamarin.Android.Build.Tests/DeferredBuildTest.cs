using NUnit.Framework;
using System.IO;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Build.Tests
{
	[Parallelizable (ParallelScope.Children)]
	public class DeferredBuildTest : BaseTest
	{
		[Test]
		public void SelectivelyRunUpdateAndroidResources ()
		{
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
			};

			app.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
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

				Assert.IsTrue (appBuilder.Output.IsTargetSkipped ("UpdateAndroidResources"), $"`UpdateAndroidResources` should be skipped for DTB when deferred build is supported!");

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

				Assert.IsFalse (appBuilder.Output.IsTargetSkipped ("UpdateAndroidResources"), $"`UpdateAndroidResources` should *not* be skipped in the deferred build!");

				// Run the real build now
				Assert.IsTrue (appBuilder.Build(app, parameters: new string[]{
					"DesignTimeBuild=false",
					"BuildingInsideVisualStudio=true",
					"DeferredBuildSupported=true",
				}), "real build should have succeeded.");
			}
		}

		[Test]
		public void RunUpdateAndroidResourcesIfBackgroundBuildNotSupported ()
		{
			var path = Path.Combine ("temp", TestName);
			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
			};

			app.SetProperty ("AndroidUseManagedDesignTimeResourceGenerator", "True");
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
