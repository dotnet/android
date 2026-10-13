#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateNativeApplicationConfigSourcesTests : BaseTest
{
	[Test]
	public void InvalidBundleConfigUsesCodedWarning ()
	{
		var warnings = new List<BuildWarningEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, warnings: warnings);
		var log = new TaskLoggingHelper (engine, nameof (BundleConfigSplitConfigsChecker));
		string configFile = Path.Combine (Root, "missing-bundle-config.json");

		Assert.IsTrue (BundleConfigSplitConfigsChecker.ShouldIgnoreSplitConfigs (log, configFile));
		var warning = warnings.Single (warning => warning.Code == "XA4331");
		Assert.AreEqual ($"Failed to process bundle config file '{configFile}', split config files will be ignored at run time.", warning.Message);
	}

	[TestCase (null)]
	[TestCase (" ")]
	public void CoreClrRequiresBootstrapOutputFile (string? bootstrapPath)
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			ResolvedAssemblies = [],
			CoreClrBootstrapOutputFile = bootstrapPath,
			SupportedAbis = ["arm64-v8a"],
			AndroidPackageName = "com.example.bootstrap",
			AndroidRuntime = "CoreCLR",
		};

		Assert.IsFalse (task.Execute ());
		Assert.That (errors, Has.Count.EqualTo (1));
		StringAssert.Contains ("CoreCLR bootstrap output path is required.", errors [0].Message);
		FileAssert.DoesNotExist (Path.Combine (outputRoot, "environment.arm64-v8a.ll"));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void CoreClrConfigurationUsesBinaryBootstrapInsteadOfApplicationGlobals (bool haveAssemblyStore)
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		string rawPath = Path.Combine (outputRoot, "android", "coreclr-bootstrap.bin");
		string configPath = Path.Combine (outputRoot, "app.runtimeconfig.json");
		string environmentPath = Path.Combine (outputRoot, "environment.txt");
		Directory.CreateDirectory (outputRoot);
		File.WriteAllText (configPath, """{"runtimeOptions":{"configProperties":{"Test.Feature":"enabled"}}}""");
		File.WriteAllText (environmentPath, "BOOTSTRAP_VALUE=\u00e9\u65e5\nBOOTSTRAP_EMPTY=\n");
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (Path.Combine (outputRoot, "Example.dll"))],
			CoreClrBootstrapOutputFile = rawPath,
			SupportedAbis = ["arm64-v8a", "x86"],
			AndroidPackageName = "com.example.bootstrap",
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
			ProjectRuntimeConfigFilePath = configPath,
			Environments = [new TaskItem (environmentPath)],
		};
		Assert.IsTrue (task.Execute ());
		var bootstrap = EnvironmentHelper.ReadCoreClrBootstrap (outputRoot);
		var config = bootstrap.Config;
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
		Assert.AreEqual ("\u00e9\u65e5", bootstrap.EnvironmentVariables ["BOOTSTRAP_VALUE"]);
		Assert.AreEqual ("", bootstrap.EnvironmentVariables ["BOOTSTRAP_EMPTY"]);
		Assert.AreEqual (bootstrap.EnvironmentVariables.Count, config.environment_variable_count);
		byte [] raw = File.ReadAllBytes (rawPath);
		StringAssert.Contains ("com.example.bootstrap", Encoding.UTF8.GetString (raw));
		StringAssert.Contains ("Test.Feature", Encoding.UTF8.GetString (raw));
		Assert.IsEmpty (Directory.GetFiles (Path.Combine (outputRoot, "android"), "*.ll"),
			"Application configuration should not generate LLVM sources for any ABI.");
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
