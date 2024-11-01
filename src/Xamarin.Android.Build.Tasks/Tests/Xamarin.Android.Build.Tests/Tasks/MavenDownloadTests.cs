#nullable enable

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
		Assert.AreEqual ("'<AndroidMavenLibrary>' item 'com.google.android.material:material' is missing required attribute 'Version'.", engine.Errors [0].Message);
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
		Assert.AreEqual ($"Cannot download Maven artifact 'com.example:dummy'.{Environment.NewLine}- dummy-1.0.0.jar: Response status code does not indicate success: 404 (Not Found).{Environment.NewLine}- dummy-1.0.0.aar: Response status code does not indicate success: 404 (Not Found).", engine.Errors [0].Message?.ReplaceLineEndings ());
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
			var dummy_jar = Path.Combine (temp_cache_dir, "central", "com.example", "dummy", "1.0.0", "dummy-1.0.0.jar");
			Directory.CreateDirectory (Path.GetDirectoryName (dummy_jar)!);

			using (File.Create (dummy_jar)) { }

			await task.RunTaskAsync ();

			Assert.AreEqual (1, engine.Errors.Count);
			Assert.AreEqual ($"Cannot download POM file for Maven artifact 'com.example:dummy:1.0.0'.{Environment.NewLine}- Response status code does not indicate success: 404 (Not Found).", engine.Errors [0].Message?.ReplaceLineEndings ());
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
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_item = task.ResolvedAndroidMavenLibraries! [0];

			Assert.AreEqual ("com.google.auto.value:auto-value-annotations:1.10.4", output_item.GetMetadata ("JavaArtifact"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "central", "com.google.auto.value", "auto-value-annotations", "1.10.4", "auto-value-annotations-1.10.4.pom"), output_item.GetMetadata ("Manifest"));
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
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_item = task.ResolvedAndroidMavenLibraries! [0];

			Assert.AreEqual ("androidx.core:core:1.12.0", output_item.GetMetadata ("JavaArtifact"));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "google", "androidx.core", "core", "1.12.0", "core-1.12.0.pom"), output_item.GetMetadata ("Manifest"));
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	[Test]
	public async Task ArtifactFilenameOverride ()
	{
		// Technically the artifact is 'react-android-0.76.1-release.aar' but we're going to override the filename to
		// 'react-android-0.76.1.module' and download it instead for this test because the real .aar is 120+ MB.
		var temp_cache_dir = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());

		try {
			var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
			var task = new MavenDownload {
				BuildEngine = engine,
				MavenCacheDirectory = temp_cache_dir,
				AndroidMavenLibraries = [CreateMavenTaskItem ("com.facebook.react:react-android", "0.76.1", artifactFilename: "react-android-0.76.1.module")],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_item = task.ResolvedAndroidMavenLibraries! [0];

			Assert.AreEqual ("com.facebook.react:react-android:0.76.1", output_item.GetMetadata ("JavaArtifact"));
			Assert.True (output_item.ItemSpec.EndsWith (Path.Combine ("0.76.1", "react-android-0.76.1.module"), StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual (Path.Combine (temp_cache_dir, "central", "com.facebook.react", "react-android", "0.76.1", "react-android-0.76.1.pom"), output_item.GetMetadata ("Manifest"));
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	ITaskItem CreateMavenTaskItem (string name, string? version, string? repository = null, string? artifactFilename = null)
	{
		var item = new TaskItem (name);

		if (version is not null)
			item.SetMetadata ("Version", version);
		if (repository is not null)
			item.SetMetadata ("Repository", repository);
		if (artifactFilename is not null)
			item.SetMetadata ("ArtifactFilename", artifactFilename);

		return item;
	}

	public static void DeleteTempDirectory (string dir)
	{
		try {
			Directory.Delete (dir, true);
		} catch {
			// Ignore any cleanup failure
		}
	}
}
