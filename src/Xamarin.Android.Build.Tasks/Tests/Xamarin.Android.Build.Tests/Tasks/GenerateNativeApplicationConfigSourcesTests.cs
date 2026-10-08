#nullable enable
using System.IO;
using System.Text;

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
	public void CoreClrConfigurationUsesBinaryBootstrapInsteadOfApplicationGlobals (bool haveAssemblyStore)
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		string rawPath = Path.Combine (outputRoot, "android", "coreclr-bootstrap.bin");
		string configPath = Path.Combine (outputRoot, "app.runtimeconfig.json");
		Directory.CreateDirectory (outputRoot);
		File.WriteAllText (configPath, """{"runtimeOptions":{"configProperties":{"Test.Feature":"enabled"}}}""");
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (Path.Combine (outputRoot, "Example.dll"))],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			CoreClrBootstrapOutputFile = rawPath,
			SupportedAbis = ["arm64-v8a", "x86"],
			AndroidPackageName = "com.example.bootstrap",
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
			ProjectRuntimeConfigFilePath = configPath,
		};
		Assert.IsTrue (task.Execute ());
		var config = EnvironmentHelper.ReadCoreClrBootstrap (outputRoot).Config;
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
		byte [] raw = File.ReadAllBytes (rawPath);
		StringAssert.Contains ("com.example.bootstrap", Encoding.UTF8.GetString (raw));
		StringAssert.Contains ("Test.Feature", Encoding.UTF8.GetString (raw));
		string arm64 = File.ReadAllText (Path.Combine (task.EnvironmentOutputDirectory, "environment.arm64-v8a.ll"));
		string x86 = File.ReadAllText (Path.Combine (task.EnvironmentOutputDirectory, "environment.x86.ll"));
		Assert.That (arm64, Does.Not.Contain ("com.example.bootstrap"));
		Assert.That (arm64, Does.Not.Contain ("Test.Feature"));
		Assert.That (x86, Does.Not.Contain ("com.example.bootstrap"));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ApplicationConfigDoesNotReadAssemblyMetadata (bool haveAssemblyStore)
	{
		string outputRoot = Path.Combine (Root, "temp", $"{nameof (ApplicationConfigDoesNotReadAssemblyMetadata)}-{haveAssemblyStore}");
		string monoAndroidPath = Path.Combine (outputRoot, "missing", "Mono.Android.dll");
		FileAssert.DoesNotExist (monoAndroidPath);

		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (monoAndroidPath)],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			CoreClrBootstrapOutputFile = Path.Combine (outputRoot, "android", "coreclr-bootstrap.bin"),
			SupportedAbis = ["arm64-v8a", "armeabi-v7a", "x86_64", "x86"],
			AndroidPackageName = "com.microsoft.android.configtest",
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
		};

		Assert.IsTrue (task.Execute (), "Application config generation should only need assembly names, not metadata.");

		var config = EnvironmentHelper.ReadCoreClrBootstrap (outputRoot).Config;
		Assert.AreEqual (1u, config.number_of_assemblies_in_apk);
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
		Assert.AreEqual (task.AndroidPackageName, config.android_package_name);
		if (!haveAssemblyStore) {
			Assert.AreEqual (29u, config.bundled_assembly_name_width);
		}
	}

	[Test]
	public void TypeMapAssemblyIsCountedOnceAcrossResolvedAssemblyLists ()
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		string monoAndroidPath = Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, "Mono.Android.dll");
		FileAssert.Exists (monoAndroidPath);
		string typeMapPath = Path.Combine (outputRoot, "typemap", "_Test.TypeMap.dll");
		var typeMap = new TaskItem (typeMapPath);
		typeMap.SetMetadata ("Abi", "arm64-v8a");
		typeMap.SetMetadata ("DestinationSubDirectory", "arm64-v8a/");
		typeMap.SetMetadata ("RelativePath", "_Test.TypeMap.dll");

		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (monoAndroidPath), typeMap],
			AdditionalResolvedAssemblies = [new TaskItem (typeMapPath)],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			CoreClrBootstrapOutputFile = Path.Combine (outputRoot, "android", "coreclr-bootstrap.bin"),
			SupportedAbis = ["arm64-v8a"],
			AndroidPackageName = "com.microsoft.android.typemapcounttest",
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = true,
		};

		Assert.IsTrue (task.Execute (), "GenerateNativeApplicationConfigSources should succeed.");
		var config = EnvironmentHelper.ReadCoreClrBootstrap (outputRoot).Config;
		Assert.AreEqual (2u, config.number_of_assemblies_in_apk, "The type map must not be counted again as a satellite assembly.");
	}
}
