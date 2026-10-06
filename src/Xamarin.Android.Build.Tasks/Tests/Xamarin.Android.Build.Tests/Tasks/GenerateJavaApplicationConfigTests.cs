#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
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
		var config = JavaAppConfigTestHelper.Read (source);
		Assert.That (config.Layout.Take (4), Is.EqualTo (new [] { 1, 1, 1, 1 }));
		Assert.That (config.Strings, Is.EqualTo (new [] {
			"example.test", "MY_ENV", "value\\\"quoted", "custom.setting", "some value",
			"Example.Switch", "enabled", "libSome.Library.dll.so",
		}));
		Assert.That (source, Does.Contain ($"HaveAssemblyStore = {haveStore.ToString ().ToLowerInvariant ()}"));
		Assert.That (source, Does.Not.Contain ("String[] Environment"));
		Assert.That (source, Does.Not.Contain ("String[] SystemProperties"));
		Assert.That (source, Does.Not.Contain ("String[] RuntimeProperties"));
		Assert.That (source, Does.Contain ("// \"example.test\""));
		Assert.That (source, Does.Contain ("// \"MY_ENV\""));
		Assert.That (source, Does.Contain ("// \"value\\\\\\\"quoted\""));
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
		Assert.That (JavaAppConfigTestHelper.Read (source).Strings,
			Is.EqualTo (new [] { "example.test", "Z_ENV", "first", "A_ENV", "second" }));
	}

	[Test]
	public void WritesStandardUtf8IncludingSupplementaryCharactersAndEmptyValues ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string value = "quoted\\\"\t\u00e9\u4e2d\ud83d\ude80";
		string environmentFile = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environmentFile, $"MY_ENV={value}\nEMPTY_ENV=\n");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			Environments = [new TaskItem (environmentFile)],
		};

		Assert.IsTrue (task.Execute ());
		var config = JavaAppConfigTestHelper.Read (File.ReadAllText (task.OutputFile));
		Assert.That (config.Strings, Is.EqualTo (new [] { "example.test", "MY_ENV", value, "EMPTY_ENV", "" }));
		Assert.That (config.Data, Does.Contain ((byte)0xf0), "Supplementary characters must use standard UTF-8, not JNI modified UTF-8.");
		Assert.That (File.ReadAllText (task.OutputFile), Does.Not.Contain ("System.arraycopy"));
	}

	[Test]
	public void WritesEmptyConfiguration ()
	{
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (Root, "temp", TestName, "AppBootstrapConfig.java"),
		};

		Assert.IsTrue (task.Execute ());
		var config = JavaAppConfigTestHelper.Read (File.ReadAllText (task.OutputFile));
		Assert.That (config.Layout, Is.EqualTo (new [] { 0, 0, 0, 0, 0 }));
		Assert.That (config.Strings, Is.EqualTo (new [] { "example.test" }));
	}

	[Test]
	public void SplitsLargeBlobIntoBoundedJavaInitializers ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string value = new string ('x', 20000) + "\ud83d\ude80";
		string environmentFile = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environmentFile, $"MY_ENV={value}\n");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			Environments = [new TaskItem (environmentFile)],
		};

		Assert.IsTrue (task.Execute ());
		string source = File.ReadAllText (task.OutputFile);
		Assert.That (JavaAppConfigTestHelper.Read (source).Strings, Is.EqualTo (new [] { "example.test", "MY_ENV", value }));
		Assert.That (source, Does.Contain ("nativeConfigChunk4 ()"));
		Assert.That (source, Does.Contain ("System.arraycopy"));
		Assert.That (source, Does.Contain ("// Continuation of the preceding UTF-8 string."));
		Assert.That (source, Does.Not.Contain ("getBytes"));
		Assert.That (source, Does.Not.Contain ("Base64"));
	}

	[TestCase (4096)]
	[TestCase (4097)]
	public void OnlyConcatenatesBlobsAboveInitializerLimit (int size)
	{
		string directory = Path.Combine (Root, "temp", $"{TestName}-{size}");
		Directory.CreateDirectory (directory);
		string value = new string ('x', size - Encoding.UTF8.GetByteCount ("example.test\0MY_ENV\0\0"));
		string environmentFile = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environmentFile, $"MY_ENV={value}\n");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			Environments = [new TaskItem (environmentFile)],
		};

		Assert.IsTrue (task.Execute ());
		string source = File.ReadAllText (task.OutputFile);
		var config = JavaAppConfigTestHelper.Read (source);
		Assert.That (config.Data.Length, Is.EqualTo (size));
		Assert.That (config.Strings, Is.EqualTo (new [] { "example.test", "MY_ENV", value }));
		Assert.That (source.Contains ("System.arraycopy"), Is.EqualTo (size > 4096));
		Assert.That (source.Contains ("nativeConfigChunk"), Is.EqualTo (size > 4096));
	}

	[Test]
	public void EscapesStringCommentsWithoutChangingBlobBytes ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string runtimeConfig = Path.Combine (directory, "app.runtimeconfig.json");
		File.WriteAllText (runtimeConfig, """{"runtimeOptions":{"configProperties":{"Example":"line\n//\"quoted\"\\u000a};"}}}""");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			ProjectRuntimeConfigFilePath = runtimeConfig,
		};

		Assert.IsTrue (task.Execute ());
		string source = File.ReadAllText (task.OutputFile);
		Assert.That (source, Does.Contain ("// \"line\\n//\\\"quoted\\\"\\\\u000a};\""));
		Assert.That (JavaAppConfigTestHelper.Read (source).Strings, Is.EqualTo (new [] { "example.test", "Example", "line\n//\"quoted\"\\u000a};" }));
	}

	[Test]
	public void RejectsNulInRuntimePropertyBeforeWritingJava ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string runtimeConfig = Path.Combine (directory, "app.runtimeconfig.json");
		File.WriteAllText (runtimeConfig, """{"runtimeOptions":{"configProperties":{"Example":"before\u0000after"}}}""");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			ProjectRuntimeConfigFilePath = runtimeConfig,
		};

		Assert.IsFalse (task.Execute ());
		FileAssert.DoesNotExist (task.OutputFile);
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
