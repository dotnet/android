using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Task = System.Threading.Tasks.Task;
namespace Xamarin.Android.Build.Tests;

public class MavenDownloadTests
{
	[Test]
	public async Task MissingVersionMetadata ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.android.material:material", null)],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("'<AndroidMavenLibrary>' item 'com.google.android.material:material' is missing required metadata 'Version'", engine.Errors [0].Message);
	}

	[Test]
	public async Task InvalidArtifactSpecification_WrongNumberOfParts ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.android.material", "1.0.0")],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Maven artifact specification 'com.google.android.material' is invalid. The correct format is 'group_id:artifact_id'.", engine.Errors [0].Message);
	}

	[Test]
	public async Task InvalidArtifactSpecification_EmptyPart ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.android.material: ", "1.0.0")],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Maven artifact specification 'com.google.android.material: ' is invalid. The correct format is 'group_id:artifact_id'.", engine.Errors [0].Message);
	}

	[Test]
	public async Task UnknownRepository ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.android.material:material", "1.0.0", "bad-repo")],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Unknown Maven repository: 'bad-repo'.", engine.Errors [0].Message);
	}

	[Test]
	public async Task UnknownArtifact ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			MavenCacheDirectory = Path.GetTempPath (),
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.example:dummy", "1.0.0")],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ($"Cannot download Maven artifact 'com.example:dummy'.{Environment.NewLine}- com.example_dummy.jar: Response status code does not indicate success: 404 (Not Found).{Environment.NewLine}- com.example_dummy.aar: Response status code does not indicate success: 404 (Not Found).", engine.Errors [0].Message.ReplaceLineEndings ());
	}

	[Test]
	public async Task UnknownPom ()
	{
		var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());

		try {
			var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
			var task = new MavenDownload {
				BuildEngine = engine,
				MavenCacheDirectory = temp_cache_dir,
				AndroidMavenLibraries = [CreateMavenTaskItem ("com.example:dummy", "1.0.0")],
			};

			// Create the dummy jar so we bypass that step and try to download the dummy pom
			var dummy_jar = Path.Combine (temp_cache_dir, "central", "com.example", "dummy", "1.0.0", "com.example_dummy.jar");
			Directory.CreateDirectory (Path.GetDirectoryName (dummy_jar));

			using (File.Create (dummy_jar)) { }

			await task.RunTaskAsync ();

			Assert.AreEqual (1, engine.Errors.Count);
			Assert.AreEqual ($"Cannot download POM file for Maven artifact 'com.example:dummy'.{Environment.NewLine}- com.example_dummy.pom: Response status code does not indicate success: 404 (Not Found).", engine.Errors [0].Message.ReplaceLineEndings ());
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	[Test]
	public async Task MavenCentralSuccess ()
	{
		var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());

		try {
			var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
			var task = new MavenDownload {
				BuildEngine = engine,
				MavenCacheDirectory = temp_cache_dir,
				AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.auto.value:auto-value-annotations", "1.10.4")],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries.Length);

			var output_item = task.ResolvedAndroidMavenLibraries [0];

			Assert.AreEqual ("com.google.auto.value:auto-value-annotations", output_item.GetMetadata ("ArtifactSpec"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "central", "com.google.auto.value", "auto-value-annotations", "1.10.4", "com.google.auto.value_auto-value-annotations.jar"), output_item.GetMetadata ("ArtifactFile"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "central", "com.google.auto.value", "auto-value-annotations", "1.10.4", "com.google.auto.value_auto-value-annotations.pom"), output_item.GetMetadata ("ArtifactPom"));

			Assert.True (File.Exists (output_item.GetMetadata ("ArtifactFile")));
			Assert.True (File.Exists (output_item.GetMetadata ("ArtifactPom")));
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	[Test]
	public async Task MavenGoogleSuccess ()
	{
		var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());

		try {
			var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
			var task = new MavenDownload {
				BuildEngine = engine,
				MavenCacheDirectory = temp_cache_dir,
				AndroidMavenLibraries = [CreateMavenTaskItem ("androidx.core:core", "1.12.0", "Google")],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries.Length);

			var output_item = task.ResolvedAndroidMavenLibraries [0];

			Assert.AreEqual ("androidx.core:core", output_item.GetMetadata ("ArtifactSpec"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "google", "androidx.core", "core", "1.12.0", "androidx.core_core.aar"), output_item.GetMetadata ("ArtifactFile"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "google", "androidx.core", "core", "1.12.0", "androidx.core_core.pom"), output_item.GetMetadata ("ArtifactPom"));

			Assert.True (File.Exists (output_item.GetMetadata ("ArtifactFile")));
			Assert.True (File.Exists (output_item.GetMetadata ("ArtifactPom")));
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	ITaskItem CreateMavenTaskItem (string name, string version, string repository = null)
	{
		var item = new TaskItem (name);

		if (version is not null)
			item.SetMetadata ("Version", version);
		if (repository is not null)
			item.SetMetadata ("Repository", repository);

		return item;
	}

	void DeleteTempDirectory (string dir)
	{
		try {
			Directory.Delete (dir, true);
		} catch {
			// Ignore any cleanup failure
		}
	}
}
