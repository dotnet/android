using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

[TestClass]
public class AssemblyCompressionTargetsTests : BaseTest
{
	[TestMethod]
	[DataRow ("none")]
	[DataRow ("mixed")]
	[DataRow ("all")]
	[DataRow ("case-insensitive")]
	public void FailedAssembliesLoseOnlyCompressionMetadata (string failures)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var source = XDocument.Load (targets);
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_ProcessAssemblyCompressionFailures"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}

		var assemblies = new [] {
			(Group: "Framework", Identity: "arm64/System.One.dll", Abi: "arm64-v8a", Culture: "", DestinationSubPath: "System.One.dll"),
			(Group: "Framework", Identity: "x86/System.Two.dll", Abi: "x86", Culture: "", DestinationSubPath: "System.Two.dll"),
			(Group: "User", Identity: "arm64/App.dll", Abi: "arm64-v8a", Culture: "", DestinationSubPath: "App.dll"),
			(Group: "User", Identity: "x86/fr/App.resources.dll", Abi: "x86", Culture: "fr", DestinationSubPath: "fr/App.resources.dll"),
		};
		var failed = assemblies.Where ((assembly, index) => failures == "all" ||
			(failures != "none" && (index == 0 || index == 3))).ToArray ();
		var inputs = new XElement ("ItemGroup",
			assemblies.Select (assembly => new XElement (
				assembly.Group == "Framework" ? "_CompressedResolvedFrameworkAssemblies" : "_CompressedBuildApkResolvedUserAssemblies",
				new XAttribute ("Include", assembly.Identity),
				new XElement ("CompressedAssembly", "compressed/" + assembly.Identity),
				new XElement ("Abi", assembly.Abi),
				new XElement ("Culture", assembly.Culture),
				new XElement ("DestinationSubPath", assembly.DestinationSubPath),
				new XElement ("CustomMetadata", "preserved-" + assembly.Identity))),
			failed.Select (assembly => new XElement ("_FailedToCompressAssemblies",
				new XAttribute ("Include", failures == "case-insensitive" ? assembly.Identity.ToUpperInvariant () : assembly.Identity))));
		string output = Path.Combine (directory, "assemblies.txt");
		string project = Path.Combine (directory, "compression.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup", new XElement ("EmbedAssembliesIntoApk", "True")),
			target,
			// Supply compression results without invoking the compressor or its SDK dependencies.
			new XElement ("Target", new XAttribute ("Name", "_CompressAssemblies"), inputs),
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_ProcessAssemblyCompressionFailures"),
				new XElement ("WriteLinesToFile",
					new XAttribute ("File", output),
					new XAttribute ("Overwrite", "true"),
					new XAttribute ("Lines",
						"@(_BuildApkResolvedFrameworkAssemblies->'Framework|%(Identity)|%(CompressedAssembly)|%(Abi)|%(Culture)|%(DestinationSubPath)|%(CustomMetadata)');" +
						"@(_BuildApkResolvedUserAssemblies->'User|%(Identity)|%(CompressedAssembly)|%(Abi)|%(Culture)|%(DestinationSubPath)|%(CustomMetadata)')")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		var actual = File.ReadAllLines (output);
		var expected = assemblies.Select (assembly => string.Join ("|",
			assembly.Group, assembly.Identity, failed.Contains (assembly) ? "" : "compressed/" + assembly.Identity,
			assembly.Abi, assembly.Culture, assembly.DestinationSubPath, "preserved-" + assembly.Identity)).ToArray ();
		CollectionAssert.AreEquivalent (expected, actual,
			"Every input must appear exactly once in its original group, with only failed items losing CompressedAssembly.");
	}
}
