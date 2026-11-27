using System.IO;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateCoreClrRuntimeProviderSourcesTests : BaseTest
{
	[Test]
	public void GeneratesAdditionalProcessProvider ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AdditionalProviderSources = ["MonoRuntimeProvider_1"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute (), "The additional provider should be generated.");
		var provider = Path.Combine (outputDirectory, "src", "mono", "MonoRuntimeProvider_1.java");
		FileAssert.Exists (provider);
		StringAssert.Contains ("class MonoRuntimeProvider_1", File.ReadAllText (provider));
	}

	[Test]
	public void RemovesObsoleteNumberedProviders ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
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
		var firstTimestamp = File.GetLastWriteTimeUtc (first);

		task.AdditionalProviderSources = ["MonoRuntimeProvider_1"];
		Assert.IsTrue (task.Execute ());
		FileAssert.Exists (first);
		FileAssert.DoesNotExist (second);
		FileAssert.Exists (unrelated);
		FileAssert.Exists (similarName);
		CollectionAssert.AreEquivalent (new [] { second }, task.DeletedSources);
		Assert.AreEqual (firstTimestamp, File.GetLastWriteTimeUtc (first));

		task.AdditionalProviderSources = [];
		Assert.IsTrue (task.Execute ());
		FileAssert.DoesNotExist (first);
		FileAssert.Exists (unrelated);
		FileAssert.Exists (similarName);
		CollectionAssert.AreEquivalent (new [] { first }, task.DeletedSources);
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.DeletedSources);
	}
}
