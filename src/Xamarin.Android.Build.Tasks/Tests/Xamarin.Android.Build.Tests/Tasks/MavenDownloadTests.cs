#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Java.Interop.Tools.Maven;
using Java.Interop.Tools.Maven.Models;
using Java.Interop.Tools.Maven.Repositories;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
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
	public async Task InsecureHttpRepository_Blocked ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var task = new MavenDownload {
			BuildEngine = engine,
			AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.android.material:material", "1.0.0", "http://repo.example.com/maven2/")],
		};

		await task.RunTaskAsync ();

		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("Insecure HTTP Maven repository URL 'http://repo.example.com/maven2/' is not allowed. Use an HTTPS URL, or set AllowInsecureHttp=\"true\" metadata on the item to override this check.", engine.Errors [0].Message);
	}

	[Test]
	public async Task InsecureHttpRepository_AllowedWithOptIn ()
	{
		var engine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> ());
		var item = CreateMavenTaskItem ("com.example:dummy", "1.0.0", "http://127.0.0.1:1/maven2/");
		item.SetMetadata ("AllowInsecureHttp", "true");

		var task = new MavenDownload {
			BuildEngine = engine,
			MavenCacheDirectory = Path.GetTempPath (),
			AndroidMavenLibraries = [item],
		};

		await task.RunTaskAsync ();

		// Should bypass the XA4252 insecure HTTP check and attempt the download, which fails with XA4236
		Assert.AreEqual (1, engine.Errors.Count);
		Assert.AreEqual ("XA4236", engine.Errors [0].Code, "Expected a download error (XA4236), not a security error (XA4252)");
	}

	[Test]
	public async Task UnknownArtifact ()
	{
		if (TestEnvironment.IsRunningOnCI)
			Assert.Ignore ("The CI mirror returns 401 for uncached artifacts instead of Maven Central's 404.");

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
		if (TestEnvironment.IsRunningOnCI)
			Assert.Ignore ("The CI mirror returns 401 for uncached artifacts instead of Maven Central's 404.");

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
			var dummy_jar_directory = Path.GetDirectoryName (dummy_jar);
			if (dummy_jar_directory is null)
				throw new InvalidOperationException ($"Could not determine the directory for '{dummy_jar}'.");
			Directory.CreateDirectory (dummy_jar_directory);

			using (File.Create (dummy_jar)) { }

			await task.RunTaskAsync ();

			Assert.AreEqual (1, engine.Errors.Count);
			Assert.AreEqual ($"Cannot download POM file for Maven artifact 'com.example:dummy:1.0.0'.{Environment.NewLine}- Failed to resolve POM for Maven artifact 'com.example:dummy:1.0.0' from 'https://repo1.maven.org/maven2/com/example/dummy/1.0.0/dummy-1.0.0.pom'.{Environment.NewLine}- Response status code does not indicate success: 404 (Not Found).", engine.Errors [0].Message?.ReplaceLineEndings ());
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	[Test]
	public void ImportedPomFailureIdentifiesTransitiveArtifactAndRepository ()
	{
		var root = new Artifact ("com.example", "library", "1.0.0");
		var imported = new Artifact ("androidx.compose", "compose-bom", "2024.09.00");
		var root_pom = """
			<project xmlns="http://maven.apache.org/POM/4.0.0">
			  <modelVersion>4.0.0</modelVersion>
			  <groupId>com.example</groupId>
			  <artifactId>library</artifactId>
			  <version>1.0.0</version>
			  <dependencyManagement>
			    <dependencies>
			      <dependency>
			        <groupId>androidx.compose</groupId>
			        <artifactId>compose-bom</artifactId>
			        <version>2024.09.00</version>
			        <type>pom</type>
			        <scope>import</scope>
			      </dependency>
			    </dependencies>
			  </dependencyManagement>
			</project>
			""";
		var repository = new TestMavenRepository (root, root_pom);
		var cache = new CachedMavenRepository (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ()), repository);
		var resolver = new LoggingPomResolver (cache, "https://repo.example.com/maven2/");

		try {
			var exception = Assert.Throws<InvalidOperationException> (() => ResolvedProject.FromArtifact (root, resolver));

			Assert.AreEqual ($"No POM found for {imported}", exception?.Message);
			Assert.AreEqual (imported.VersionedArtifactString, resolver.UnresolvedArtifact?.VersionedArtifactString);
			Assert.AreEqual ("https://repo.example.com/maven2/androidx/compose/compose-bom/2024.09.00/compose-bom-2024.09.00.pom", resolver.UnresolvedPomUrl);
		} finally {
			DeleteTempDirectory (cache.CacheDirectory);
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
				AndroidMavenLibraries = [CreateMavenTaskItem ("com.google.auto.value:auto-value-annotations", "1.10.4", TestEnvironment.DotNetPublicMaven)],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_items = task.ResolvedAndroidMavenLibraries;
			if (output_items is null)
				throw new InvalidOperationException ("MavenDownload did not produce resolved libraries.");
			var output_item = output_items [0];

			Assert.AreEqual ("com.google.auto.value:auto-value-annotations:1.10.4", output_item.GetMetadata ("JavaArtifact"));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.StartWith (temp_cache_dir));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.EndWith (Path.Combine ("com.google.auto.value", "auto-value-annotations", "1.10.4", "auto-value-annotations-1.10.4.pom")));
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
				AndroidMavenLibraries = [CreateMavenTaskItem ("androidx.core:core", "1.12.0", TestEnvironment.DotNetPublicMaven)],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_items = task.ResolvedAndroidMavenLibraries;
			if (output_items is null)
				throw new InvalidOperationException ("MavenDownload did not produce resolved libraries.");
			var output_item = output_items [0];

			Assert.AreEqual ("androidx.core:core:1.12.0", output_item.GetMetadata ("JavaArtifact"));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.StartWith (temp_cache_dir));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.EndWith (Path.Combine ("androidx.core", "core", "1.12.0", "core-1.12.0.pom")));
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
				AndroidMavenLibraries = [CreateMavenTaskItem ("com.facebook.react:react-android", "0.76.1", TestEnvironment.DotNetPublicMaven, artifactFilename: "react-android-0.76.1.module")],
			};

			await task.RunTaskAsync ();

			Assert.AreEqual (0, engine.Errors.Count);
			Assert.AreEqual (1, task.ResolvedAndroidMavenLibraries?.Length);

			var output_items = task.ResolvedAndroidMavenLibraries;
			if (output_items is null)
				throw new InvalidOperationException ("MavenDownload did not produce resolved libraries.");
			var output_item = output_items [0];

			Assert.AreEqual ("com.facebook.react:react-android:0.76.1", output_item.GetMetadata ("JavaArtifact"));
			Assert.True (output_item.ItemSpec.EndsWith (Path.Combine ("0.76.1", "react-android-0.76.1.module"), StringComparison.OrdinalIgnoreCase));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.StartWith (temp_cache_dir));
			Assert.That (output_item.GetMetadata ("Manifest"), Does.EndWith (Path.Combine ("com.facebook.react", "react-android", "0.76.1", "react-android-0.76.1.pom")));
		} finally {
			DeleteTempDirectory (temp_cache_dir);
		}
	}

	// The tests below route every download through TestEnvironment.DotNetPublicMaven, so the
	// "Central"/"Google" shorthands are never exercised there. Cover them directly instead --
	// this needs no network, so it also runs under CI's network isolation.
	[TestCase ("Central", "central")]
	[TestCase ("central", "central")]
	[TestCase ("Google", "google")]
	[TestCase ("google", "google")]
	public void KnownRepositoryShorthand (string metadata, string expectedName)
	{
		var repository = MavenDownload.GetKnownRepository (metadata);

		Assert.IsNotNull (repository);
		Assert.AreEqual (expectedName, repository?.Name);
	}

	[TestCase ("bad-repo")]
	[TestCase ("https://repo1.maven.org/maven2/")]
	public void UnknownRepositoryShorthand (string metadata)
	{
		Assert.IsNull (MavenDownload.GetKnownRepository (metadata));
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

	sealed class TestMavenRepository : IMavenRepository
	{
		readonly Artifact artifact;
		readonly byte [] pom;

		public string Name => "test";

		public TestMavenRepository (Artifact artifact, string pom)
		{
			this.artifact = artifact;
			this.pom = Encoding.UTF8.GetBytes (pom);
		}

		public bool TryGetFile (Artifact artifact, string filename, out Stream stream)
		{
			if (artifact.VersionedArtifactString == this.artifact.VersionedArtifactString) {
				stream = new MemoryStream (pom);
				return true;
			}

			stream = Stream.Null;
			return false;
		}
	}
}
