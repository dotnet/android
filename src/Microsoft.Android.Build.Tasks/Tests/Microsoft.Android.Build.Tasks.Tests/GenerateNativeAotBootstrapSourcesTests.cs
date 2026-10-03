using System.IO;
using System.Linq;

using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateNativeAotBootstrapSourcesTests : BaseTest
{
	[Test]
	public void GeneratesEnvironmentAndSystemPropertiesWithOverrides ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		string libraryFile = Path.Combine (path, "library.txt");
		File.WriteAllText (environmentFile, """
			DOTNET_VALUE=application
			debug.dotnet.log=initial
			""");
		File.WriteAllText (libraryFile, """
			DOTNET_VALUE=library "C:\quoted"
			debug.dotnet.log=all
			debug.dotnet.max_grefc=1234
			""");
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = path,
			TargetName = "App",
			Environments = [new TaskItem (environmentFile), new TaskItem (libraryFile)],
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (2, task.GeneratedSources.Length);
		Assert.IsTrue (task.GeneratedSources.All (File.Exists));

		string source = File.ReadAllText (task.GeneratedSources [1]);
		StringAssert.Contains ("\"DOTNET_VALUE\"", source);
		StringAssert.Contains ("\"library \\\"C:\\\\quoted\\\"\"", source);
		StringAssert.Contains ("\"debug.dotnet.log\",\n\t\t\"all\"", source);
		StringAssert.Contains ("\"debug.dotnet.max_grefc\",\n\t\t\"1234\"", source);
		StringAssert.DoesNotContain ("System.setProperty", source);
		StringAssert.DoesNotContain ("\"initial\"", source);
	}

	[Test]
	public void EmptyEnvironmentStillGeneratesBootstrapSources ()
	{
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = Path.Combine (Root, "temp", TestName),
			TargetName = "App",
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (2, task.GeneratedSources.Length);
		string source = File.ReadAllText (task.GeneratedSources [1]);
		StringAssert.Contains ("static final String[] systemProperties = new String[] {\n\n\t};", source.ReplaceLineEndings ("\n"));
		StringAssert.DoesNotContain ("@SYSTEM_PROPERTIES@", source);
	}
}
