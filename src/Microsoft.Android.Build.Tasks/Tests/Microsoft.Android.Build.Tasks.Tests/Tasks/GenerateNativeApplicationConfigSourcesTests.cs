#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateNativeApplicationConfigSourcesTests : BaseTest
{
	static void WriteTestJniLibrary (string path, string libraryName)
	{
		using var payload = new MemoryStream ([0]);
		using var elf = new MemoryStream ();
		AssemblyStoreElfWriter.Write ([("JNI_OnLoad", payload)], elf, AndroidTargetArch.Arm64, libraryName);
		byte [] image = elf.ToArray ();
		// Patch Symbol 1's st_info from STB_GLOBAL | STT_OBJECT (0x11) to STB_GLOBAL | STT_FUNC (0x12).
		image [316] = 0x12;
		File.WriteAllBytes (path, image);
	}

	[TestCase ("NativeAOT")]
	[TestCase ("nativeaot")]
	[SetUICulture ("en-US")]
	public void RejectsUnsupportedRuntimesWithoutWritingBootstrap (string runtime)
	{
		string bootstrapPath = Path.Combine (Root, "temp", TestName, "coreclr-bootstrap.bin");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			AndroidRuntime = runtime,
			CoreClrBootstrapOutputFile = bootstrapPath,
		};
		Assert.IsFalse (task.Execute ());
		Assert.That (errors, Has.Count.EqualTo (1));
		Assert.AreEqual ("XAGCA7009", errors [0].Code);
		StringAssert.Contains (
			$"GenerateNativeApplicationConfigSources only supports CoreCLR bootstrap configuration. The supplied AndroidRuntime value was '{runtime}'.",
			errors [0].Message);
		FileAssert.DoesNotExist (bootstrapPath);
	}

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

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, false)]
	[TestCase (true, true)]
	public void CoreClrConfigurationUsesBinaryBootstrapInsteadOfApplicationGlobals (bool haveAssemblyStore, bool ignoreSplitConfigs)
	{
		string outputRoot = Path.Combine (Root, "temp", TestName);
		string rawPath = Path.Combine (outputRoot, "android", "coreclr-bootstrap.bin");
		string configPath = Path.Combine (outputRoot, "app.runtimeconfig.json");
		string devConfigPath = Path.Combine (outputRoot, "app.runtimeconfig.dev.json");
		string bundleConfigPath = Path.Combine (outputRoot, "BundleConfig.json");
		string environmentPath = Path.Combine (outputRoot, "environment.txt");
		Directory.CreateDirectory (outputRoot);
		File.WriteAllText (configPath, """{"runtimeOptions":{"configProperties":{"Test.Feature":"enabled"}}}""");
		File.WriteAllText (devConfigPath, """{"runtimeOptions":{"configProperties":{"Test.Feature":"dev-override","Test.Empty":""}}}""");
		File.WriteAllText (bundleConfigPath, $$"""
			{
			  "optimizations": {
			    "splitsConfig": {
			      "splitDimension": [{"value":"ABI","negate":{{ignoreSplitConfigs.ToString ().ToLowerInvariant ()}}}]
			    }
			  }
			}
			""");
		File.WriteAllText (environmentPath, "BOOTSTRAP_VALUE=\u00e9\u65e5\nBOOTSTRAP_EMPTY=\nDOTNET_MODIFIABLE_ASSEMBLIES=Debug\ndebug.dotnet.log=gref\n");
		var libraries = new List<ITaskItem> ();
		foreach (string name in new [] { "plain.so", "libprefixed.so", "libmanaged.dll.so" }) {
			string libraryPath = Path.Combine (outputRoot, name);
			WriteTestJniLibrary (libraryPath, name);
			libraries.Add (new TaskItem (libraryPath));
		}
		var task = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [new TaskItem (Path.Combine (outputRoot, "Example.dll"))],
			CoreClrBootstrapOutputFile = rawPath,
			SupportedAbis = ["arm64-v8a", "x86"],
			AndroidPackageName = "com.example.bootstrap",
			AndroidRuntime = "CoreCLR",
			UseAssemblyStore = haveAssemblyStore,
			ProjectRuntimeConfigFilePath = configPath,
			ProjectRuntimeConfigDevFilePath = devConfigPath,
			CustomBundleConfigFile = bundleConfigPath,
			NativeLibraries = libraries.ToArray (),
			NativeLibrariesNoJniPreload = ignoreSplitConfigs ? [new TaskItem ("libprefixed.so"), new TaskItem ("libmanaged.dll.so")] : [],
			NativeLibrariesAlwaysJniPreload = [new TaskItem ("libprefixed.so")],
			Environments = [new TaskItem (environmentPath)],
		};
		Assert.IsTrue (task.Execute ());
		var bootstrap = EnvironmentHelper.ReadCoreClrBootstrap (outputRoot);
		var config = bootstrap.Config;
		Assert.AreEqual (haveAssemblyStore, config.have_assembly_store);
		Assert.AreEqual (ignoreSplitConfigs, config.ignore_split_configs);
		Assert.AreEqual (12, config.number_of_dso_cache_entries, "The 2/4/6-alias libraries must retain all cache entries.");
		Assert.AreEqual (ignoreSplitConfigs ? 4 : 6, bootstrap.Preloads.IndexStride);
		Assert.AreEqual (ignoreSplitConfigs ? 8 : 18, bootstrap.Preloads.Entries.Count);
		Assert.AreEqual ("\u00e9\u65e5", bootstrap.EnvironmentVariables ["BOOTSTRAP_VALUE"]);
		Assert.AreEqual ("", bootstrap.EnvironmentVariables ["BOOTSTRAP_EMPTY"]);
		Assert.AreEqual (bootstrap.EnvironmentVariables.Count, config.environment_variable_count);
		byte [] raw = File.ReadAllBytes (rawPath);
		StringAssert.Contains ("com.example.bootstrap", Encoding.UTF8.GetString (raw));
		StringAssert.Contains ("Test.Feature", Encoding.UTF8.GetString (raw));
		StringAssert.Contains ("dev-override", Encoding.UTF8.GetString (raw));
		StringAssert.DoesNotContain ("enabled", Encoding.UTF8.GetString (raw));
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
		string monoAndroidPath = Path.Combine (outputRoot, "Mono.Android.dll");
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

	[Test]
	public void GenerateNativeApplicationConfigUpdatesEnvironmentAndSkipsExcludedAssemblies ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);

		string envFile = Path.Combine (path, "myenv.txt");
		File.WriteAllText (envFile, "MYENV=YYYY");

		var metadata = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{ "Abi", "arm64-v8a" },
		};
		var skipped = new Dictionary<string, string> (metadata, StringComparer.OrdinalIgnoreCase) {
			{ "AndroidSkipAddToPackage", "true" },
		};
		string bootstrapPath = Path.Combine (path, "env", "coreclr-bootstrap.bin");

		var configTask = new GenerateNativeApplicationConfigSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ResolvedAssemblies = [
				new TaskItem ("linked/HelloAndroid.dll", metadata),
				new TaskItem ("linked/Excluded.dll", skipped),
			],
			CoreClrBootstrapOutputFile = bootstrapPath,
			SupportedAbis = ["x86", "arm64-v8a"],
			AndroidPackageName = "com.microsoft.net6.helloandroid",
			AndroidRuntime = "CoreCLR",
			Environments = [new TaskItem (envFile)],
		};

		Assert.IsTrue (configTask.Execute (), "GenerateNativeApplicationConfigSources task should have executed.");
		string bootstrap = Encoding.UTF8.GetString (File.ReadAllBytes (bootstrapPath));
		StringAssert.Contains ("YYYY", bootstrap, "CoreCLR bootstrap should contain the initial custom environment value.");
		StringAssert.DoesNotContain ("Excluded.dll", bootstrap, "CoreCLR bootstrap should not list assemblies excluded from packaging.");
		Assert.AreEqual (1u, EnvironmentHelper.ReadCoreClrBootstrap (path).Config.number_of_assemblies_in_apk);

		File.WriteAllText (envFile, "MYENV=XXXX");
		Assert.IsTrue (configTask.Execute (), "GenerateNativeApplicationConfigSources task should have executed. (run 2)");
		StringAssert.Contains ("XXXX", Encoding.UTF8.GetString (File.ReadAllBytes (bootstrapPath)));
	}
}
