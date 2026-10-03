#nullable enable
using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

public class NuGetPackageVersionFinderTests : BaseTest
{
	const string PackageName = "Xamarin.Google.Material.Core";
	const string PackagePath = "xamarin.google.material.core/1.0.0";
	const string NuspecFile = "xamarin.google.material.core.nuspec";
	const string ArtifactTag = "artifact_versioned=com.google.android:material-core:1.0";

	string TestDirectory => Path.Combine (Root, "temp", TestName);

	[Test]
	public void PackageVersionsUseNuGetSemantics ()
	{
		var cache = Path.Combine (TestDirectory, "packages");
		CreateNuspec (cache);

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification { BuildEngine = engine };
		var assets = CreateAssets ("1.0.0", [cache]);
		var finder = NuGetPackageVersionFinder.Create (WriteAssets (assets.ToJsonString ()), task.Log)
			?? throw new InvalidOperationException ("Could not read assets file.");

		var artifacts = finder.GetArtifactsInNugetPackage (PackageName.ToUpperInvariant (), "1.0", task.Log);

		Assert.AreEqual (1, artifacts.Count);
		Assert.AreEqual ("com.google.android:material-core:1.0", artifacts [0].VersionedArtifactString);
		Assert.IsEmpty (engine.Errors);
	}

	[Test]
	public void DependencyFulfilledByPackageNuspec ()
	{
		var missingCache = Path.Combine (TestDirectory, "missing-packages");
		var cache = Path.Combine (TestDirectory, "packages");
		CreateNuspec (cache, metadataNamespace: "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");
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
			ProjectAssetsLockFile = WriteAssets (CreateAssets ("1.0.0", [missingCache, cache]).ToJsonString ()),
		};

		Assert.IsTrue (task.RunTask ());
		Assert.IsEmpty (engine.Errors);
	}

	[Test]
	public void MalformedAssetsAreLogged ()
	{
		var engine = new MockBuildEngine (TestContext.Out, [], [], []);
		var task = new JavaDependencyVerification { BuildEngine = engine };

		Assert.IsNull (NuGetPackageVersionFinder.Create (WriteAssets ("not JSON"), task.Log));
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

	static JsonObject CreateAssets (string version, string [] packageFolders)
	{
		var folders = new JsonObject ();
		foreach (var folder in packageFolders)
			folders [folder] = new JsonObject ();

		return new JsonObject {
			["version"] = 3,
			["libraries"] = new JsonObject {
				[$"{PackageName}/{version}"] = new JsonObject {
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

	static void CreateNuspec (string cache, string packageNamespace = "", string metadataNamespace = "")
	{
		var directory = Path.Combine (cache, PackagePath);
		Directory.CreateDirectory (directory);
		File.WriteAllText (Path.Combine (directory, NuspecFile), $"""
			<package xmlns="{packageNamespace}">
				<metadata xmlns="{metadataNamespace}">
					<id>{PackageName}</id>
					<version>1.0.0</version>
					<tags>{ArtifactTag}</tags>
				</metadata>
			</package>
			""");
	}
}
