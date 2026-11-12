using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class ExtraPackagesTargetsTests : BaseTest
{
	[TestCase ("custom_imports")]
	[TestCase ("library_project_imports")]
	public void CollectsPackagesInLibraryAndImportOrder (string importsDirectory)
	{
		string directory = CreateDirectory ();
		string libraries = Path.Combine (directory, "obj", "__library_projects__");
		foreach (string library in new [] { "z-library", "a-library" }) {
			WriteManifest (Path.Combine (libraries, library, importsDirectory), $"{library}.custom");
			WriteManifest (Path.Combine (libraries, library, importsDirectory, "bin"), $"{library}.bin");
			if (importsDirectory != "library_project_imports") {
				WriteManifest (Path.Combine (libraries, library, "library_project_imports"), $"{library}.legacy");
			}
		}
		string expected = string.Join (":", Directory.GetDirectories (libraries).SelectMany (library =>
			importsDirectory == "library_project_imports"
				? new [] { $"{Path.GetFileName (library)}.bin" }
				: new [] { $"{Path.GetFileName (library)}.bin", $"{Path.GetFileName (library)}.legacy" }));

		AssertPackages (directory, importsDirectory, expected);
	}

	[Test]
	public void DeduplicatesWithCaseAndPreservesEscaping ()
	{
		string directory = CreateDirectory ();
		string library = Path.Combine (directory, "obj", "__library_projects__", "spaces ; % ' [brackets] $");
		WriteManifest (Path.Combine (library, "custom_imports"), "com.example.Same");
		WriteManifest (Path.Combine (library, "library_project_imports"), "com.example.same");
		AssertPackages (directory, "custom_imports", "com.example.Same:com.example.same");
	}

	[Test]
	public void PreservesPackageWhitespaceAndXmlEntities ()
	{
		string directory = CreateDirectory ();
		string library = Path.Combine (directory, "obj", "__library_projects__", "library");
		WriteManifest (Path.Combine (library, "custom_imports"), " com.example;percent%3B&entity'\" ");
		WriteManifest (Path.Combine (library, "library_project_imports"), " com.example;percent%3B&entity'\" ");
		AssertPackages (directory, "custom_imports", " com.example;percent%3B&entity'\" ");
	}

	[TestCase (false)]
	[TestCase (true)]
	public void MissingLibrariesOrManifestsProduceEmptyPackages (bool createLibrary)
	{
		string directory = CreateDirectory ();
		if (createLibrary) {
			Directory.CreateDirectory (Path.Combine (directory, "obj", "__library_projects__", "empty", "custom_imports"));
		}
		AssertPackages (directory, "custom_imports", "");
	}

	[TestCase ("<manifest />", TestName = "MissingPackageIsSkipped")]
	[TestCase ("<other package=\"ignored\" />", TestName = "NonManifestRootIsSkipped")]
	public void MissingPackageOrManifestRootIsSkipped (string manifest)
	{
		string directory = CreateDirectory ();
		WriteManifestText (directory, manifest);
		AssertPackages (directory, "custom_imports", "");
	}

	[TestCase ("<manifest", "MSB3733", TestName = "MalformedManifestReportsXmlPeekError")]
	[TestCase ("<manifest package=\"\" />", "MSB4028", TestName = "EmptyPackageReportsItemError")]
	[TestCase ("""
		<!DOCTYPE manifest [<!ATTLIST manifest package CDATA "default.package">]>
		<manifest />
		""", "MSB3733", TestName = "DtdDefaultPackageIsNotResolved")]
	[TestCase ("""
		<!DOCTYPE manifest [<!ENTITY package "entity.package">]>
		<manifest package="&package;" />
		""", "MSB3733", TestName = "DtdEntityIsNotResolved")]
	public void InvalidManifestsReportXmlPeekErrors (string manifest, string errorCode)
	{
		string directory = CreateDirectory ();
		WriteManifestText (directory, manifest);
		var result = BuildTarget (directory, "custom_imports");
		Assert.AreNotEqual (0, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains (errorCode, result.Output + result.Error);
	}

	string CreateDirectory ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		return directory;
	}

	static void WriteManifest (string directory, string package)
	{
		Directory.CreateDirectory (directory);
		new XDocument (new XElement ("manifest", new XAttribute ("package", package)))
			.Save (Path.Combine (directory, "AndroidManifest.xml"));
	}

	static void WriteManifestText (string directory, string manifest)
	{
		string imports = Path.Combine (directory, "obj", "__library_projects__", "library", "custom_imports");
		Directory.CreateDirectory (imports);
		using var writer = File.CreateText (Path.Combine (imports, "AndroidManifest.xml"));
		writer.Write (manifest);
	}

	static void AssertPackages (string directory, string importsDirectory, string expected)
	{
		var result = BuildTarget (directory, importsDirectory);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("BEGIN" + expected + "END",
			(string?) XDocument.Load (Path.Combine (directory, "packages.xml")).Root?.Attribute ("value"));
	}

	static (int ExitCode, string Output, string Error) BuildTarget (string directory, string importsDirectory)
	{
		string source = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var target = new XElement (XDocument.Load (source).Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_GetAaptExtraPackages"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		string output = Path.Combine (directory, "packages.xml");
		new XDocument (new XElement ("packages", new XAttribute ("value", ""))).Save (output);
		string project = Path.Combine (directory, "packages.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("IntermediateOutputPath", Path.Combine (directory, "obj")),
				new XElement ("_LibraryProjectImportsDirectoryName", importsDirectory),
				new XElement ("AaptExtraPackages", "stale.package")),
			target,
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_GetAaptExtraPackages"),
				new XElement ("XmlPoke",
					new XAttribute ("XmlInputPath", output),
					new XAttribute ("Query", "/packages/@value"),
					new XAttribute ("Value", "BEGIN$(AaptExtraPackages)END")))))
			.Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		return NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
	}
}
