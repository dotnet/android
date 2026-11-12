using System;
using System.IO;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class JavacVersionTargetsTests : BaseTest
{
	[TestCase ("9", "1.8", "true", "false", "", "", "1.8", "1.8")]
	[TestCase ("9.0.4", "11", "true", "bogus", "", "", "11", "11")]
	[TestCase ("90", "1.8", "", "", "", "", "1.8", "1.8")]
	[TestCase ("9", "1.8", "", "", "17", "", "1.8", "1.8")]
	[TestCase ("9", "1.8", "", "", "", "17", "1.8", "1.8")]
	[TestCase ("9", "1.8", "", "", "11", "17", "11", "17")]
	[TestCase ("1.8", "11", "", "true", "", "", "1.7", "1.7")]
	[TestCase ("1.8.0_292", "11", "false", "true", "", "", "1.7", "1.7")]
	[TestCase ("1.80", "11", "off", "yes", "", "", "1.7", "1.7")]
	[TestCase ("1.8", "11", "!true", "!false", "", "", "1.7", "1.7")]
	[TestCase ("1.8", "11", "FALSE", "TRUE", "17", "", "1.7", "1.7")]
	[TestCase ("1.8", "11", "no", "on", "", "17", "1.7", "1.7")]
	[TestCase ("1.8", "11", "", "true", "11", "17", "11", "17")]
	[TestCase ("1.8", "11", "", "", "", "", "", "")]
	[TestCase ("1.8", "11", "", "false", "17", "", "17", "")]
	[TestCase ("1.8", "11", "", "off", "", "17", "", "17")]
	[TestCase ("1.8", "11", "true", "true", "", "", "", "")]
	[TestCase ("1.8", "11", "on", "bogus", "", "", "", "")]
	[TestCase ("1.7", "11", "", "true", "", "", "", "")]
	[TestCase ("17.0.1", "11", "", "true", "", "", "", "")]
	[TestCase ("21.0.5", "11", "", "true", "17", "", "17", "")]
	[TestCase (" 9", "11", "", "", "", "", "", "")]
	// Missing required task inputs intentionally no longer produce MSB4044.
	[TestCase ("", "11", "", "true", "17", "", "17", "")]
	[TestCase ("1.8", "", "", "true", "", "", "1.7", "1.7")]
	[TestCase ("9", "", "true", "", "17", "", "", "")]
	// XML conditions report MSB4100 instead of task conversion error MSB4030.
	[TestCase ("1.8", "11", "bogus", "false", "", "", "", "", "AndroidSkipJavacVersionCheck")]
	[TestCase ("17", "11", "bogus", "", "", "", "", "", "AndroidSkipJavacVersionCheck")]
	[TestCase ("9", "11", "bogus", "", "", "", "", "", "AndroidSkipJavacVersionCheck")]
	[TestCase ("1.8", "11", "", "bogus", "", "", "", "", "AndroidEnableMultiDex")]
	[TestCase ("17", "11", "false", "bogus", "", "", "", "", "AndroidEnableMultiDex")]
	// Both preset versions skip the target, including malformed booleans.
	[TestCase ("9", "", "bogus", "bogus", "11", "17", "11", "17")]
	public void AdjustJavacVersionArguments (string jdk, string defaultJdk, string skipCheck, string multiDex,
		string source, string target, string expectedSource, string expectedTarget, string? invalidProperty = null)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src",
			"Xamarin.Android.Build.Tasks", "MSBuild", "Xamarin", "Android", "Xamarin.Android.Javac.targets");
		string project = Path.Combine (directory, "javac.proj");
		string output = Path.Combine (directory, "versions.txt");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (Microsoft.Android.Tasks.CreateJavaArchive).Assembly.Location),
				new XElement ("_JdkVersion", jdk),
				new XElement ("_DefaultJdkVersion", defaultJdk),
				new XElement ("AndroidSkipJavacVersionCheck", skipCheck),
				new XElement ("AndroidEnableMultiDex", multiDex),
				new XElement ("JavacSourceVersion", source),
				new XElement ("JavacTargetVersion", target)),
			new XElement ("Import", new XAttribute ("Project", targets)),
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_AdjustJavacVersionArguments"),
				new XElement ("WriteLinesToFile",
					new XAttribute ("File", output),
					new XAttribute ("Lines", "Source=$(JavacSourceVersion);Target=$(JavacTargetVersion)"),
					new XAttribute ("Overwrite", "true")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
		string log = result.Output + result.Error;
		if (invalidProperty != null) {
			Assert.AreNotEqual (0, result.ExitCode, log);
			StringAssert.Contains ("error MSB4100:", log);
			StringAssert.Contains ("$(" + invalidProperty + ")", log);
			FileAssert.DoesNotExist (output, "Invalid booleans must stop the build before recording versions.");
		} else {
			Assert.AreEqual (0, result.ExitCode, log);
			CollectionAssert.AreEqual (new [] { "Source=" + expectedSource, "Target=" + expectedTarget }, File.ReadAllLines (output));
		}
	}
}
