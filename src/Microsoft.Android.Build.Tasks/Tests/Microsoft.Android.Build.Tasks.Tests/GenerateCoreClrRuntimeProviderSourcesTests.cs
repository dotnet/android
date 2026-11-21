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
			AdditionalProviderSources = ["CoreClrRuntimeProvider_1"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute (), "The additional provider should be generated.");
		var provider = Path.Combine (outputDirectory, "src", "net", "dot", "android", "CoreClrRuntimeProvider_1.java");
		FileAssert.Exists (provider);
		StringAssert.Contains ("class CoreClrRuntimeProvider_1", File.ReadAllText (provider));
	}
}
