#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class BuildArchiveTests
{
	string? tempDirectory;

	string TempDirectory => tempDirectory ?? throw new InvalidOperationException ("Setup has not run.");

	[SetUp]
	public void Setup ()
	{
		tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		Directory.CreateDirectory (tempDirectory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (!string.IsNullOrEmpty (tempDirectory) && Directory.Exists (tempDirectory))
			Directory.Delete (tempDirectory, recursive: true);
	}

	[Test]
	public void ConsecutiveUnchangedBuildsKeepJavaArchiveEntries ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var jar = Path.Combine (TempDirectory, "classes.jar");

		CreateArchive (apk, ("AndroidManifest.xml", "manifest"), ("commonMain/default/manifest", "existing"), ("stale.txt", "stale"));
		CreateArchive (jar, ("commonMain/default/manifest", "current"));

		var item = new TaskItem ($"{jar}#commonMain/default/manifest");
		item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
		string? previousSnapshot = null;

		for (var build = 1; build <= 3; build++) {
			var task = new BuildArchive {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ApkOutputPath = apk,
				FilesToAddToArchive = [item],
			};

			Assert.IsTrue (task.RunTask (), $"build {build} should have succeeded");

			var snapshot = GetArchiveSnapshot (apk);
			if (previousSnapshot is not null)
				Assert.AreEqual (previousSnapshot, snapshot, $"build {build} should match the previous unchanged build");
			previousSnapshot = snapshot;

			using (var archive = ZipFile.OpenRead (apk)) {
				archive.AssertEntryContents (apk, "commonMain/default/manifest", "current");
				archive.AssertDoesNotContainEntry (apk, "stale.txt");
			}
		}
	}

	[Test]
	public void ExistingJavaArchiveEntriesAreSkippedWhenUpToDate ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var jar = Path.Combine (TempDirectory, "classes.jar");

		CreateArchive (apk, ("commonMain/default/manifest", "current"));
		CreateArchive (jar, ("commonMain/default/manifest", "current"));

		var item = new TaskItem ($"{jar}#commonMain/default/manifest");
		item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
		var messages = new List<BuildMessageEventArgs> ();

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ($"Skipping commonMain/default/manifest from {jar} as it is up to date."));

		using (var archive = ZipFile.OpenRead (apk)) {
			archive.AssertEntryContents (apk, "commonMain/default/manifest", "current");
		}
	}

	[Test]
	public void DuplicateJavaArchiveEntriesKeepFirstCurrentBuildItem ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var firstJar = Path.Combine (TempDirectory, "first.jar");
		var secondJar = Path.Combine (TempDirectory, "second.jar");

		CreateArchive (apk, ("stale.txt", "stale"));
		CreateArchive (firstJar, ("commonMain/default/manifest", "first"));
		CreateArchive (secondJar, ("commonMain/default/manifest", "second"));

		var firstItem = new TaskItem ($"{firstJar}#commonMain/default/manifest");
		firstItem.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		firstItem.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
		var secondItem = new TaskItem ($"{secondJar}#commonMain/default/manifest");
		secondItem.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		secondItem.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
		var messages = new List<BuildMessageEventArgs> ();

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
			ApkOutputPath = apk,
			FilesToAddToArchive = [firstItem, secondItem],
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ("Failed to add jar entry commonMain/default/manifest from second.jar: the same file already exists in the apk"));

		using (var archive = ZipFile.OpenRead (apk)) {
			archive.AssertEntryContents (apk, "commonMain/default/manifest", "first");
			archive.AssertDoesNotContainEntry (apk, "stale.txt");
		}
	}

	[Test]
	public void MissingJarEntryIsSkippedAndExistingOutputEntryIsRemoved ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var jar = Path.Combine (TempDirectory, "classes.jar");

		CreateArchive (apk, ("commonMain/default/manifest", "existing"));
		CreateArchive (jar, ("other-entry.txt", "contents"));

		var item = new TaskItem ($"{jar}#commonMain/default/manifest");
		item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
		var messages = new List<BuildMessageEventArgs> ();

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ($"Failed to add jar entry commonMain/default/manifest from {jar}: entry not found in jar."));

		// The entry should be removed. If the APK itself no longer exists, all entries were cleared (also satisfies the assertion).
		if (File.Exists (apk)) {
			using (var archive = ZipFile.OpenRead (apk)) {
				archive.AssertDoesNotContainEntry (apk, "commonMain/default/manifest");
			}
		}
	}

	[Test]
	public void StoredBundleManifestRelocationPreservesCompressionMethod ()
	{
		var bundle = Path.Combine (TempDirectory, "app.aab");

		CreateArchive (bundle, ("AndroidManifest.xml", "manifest", CompressionLevel.NoCompression));

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageFormat = "aab",
			ApkOutputPath = bundle,
			FilesToAddToArchive = [],
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		var metadata = ZipArchiveMetadataReader.Read (bundle);
		Assert.IsFalse (metadata.ContainsKey ("AndroidManifest.xml"), "Original manifest entry should be moved.");
		Assert.AreEqual (ZipEntryCompressionMethod.Store, metadata ["manifest/AndroidManifest.xml"].CompressionMethod, "Moved manifest should stay stored.");
	}

	[Test]
	public void ZeroByteStoredFileStabilizesAcrossBuilds ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var emptyFile = Path.Combine (TempDirectory, "empty.dat");
		File.WriteAllBytes (emptyFile, []);

		var item = new TaskItem (emptyFile);
		item.SetMetadata ("ArchivePath", "empty.dat");
		var secondRunMessages = new List<BuildMessageEventArgs> ();

		var firstRun = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
			UncompressedFileExtensions = ".dat",
		};
		Assert.IsTrue (firstRun.RunTask (), "first build should have succeeded");

		var firstSnapshot = GetArchiveSnapshot (apk);
		Assert.AreEqual (ZipEntryCompressionMethod.Store, ZipArchiveMetadataReader.Read (apk) ["empty.dat"].CompressionMethod, "Entry should be stored.");

		var secondRun = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: secondRunMessages),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
			UncompressedFileExtensions = ".dat",
		};
		Assert.IsTrue (secondRun.RunTask (), "second build should have succeeded");

		var secondSnapshot = GetArchiveSnapshot (apk);
		Assert.AreEqual (firstSnapshot, secondSnapshot, "Archive contents should be stable across unchanged builds.");
		Assert.That (secondRunMessages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ($"Skipping {emptyFile} as the archive file is up to date."));
	}

	[Test]
	public void MissingArchivePathUsesCodedError ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			ApkOutputPath = Path.Combine (TempDirectory, "app.apk"),
			FilesToAddToArchive = [new TaskItem ("file.txt")],
		};

		Assert.IsFalse (task.RunTask (), "task should have failed");
		Assert.That (errors, Has.One.Property (nameof (BuildErrorEventArgs.Code)).EqualTo ("XA4234"));
	}

	[Test]
	public void ArchiveRootDirectoryProvidesRelativeEntryPath ()
	{
		var root = Path.Combine (TempDirectory, "classes");
		var classDirectory = Path.Combine (root, "com", "example");
		var classFile = Path.Combine (classDirectory, "Main.class");
		Directory.CreateDirectory (classDirectory);
		File.WriteAllText (classFile, "class");

		var apk = Path.Combine (TempDirectory, "classes.zip");
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			ArchiveRootDirectory = root,
			FilesToAddToArchive = [new TaskItem (classFile)],
			UncompressedFileExtensions = ".class",
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");
		using var archive = ZipFile.OpenRead (apk);
		archive.AssertEntryContents (apk, "com/example/Main.class", "class");
		Assert.AreEqual (ZipEntryCompressionMethod.Store, ZipArchiveMetadataReader.Read (apk) ["com/example/Main.class"].CompressionMethod);
	}

	static void CreateArchive (string path, params (string name, string contents, CompressionLevel compressionLevel) [] entries)
	{
		using (var stream = new FileStream (path, FileMode.Create, FileAccess.ReadWrite))
		using (var archive = new ZipArchive (stream, ZipArchiveMode.Create)) {
			foreach (var entry in entries) {
				var zipEntry = archive.CreateEntry (entry.name, entry.compressionLevel);
				using (var writer = new StreamWriter (zipEntry.Open (), Encoding.UTF8)) {
					writer.Write (entry.contents);
				}
			}
		}
	}

	static void CreateArchive (string path, params (string name, string contents) [] entries)
	{
		CreateArchive (path, entries.Select (entry => (entry.name, entry.contents, CompressionLevel.Optimal)).ToArray ());
	}

	static string GetArchiveSnapshot (string path)
	{
		using var archive = ZipFile.OpenRead (path);
		return string.Join ("\n", archive.Entries
			.OrderBy (entry => entry.FullName, StringComparer.Ordinal)
			.Select (entry => {
				using var stream = new MemoryStream ();
				using (var entryStream = entry.Open ()) {
					entryStream.CopyTo (stream);
				}
				return $"{entry.FullName}:{Convert.ToBase64String (stream.ToArray ())}";
			}));
	}
}
