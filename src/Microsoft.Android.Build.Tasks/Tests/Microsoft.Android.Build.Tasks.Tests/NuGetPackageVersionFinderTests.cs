#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

public class NuGetPackageVersionFinderTests : BaseTest
{
	const string PackageName = "Xamarin.Google.Material.Core";
	const string PackagePath = "xamarin.google.material.core/1.0.0";
	const string NuspecFile = "xamarin.google.material.core.nuspec";
	const string ArtifactTag = "artifact_versioned=com.google.android:material-core:1.0";

	string TestDirectory => Path.Combine (Root, "temp", TestName);

	[TestCase ("1.0.0", "1.0")]
	[TestCase ("1.0.0", "1.0.0.0")]
	[TestCase ("1.0.0", "1.0.0+metadata")]
	[TestCase ("1.0.0-beta.1", "1.0.0-BETA.1")]
	public void PackageVersionsUseNuGetSemantics (string assetVersion, string requestedVersion)
	{
		var cache = Path.Combine (TestDirectory, "packages");
		CreateNuspec (cache);

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var assets = CreateAssets (assetVersion, [cache]);
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (assets.ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		var artifacts = finder.GetArtifactsInNugetPackage (PackageName.ToUpperInvariant (), requestedVersion, task.Log);

		Assert.AreEqual ("com.google.android:material-core:1.0", string.Join ("|", artifacts.Select (artifact => artifact.VersionedArtifactString)));
		Assert.IsEmpty (engine.Errors);
	}

	[Test]
	public void DependencyFulfilledByPackageNuspec ()
	{
		var cache = Path.Combine (TestDirectory, "packages");
		CreateNuspec (cache);
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		var library = new TaskItem ("material.jar");
		library.SetMetadata ("Manifest", pom.FilePath);
		var package = new TaskItem (PackageName);
		package.SetMetadata ("Version", "1.0");

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [library],
			PackageReferences = [package],
			ProjectAssetsLockFile = WriteAssets (CreateAssets ("1.0.0", [cache]).ToJsonString ()),
		};

		Assert.IsTrue (task.RunTask ());
		Assert.IsEmpty (engine.Errors);
	}

	[TestCase ("")]
	[TestCase ("http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd")]
	public void PackageFoldersAndNuspecNamespaces (string xmlNamespace)
	{
		var missingCache = Path.Combine (TestDirectory, "missing-packages");
		var cache = Path.Combine (TestDirectory, "packages with spaces");
		var fallbackCache = Path.Combine (TestDirectory, "fallback-packages");
		CreateNuspec (cache, xmlNamespace);
		CreateNuspec (fallbackCache, xmlNamespace);

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var assets = CreateAssets ("1.0.0", [missingCache, cache, fallbackCache]);
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (assets.ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		var artifacts = finder.GetArtifactsInNugetPackage (PackageName, "1.0.0", task.Log);

		Assert.AreEqual (1, artifacts.Count);
		Assert.AreEqual ("com.google.android:material-core:1.0", artifacts [0].VersionedArtifactString);
		Assert.IsEmpty (engine.Errors);
	}

	[TestCase ("Missing.Package", "1.0.0")]
	[TestCase (PackageName, "2.0.0")]
	public void MissingPackageReportsXA4248 (string name, string version)
	{
		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (CreateAssets ("1.0.0", []).ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		Assert.IsEmpty (finder.GetArtifactsInNugetPackage (name, version, task.Log));
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("XA4248", engine.Errors [0].Code);
	}

	[TestCase (false)]
	[TestCase (true)]
	public void MissingNuspecDoesNotProvideArtifacts (bool listedInAssets)
	{
		var package = new JsonObject {
			["path"] = PackagePath,
			["files"] = listedInAssets ? new JsonArray (NuspecFile) : new JsonArray (),
		};
		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var assets = CreateAssets ("1.0.0", [Path.Combine (TestDirectory, "packages")], package);
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (assets.ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		Assert.IsEmpty (finder.GetArtifactsInNugetPackage (PackageName, "1.0.0", task.Log));
		Assert.IsEmpty (engine.Errors);
	}

	[TestCase (null)]
	[TestCase ("")]
	public void MissingNuspecTagsDoNotProvideArtifacts (string? tags)
	{
		var cache = Path.Combine (TestDirectory, "packages");
		CreateNuspec (cache, tags: tags);

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (CreateAssets ("1.0.0", [cache]).ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		Assert.IsEmpty (finder.GetArtifactsInNugetPackage (PackageName, "1.0.0", task.Log));
		Assert.IsEmpty (engine.Errors);
	}

	[Test]
	public void AssetsWithCommentsAndTrailingCommas ()
	{
		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var json = """
			{
				// Unknown properties are ignored.
				"version": 3,
				"libraries": {},
				"packageFolders": {},
			}
			""";

		Assert.IsNotNull (NuGetPackageVersionFinder.Create (WriteAssets (json), task.Log));
		Assert.IsEmpty (engine.Errors);
	}

	[TestCase ("not JSON")]
	[TestCase ("null")]
	[TestCase ("""{"libraries": []}""")]
	[TestCase ("""{"libraries": {"invalid": {}}}""")]
	[TestCase ("""{"libraries": {"Package/1.0.0": {"files": ["package.nuspec"]}}}""")]
	[TestCase ("""{"packageFolders": []}""")]
	public void MalformedAssetsAreLogged (string json)
	{
		var engine = new MockBuildEngine (TestContext.Out, [], [], []);
		var task = new JavaDependencyVerification { BuildEngine = engine };

		Assert.IsNull (NuGetPackageVersionFinder.Create (WriteAssets (json), task.Log));
		Assert.That (engine.Messages, Has.Exactly (1).Matches<Microsoft.Build.Framework.BuildMessageEventArgs> (message =>
			message.Message?.StartsWith ("Could not parse NuGet lock file.", StringComparison.Ordinal) == true));
		Assert.IsEmpty (engine.Errors);
	}

	string WriteAssets (string json)
	{
		Directory.CreateDirectory (TestDirectory);
		var path = Path.Combine (TestDirectory, "project.assets.json");
		File.WriteAllText (path, json);
		return path;
	}

	static JsonObject CreateAssets (string version, string [] packageFolders, JsonObject? package = null)
	{
		var folders = new JsonObject ();
		foreach (var folder in packageFolders)
			folders [folder] = new JsonObject ();

		return new JsonObject {
			["version"] = 3,
			["libraries"] = new JsonObject {
				[$"{PackageName}/{version}"] = package ?? new JsonObject {
					["type"] = "package",
					["path"] = PackagePath,
					["files"] = new JsonArray ("lib/net10.0/material.dll", NuspecFile),
				},
				["BindingProject/1.0.0"] = new JsonObject {
					["type"] = "project",
					["path"] = "../BindingProject.csproj",
					["msbuildProject"] = "../BindingProject.csproj",
				},
			},
			["packageFolders"] = folders,
		};
	}

	static void CreateNuspec (string cache, string xmlNamespace = "", string? tags = ArtifactTag)
	{
		var directory = Path.Combine (cache, PackagePath);
		Directory.CreateDirectory (directory);
		XNamespace ns = xmlNamespace;
		var metadata = new XElement (ns + "metadata",
			new XElement (ns + "id", PackageName),
			new XElement (ns + "version", "1.0.0"));
		if (tags is not null)
			metadata.Add (new XElement (ns + "tags", tags));

		new XDocument (new XElement (ns + "package", metadata)).Save (Path.Combine (directory, NuspecFile));
	}
}
