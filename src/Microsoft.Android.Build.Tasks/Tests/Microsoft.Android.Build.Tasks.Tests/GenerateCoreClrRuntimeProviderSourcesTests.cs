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
}
