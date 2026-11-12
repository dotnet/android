using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class TemporaryDirectoryTargetsTests : BaseTest
{
	[TestCase ("_PrepareUpdateAndroidResgen", "ResgenTemporaryDirectory", "_UpdateAndroidResgenInputs", "_AndroidResgenFlagFile")]
	[TestCase ("_PrepareCreateBaseApk", "AaptTemporaryDirectory", "_CreateBaseApkInputs", "_PackagedResources")]
	public void TemporaryDirectoryIsOnlySetWhenTargetExecutes (string targetName, string propertyName, string inputProperty, string outputProperty)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string source = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var target = new XElement (XDocument.Load (source).Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == targetName));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		target.Attribute ("DependsOnTargets")?.Remove ();
		// Isolate directory creation from the Android tooling, retaining the production incremental contract.
		target.Elements ().Where (element => element.Name == "CreateResgenManifest" || element.Name == "GetExtraPackages").Remove ();

		string project = Path.Combine (directory, "temporary-directory.proj");
		string input = Path.Combine (directory, "input.txt");
		string stamp = Path.Combine (directory, "output.stamp");
		string output = Path.Combine (directory, "directory.txt");
		File.WriteAllText (input, "input");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement (inputProperty, input),
				new XElement (outputProperty, stamp)),
			target,
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", targetName),
				new XElement ("WriteLinesToFile",
					new XAttribute ("File", output),
					new XAttribute ("Lines", $"Directory=$({propertyName});Exists=$([System.IO.Directory]::Exists('$({propertyName})'))"),
					new XAttribute ("Overwrite", "true")),
				new XElement ("RemoveDir",
					new XAttribute ("Directories", $"$({propertyName})"),
					new XAttribute ("Condition", $"'$({propertyName})' != ''")),
				new XElement ("Touch", new XAttribute ("Files", stamp), new XAttribute ("AlwaysCreate", "true")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var first = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:normal", "-nr:false");
		Assert.AreEqual (0, first.ExitCode, first.Output + first.Error);
		string [] created = File.ReadAllLines (output);
		Assert.AreEqual (2, created.Length);
		Assert.AreNotEqual ("Directory=", created [0], "An executed target must set its temporary-directory property.");
		Assert.AreEqual ("Exists=True", created [1], "The temporary directory must exist before consumers run.");

		File.SetLastWriteTimeUtc (stamp, File.GetLastWriteTimeUtc (input).AddSeconds (2));
		var second = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:normal", "-nr:false");
		Assert.AreEqual (0, second.ExitCode, second.Output + second.Error);
		StringAssert.Contains ($"Skipping target \"{targetName}\" because all output files are up-to-date", second.Output);
		CollectionAssert.AreEqual (new [] { "Directory=", "Exists=False" }, File.ReadAllLines (output),
			"A fresh invocation that skips the target must not infer a temporary-directory property.");
	}
}
