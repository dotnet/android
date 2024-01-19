#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Java.Interop.Maven.Models;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

// - PackageReference with nuget tags
// - ProjectAssetsLockFile?
// - Download microsoft-packages.json

public class JavaDependencyVerificationTests
{
	[Test]
	public void NoManifestsSpecified ()
	{
		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
		};

		Assert.True (task.RunTask ());
	}

	[Test]
	public void MissingPom ()
	{
		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", "missing.pom")],
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Requested POM file 'missing.pom' does not exist.", engine.Errors [0].Message);
	}

	[Test]
	public void MalformedPom ()
	{
		using var pom = new TemporaryFile ("this is not valid XML");

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath)],
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.True (engine.Errors [0].Message?.StartsWith ("Failed to register POM file"));
	}

	[Test]
	public void NoSpecifiedDependencies ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0").BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath)],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void MissingSpecifiedDependency ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "missing", "1.0")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath)],
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Java dependency 'com.google.android:missing' version '1.0' is not satisfied.", engine.Errors [0].Message);
	}

	[Test]
	public void MissingParentSpecifiedDependency ()
	{
		using var parent_pom = new PomBuilder ("com.google.android", "material-parent", "1.0")
			.WithDependencyManagement ("com.google.android", "missing", "2.0")
			.BuildTemporary ();

		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithParent ("com.google.android", "material-parent", "1.0")
			.WithDependency ("com.google.android", "missing", "")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath)],
			AdditionalManifests = [CreateAndroidAdditionManifestTaskItem (parent_pom.FilePath)],
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Java dependency 'com.google.android:missing' version '2.0' is not satisfied.", engine.Errors [0].Message);
	}

	[Test]
	public void MissingSpecifiedDependencyWithNugetSuggestion ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		using var package_finder = CreateMicrosoftNuGetPackageFinder ("com.google.android:material-core", "Xamarin.Google.Material.Core");

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath)],
			MicrosoftPackagesFile = package_finder.FilePath,
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Java dependency 'com.google.android:material-core' version '1.0' is not satisfied. Microsoft maintains the NuGet package 'Xamarin.Google.Material.Core' that could fulfill this dependency.", engine.Errors [0].Message);
	}

	[Test]
	public void MalformedMicrosoftPackagesJson ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		using var package_finder = new TemporaryFile ("This is not valid json!", "microsoft-packages.json");

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
				CreateAndroidLibraryTaskItem ("com.google.android.material-core.jar", null, "com.google.android:material-core", "1.0"),
			],
			MicrosoftPackagesFile = package_finder.FilePath,
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void DependencyFulfilledByAndroidLibrary ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
				CreateAndroidLibraryTaskItem ("com.google.android.material-core.jar", null, "com.google.android:material-core", "1.0"),
			],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void DependencyFulfilledByProjectReferenceExplicitMetadata ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
			],
			ProjectReferences = [
				CreateAndroidLibraryTaskItem ("Google.Material.Core.csproj", null, "com.google.android:material-core", "1.0"),
			],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void DependencyFulfilledByPackageReferenceExplicitMetadata ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
			],
			PackageReferences = [
				CreateAndroidLibraryTaskItem ("Xamarin.Google.Material.Core", null, "com.google.android:material-core", "1.0"),
			],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void DependencyIgnored ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", "1.0")
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
			],
			IgnoredDependencies = [
				CreateAndroidLibraryTaskItem ("com.google.android:material-core", rawVersion: "1.0"),
			],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
	}

	[Test]
	public void DependencyWithoutVersionFulfilled ()
	{
		// The dependency is fulfilled but the version isn't checked
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", null)
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, [], []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
				CreateAndroidLibraryTaskItem ("com.google.android.material-core.jar", null, "com.google.android:material-core", "1.0"),
			],
		};

		var result = task.RunTask ();

		Assert.True (result);
		Assert.AreEqual (0, engine.Errors.Count);
		Assert.AreEqual (1, engine.Warnings.Count);
		Assert.AreEqual ("Could not determine required version of Java dependency 'com.google.android:material-core'. Validation of this dependency will not take version into account.", engine.Warnings [0].Message);
	}

	[Test]
	public void DependencyWithoutVersionNotFulfilled ()
	{
		using var pom = new PomBuilder ("com.google.android", "material", "1.0")
			.WithDependency ("com.google.android", "material-core", null)
			.BuildTemporary ();

		var engine = new MockBuildEngine (TestContext.Out, [], []);
		var task = new JavaDependencyVerification {
			BuildEngine = engine,
			AndroidLibraries = [
				CreateAndroidLibraryTaskItem ("com.google.android.material.jar", pom.FilePath),
			],
		};

		var result = task.RunTask ();

		Assert.False (result);
		Assert.AreEqual (1, engine.Warnings.Count);
		Assert.AreEqual ("Could not determine required version of Java dependency 'com.google.android:material-core'. Validation of this dependency will not take version into account.", engine.Warnings [0].Message);
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Java dependency 'com.google.android:material-core' is not satisfied.", engine.Errors [0].Message);
	}

	TaskItem CreateAndroidLibraryTaskItem (string name, string? manifest = null, string? javaArtifact = null, string? javaVersion = null, string? rawVersion = null)
	{
		var item = new TaskItem (name);

		if (manifest is not null)
			item.SetMetadata ("Manifest", manifest);
		if (javaArtifact is not null)
			item.SetMetadata ("JavaArtifact", javaArtifact);
		if (javaVersion is not null)
			item.SetMetadata ("JavaVersion", javaVersion);
		if (rawVersion is not null)
			item.SetMetadata ("Version", rawVersion);

		return item;
	}

	TaskItem CreateAndroidAdditionManifestTaskItem (string name)
	{
		var item = new TaskItem (name);

		return item;
	}

	TemporaryFile CreateMicrosoftNuGetPackageFinder (string javaId, string nugetId)
	{
		var package = new MicrosoftNuGetPackageFinder.PackageListFile {
			Packages = [new MicrosoftNuGetPackageFinder.Package { JavaId = javaId, NuGetId = nugetId }]
		};

		return new TemporaryFile (JsonConvert.SerializeObject (package), "microsoft-packages.json");
	}
}

class TemporaryFile : IDisposable
{
	public string Content { get; }
	public string FilePath { get; }

	public TemporaryFile (string content, string? filename = null)
	{
		Content = content;
		FilePath = Path.Combine (Path.GetTempPath (), filename ?? Path.GetTempFileName ());

		File.WriteAllText (FilePath, content);
	}

	public void Dispose ()
	{
		try {
			File.Delete (FilePath);
		} catch {
		}
	}
}

class PomBuilder
{
	public string GroupId { get; }
	public string ArtifactId { get; }
	public string? Version { get; }
	public List<Dependency> Dependencies { get; } = new ();
	public List<Dependency> DependencyManagement { get; } = new ();
	public string? ParentGroupId { get; set; }
	public string? ParentArtifactId { get; set; }
	public string? ParentVersion { get; set; }

	public PomBuilder (string groupId, string artifactId, string? version)
	{
		GroupId = groupId;
		ArtifactId = artifactId;
		Version = version;
	}

	public string Build ()
	{
		using var sw = new StringWriter ();
		using var xw = XmlWriter.Create (sw);

		xw.WriteStartDocument ();
		xw.WriteStartElement ("project", "http://maven.apache.org/POM/4.0.0");

		xw.WriteElementString ("modelVersion", "4.0.0");
		xw.WriteElementString ("groupId", GroupId);
		xw.WriteElementString ("artifactId", ArtifactId);

		if (Version.HasValue ())
			xw.WriteElementString ("version", Version);

		if (ParentGroupId.HasValue () && ParentArtifactId.HasValue ()) {
			xw.WriteStartElement ("parent");

			xw.WriteElementString ("groupId", ParentGroupId);
			xw.WriteElementString ("artifactId", ParentArtifactId);

			if (ParentVersion.HasValue ())
				xw.WriteElementString ("version", ParentVersion);

			xw.WriteEndElement ();	// parent
		}

		if (DependencyManagement.Any ()) {
			xw.WriteStartElement ("dependencyManagement");
			xw.WriteStartElement ("dependencies");

			foreach (var dependency in DependencyManagement) {
				xw.WriteStartElement ("dependency");

				xw.WriteElementString ("groupId", dependency.GroupId);
				xw.WriteElementString ("artifactId", dependency.ArtifactId);

				if (dependency.Version.HasValue ())
					xw.WriteElementString ("version", dependency.Version);

				xw.WriteEndElement ();  // dependency
			}

			xw.WriteEndElement ();  // dependencies
			xw.WriteEndElement ();  // dependencyManagement
		}


		if (Dependencies.Any ()) {
			xw.WriteStartElement ("dependencies");

			foreach (var dependency in Dependencies) {
				xw.WriteStartElement ("dependency");

				xw.WriteElementString ("groupId", dependency.GroupId);
				xw.WriteElementString ("artifactId", dependency.ArtifactId);

				if (dependency.Version.HasValue ())
					xw.WriteElementString ("version", dependency.Version);

				xw.WriteEndElement ();  // dependency
			}

			xw.WriteEndElement ();  // dependencies
		}
		xw.WriteEndElement ();	// project
		xw.Close ();

		return sw.ToString ();
	}

	public PomBuilder WithDependency (string groupId, string artifactId, string? version)
	{
		Dependencies.Add (new Dependency {
			GroupId = groupId,
			ArtifactId = artifactId,
			Version = version,
		});

		return this;
	}

	public PomBuilder WithDependencyManagement (string groupId, string artifactId, string? version)
	{
		DependencyManagement.Add (new Dependency {
			GroupId = groupId,
			ArtifactId = artifactId,
			Version = version,
		});

		return this;
	}

	public PomBuilder WithParent (string groupId, string artifactId, string? version)
	{
		ParentGroupId = groupId;
		ParentArtifactId = artifactId;
		ParentVersion = version;

		return this;
	}

	public TemporaryFile BuildTemporary () => new TemporaryFile (Build ());
}
