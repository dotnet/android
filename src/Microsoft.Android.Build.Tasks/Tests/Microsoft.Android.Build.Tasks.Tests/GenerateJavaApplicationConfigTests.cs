using System.IO;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJavaApplicationConfigTests : BaseTest
{
	[Test]
	public void NativeAotUsesTheSharedJavaBootstrapShape ()
	{
		string outputRoot = Path.Combine (Root, "temp", nameof (NativeAotUsesTheSharedJavaBootstrapShape));
		Directory.CreateDirectory (outputRoot);
		string environmentFile = Path.Combine (outputRoot, "environment.txt");
		File.WriteAllLines (environmentFile, [
			"LANG=fr-FR",
			"debug.dotnet.log=gc",
		]);

		GenerateJavaApplicationConfig.WriteNativeAotSource (outputRoot, [new TaskItem (environmentFile)]);

		string sourceFile = Path.Combine (outputRoot, "src", "net", "dot", "android", "AppBootstrapConfig.java");
		string source = File.ReadAllText (sourceFile);
		StringAssert.Contains ("package net.dot.android;", source);
		StringAssert.Contains ("\"LANG\",", source);
		StringAssert.Contains ("\"fr-FR\",", source);
		StringAssert.Contains ("\"debug.dotnet.log\",", source);
		StringAssert.Contains ("\"gc\",", source);
		StringAssert.Contains ("public static void applyEnvironment (Context context)", source);
		StringAssert.Contains ("public static final String[] RuntimeProperties = new String[] {", source);
		Assert.IsFalse (File.Exists (Path.Combine (outputRoot, "src", "net", "dot", "jni", "nativeaot", "NativeAotEnvironmentVars.java")));
	}

	[Test]
	public void GeneratesJavaBootstrapDataWithoutNativeSources ()
	{
		string outputRoot = Path.Combine (Root, "temp", nameof (GeneratesJavaBootstrapDataWithoutNativeSources));
		Directory.CreateDirectory (outputRoot);

		string environmentFile = Path.Combine (outputRoot, "environment.txt");
		string runtimeConfigFile = Path.Combine (outputRoot, "app.runtimeconfig.json");
		string devConfigFile = Path.Combine (outputRoot, "app.runtimeconfig.dev.json");
		string javaFile = Path.Combine (outputRoot, "net", "dot", "android", "AppBootstrapConfig.java");

		File.WriteAllLines (environmentFile, [
			"MY_ENV=one\\two",
			"debug.dotnet.log=gref",
		]);
		File.WriteAllText (runtimeConfigFile, """
			{"runtimeOptions":{"configProperties":{"Test.Property":"base","HOST_RUNTIME_CONTRACT":"ignored","PINVOKE_OVERRIDE":"ignored"}}}
			""");
		File.WriteAllText (devConfigFile, """
			{"runtimeOptions":{"configProperties":{"Test.Property":"dev"}}}
			""");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputFile = javaFile,
			AndroidPackageName = "example.test",
			UseAssemblyStore = true,
			Environments = [new TaskItem (environmentFile)],
			ProjectRuntimeConfigFilePath = runtimeConfigFile,
			ProjectRuntimeConfigDevFilePath = devConfigFile,
		};

		Assert.IsTrue (task.Execute ());
		string java = File.ReadAllText (javaFile);
		StringAssert.Contains ("PackageName = \"example.test\"", java);
		StringAssert.Contains ("HaveAssemblyStore = true", java);
		StringAssert.Contains ("\"MY_ENV\"", java);
		StringAssert.Contains ("\"one\\\\two\"", java);
		StringAssert.Contains ("\"debug.dotnet.log\"", java);
		StringAssert.Contains ("\"Test.Property\"", java);
		StringAssert.Contains ("\"dev\"", java);
		StringAssert.DoesNotContain ("HOST_RUNTIME_CONTRACT", java);
		StringAssert.DoesNotContain ("PINVOKE_OVERRIDE", java);
		StringAssert.Contains ("public static byte[] readRemappingAsset (String runtimeIdentifier)", java);
		StringAssert.DoesNotContain ("RemapTypes", java);
		StringAssert.DoesNotContain ("RemapMethods", java);
		StringAssert.Contains ("public static void applyEnvironment (Context context)", java);
		StringAssert.Contains ("public static void preloadJniLibraries ()", java);
		Assert.IsEmpty (Directory.GetFiles (outputRoot, "*.ll", SearchOption.AllDirectories));

		var packageManager = new GeneratePackageManagerJava {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			MainAssembly = "linked/MyApp.dll",
			OutputDirectory = Path.Combine (outputRoot, "mono"),
		};
		Assert.IsTrue (packageManager.Execute ());
		StringAssert.DoesNotContain ("UseJavaApplicationConfig",
			File.ReadAllText (Path.Combine (outputRoot, "mono", "MonoPackageManager_Resources.java")));
		StringAssert.Contains ("AppBootstrapConfig.applyEnvironment (context)",
			File.ReadAllText (Path.Combine (outputRoot, "mono", "MonoPackageManager_Resources.java")));
	}
}
