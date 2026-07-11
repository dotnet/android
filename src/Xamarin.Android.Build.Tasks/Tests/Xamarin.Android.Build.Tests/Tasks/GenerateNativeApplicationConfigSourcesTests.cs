#nullable enable
using System.IO;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateNativeApplicationConfigSourcesTests : BaseTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void HaveAssemblyStoreIsEmittedForCoreCLR (bool haveAssemblyStore)
	{
		string outputRoot = Path.Combine (Root, "temp", $"{nameof (HaveAssemblyStoreIsEmittedForCoreCLR)}-{haveAssemblyStore}");
		string monoAndroidPath = Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, "Mono.Android.dll");
		FileAssert.Exists (monoAndroidPath);

		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (monoAndroidPath)],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			SupportedAbis = ["arm64-v8a"],
			AndroidPackageName = "com.microsoft.android.assemblystoretest",
			EnablePreloadAssembliesDefault = false,
			TargetsCLR = true,
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
		};

		Assert.IsTrue (task.Execute (), "GenerateNativeApplicationConfigSources should succeed.");

		var environmentFiles = EnvironmentHelper.GatherEnvironmentFiles (
			outputRoot,
			"arm64-v8a",
			required: true,
			runtime: AndroidRuntime.CoreCLR
		);
		var config = (EnvironmentHelper.ApplicationConfig_CoreCLR)EnvironmentHelper.ReadApplicationConfig (environmentFiles, AndroidRuntime.CoreCLR);
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
	}
}
