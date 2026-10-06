#nullable enable
using System;
using System.IO;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJavaApplicationConfigTests : BaseTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void WritesCoreClrConfigurationWithoutNativeBootstrapData (bool haveStore)
	{
		string directory = Path.Combine (Root, "temp", $"{TestName}-{haveStore}");
		Directory.CreateDirectory (directory);
		string environmentFile = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environmentFile, "MY_ENV=value\\\"quoted\ncustom.setting=some value\n");
		string runtimeConfig = Path.Combine (directory, "app.runtimeconfig.json");
		File.WriteAllText (runtimeConfig, """{"runtimeOptions":{"configProperties":{"HOST_RUNTIME_CONTRACT":"untrusted","PINVOKE_OVERRIDE":"untrusted","Example.Switch":"enabled"}}}""");
		var library = new TaskItem (Path.Combine (directory, "libSome.Library.dll.so"));
		library.SetMetadata ("ArchiveFileName", "libSome.Library.dll.so");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "net", "dot", "android", "AppBootstrapConfig.java"),
			UseAssemblyStore = haveStore,
			Environments = [new TaskItem (environmentFile)],
			NativeLibraries = [library, library],
			ProjectRuntimeConfigFilePath = runtimeConfig,
		};

		Assert.IsTrue (task.Execute ());
		string source = File.ReadAllText (task.OutputFile);
		Assert.That (source, Does.Contain ("PackageName = \"example.test\""));
		Assert.That (source, Does.Contain ($"HaveAssemblyStore = {haveStore.ToString ().ToLowerInvariant ()}"));
		Assert.That (source, Does.Contain ("\"MY_ENV\","));
		Assert.That (source, Does.Contain ("\"value\\\\\\\"quoted\","));
		Assert.That (source, Does.Contain ("\"custom.setting\","));
		Assert.That (source, Does.Contain ("\"Example.Switch\","));
		Assert.That (source, Does.Not.Contain ("untrusted"));
		Assert.That (source, Does.Contain ("\"libSome.Library.dll.so\","));
		Assert.That (source, Does.Contain ("NativeLibraryFlags = new byte[] {\n\t\t0,"));
		Assert.That (source, Does.Not.Contain ("dso_cache"));
		Assert.That (source, Does.Contain ("preloadJniLibraries ()"));
		Assert.That (source, Does.Not.Contain ("readRemappingAsset"));

		DateTime before = File.GetLastWriteTimeUtc (task.OutputFile);
		Assert.IsTrue (task.Execute ());
		Assert.That (File.GetLastWriteTimeUtc (task.OutputFile), Is.EqualTo (before));
	}

	[Test]
	[TestCase (false)]
	[TestCase (true)]
	public void IncludesBundleSplitPolicy (bool ignoreSplitConfigs)
	{
		string directory = Path.Combine (Root, "temp", $"{TestName}-{ignoreSplitConfigs}");
		Directory.CreateDirectory (directory);
		string bundleConfig = Path.Combine (directory, "bundle.config");
		File.WriteAllText (bundleConfig, ignoreSplitConfigs
			? """{"optimizations":{"splitsConfig":{"splitDimension":[{"value":"ABI","negate":true}]}}}"""
			: """{"optimizations":{}}""");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			CustomBundleConfigFile = bundleConfig,
		};

		Assert.IsTrue (task.Execute ());
		Assert.That (File.ReadAllText (task.OutputFile),
			Does.Contain ($"IgnoreSplitConfigs = {ignoreSplitConfigs.ToString ().ToLowerInvariant ()};"));
	}

	[Test]
	public void PreservesEnvironmentDeclarationOrder ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string environmentFile = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environmentFile, "Z_ENV=first\nA_ENV=second\n");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			Environments = [new TaskItem (environmentFile)],
		};

		Assert.IsTrue (task.Execute ());
		string source = File.ReadAllText (task.OutputFile);
		Assert.That (source.IndexOf ("\"Z_ENV\"", StringComparison.Ordinal),
			Is.LessThan (source.IndexOf ("\"A_ENV\"", StringComparison.Ordinal)));
	}

	[Test]
	public void AlwaysPreloadOverridesNeverPreload ()
	{
		var library = new TaskItem ("libSome.Library.so");
		library.SetMetadata ("ArchiveFileName", "libSome.Library.so");
		var logger = new MockBuildEngine (TestContext.Out);
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = logger,
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (Root, "temp", TestName, "AppBootstrapConfig.java"),
			NativeLibraries = [library],
			NativeLibrariesAlwaysJniPreload = [new TaskItem ("libSome.Library.so")],
			NativeLibrariesNoJniPreload = [new TaskItem ("libSome.Library.so")],
		};
		Assert.IsTrue (task.Execute ());
		// A non-JNI library is never preloaded, even if requested as an always-preload library.
		Assert.That (File.ReadAllText (task.OutputFile), Does.Contain ("NativeLibraryFlags = new byte[] {\n\t\t0,"));
	}
}
