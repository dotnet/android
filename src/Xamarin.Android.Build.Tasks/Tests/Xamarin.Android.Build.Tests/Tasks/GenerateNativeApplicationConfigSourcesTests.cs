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
	[Test]
	public void EmitsApplicationConfigForAllAbis ()
	{
		string outputRoot = Path.Combine (Root, "temp", nameof (EmitsApplicationConfigForAllAbis));
		string monoAndroidPath = Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory, "Mono.Android.dll");
		FileAssert.Exists (monoAndroidPath);

		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (monoAndroidPath)],
			EnvironmentOutputDirectory = Path.Combine (outputRoot, "android"),
			SupportedAbis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"],
			AndroidPackageName = "com.microsoft.android.llvmassemblytest",
			EnablePreloadAssembliesDefault = false,
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = true,
		};

		Assert.IsTrue (task.Execute (), "GenerateNativeApplicationConfigSources should succeed.");

		foreach (var (abi, triple) in new [] {
			("armeabi-v7a", "armv7-unknown-linux-android21"),
			("arm64-v8a", "aarch64-unknown-linux-android21"),
			("x86", "i686-unknown-linux-android21"),
			("x86_64", "x86_64-unknown-linux-android21"),
		}) {
			string fileName = $"environment.{abi}.ll";
			string source = File.ReadAllText (Path.Combine (outputRoot, "android", fileName));
			Assert.That (source, Does.Contain ($"source_filename = \"{fileName}\""), abi);
			Assert.That (source, Does.Contain ($"target triple = \"{triple}\""), abi);
			Assert.That (source, Does.Contain ("%struct.ApplicationConfig = type"), abi);
			Assert.That (source, Does.Contain ("@application_config = "), abi);
			Assert.That (source, Does.Contain ("@app_environment_variables = "), abi);
			Assert.That (source, Does.Contain ("@app_system_properties = "), abi);
			Assert.That (source, Does.Contain ("@assembly_store = "), abi);
			Assert.That (source, Does.Contain ("@dso_cache = "), abi);
			Assert.That (source, Does.Contain ("!llvm.module.flags = "), abi);
			Assert.That (source, Does.Contain ("i1, ; bool uses_assembly_preload"), abi);
			Assert.That (source, Does.Contain ("; Application environment variables"), abi);
		}
	}

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
		var config = (EnvironmentHelper.ApplicationConfig)EnvironmentHelper.ReadApplicationConfig (environmentFiles, AndroidRuntime.CoreCLR);
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);

		string source = File.ReadAllText (Path.Combine (outputRoot, "android", "environment.arm64-v8a.ll"));
		Assert.That (source, Does.Not.Contain ("jni_add_native_method_registration_attribute_present"));
		Assert.That (source, Does.Not.Contain ("jnienv_registerjninatives_method_token"));
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
			SupportedAbis = ["arm64-v8a"],
			AndroidPackageName = "com.microsoft.android.typemapcounttest",
			EnablePreloadAssembliesDefault = false,
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = true,
		};

		Assert.IsTrue (task.Execute (), "GenerateNativeApplicationConfigSources should succeed.");
		var environmentFiles = EnvironmentHelper.GatherEnvironmentFiles (
			outputRoot, "arm64-v8a", required: true, runtime: AndroidRuntime.CoreCLR);
		var config = (EnvironmentHelper.ApplicationConfig)EnvironmentHelper.ReadApplicationConfig (environmentFiles, AndroidRuntime.CoreCLR);
		Assert.AreEqual (2u, config.number_of_assemblies_in_apk, "The type map must not be counted again as a satellite assembly.");
	}
}
