using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[DoNotParallelize]
public class GenerateCoreClrRuntimeProviderSourcesTests : BaseTest
{
	[TestMethod]
	public void GeneratesAdditionalProcessProvider ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (Console.Out),
			AdditionalProviderSources = ["MonoRuntimeProvider_1"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute (), "The additional provider should be generated.");
		var provider = Path.Combine (outputDirectory, "src", "mono", "MonoRuntimeProvider_1.java");
		Assert.IsTrue (File.Exists (provider));
		Assert.Contains ("class MonoRuntimeProvider_1", File.ReadAllText (provider));
	}

	[TestMethod]
	public void RemovesObsoleteNumberedProviders ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (Console.Out),
			AdditionalProviderSources = ["MonoRuntimeProvider_1", "MonoRuntimeProvider_2"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute ());
		var sourceDirectory = Path.Combine (outputDirectory, "src", "mono");
		var first = Path.Combine (sourceDirectory, "MonoRuntimeProvider_1.java");
		var second = Path.Combine (sourceDirectory, "MonoRuntimeProvider_2.java");
		var unrelated = Path.Combine (sourceDirectory, "MonoRuntimeProvider_custom.java");
		var similarName = Path.Combine (sourceDirectory, "MonoRuntimeProvider_1backup.java");
		File.WriteAllText (unrelated, "unrelated");
		File.WriteAllText (similarName, "unrelated");

		task.AdditionalProviderSources = ["MonoRuntimeProvider_1"];
		Assert.IsTrue (task.Execute ());
		Assert.IsTrue (File.Exists (first));
		Assert.IsFalse (File.Exists (second));
		Assert.IsTrue (File.Exists (unrelated));
		Assert.IsTrue (File.Exists (similarName));
		CollectionAssert.AreEquivalent (new [] { second }, task.DeletedSources);

		task.AdditionalProviderSources = [];
		Assert.IsTrue (task.Execute ());
		Assert.IsFalse (File.Exists (first));
		CollectionAssert.AreEquivalent (new [] { first }, task.DeletedSources);
	}
}
