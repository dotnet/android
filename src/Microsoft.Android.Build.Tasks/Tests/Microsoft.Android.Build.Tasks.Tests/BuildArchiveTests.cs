#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class BuildArchiveTests : BaseTest
{
	string TempDirectory => Path.Combine (Root, "temp", TestName);

	[SetUp]
	public void Setup ()
	{
		Directory.CreateDirectory (TempDirectory);
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
				AssertEntryContents (archive, "commonMain/default/manifest", "current");
				Assert.IsNull (archive.GetEntry ("stale.txt"));
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
			AssertEntryContents (archive, "commonMain/default/manifest", "current");
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
			AssertEntryContents (archive, "commonMain/default/manifest", "first");
			Assert.IsNull (archive.GetEntry ("stale.txt"));
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
				Assert.IsNull (archive.GetEntry ("commonMain/default/manifest"));
			}
		}
	}

	[Test]
	public void UnchangedBuildDoesNotRewriteOutput ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var file = Path.Combine (TempDirectory, "contents.txt");
		var jar = Path.Combine (TempDirectory, "classes.jar");
		File.WriteAllText (file, "contents");
		CreateArchive (jar, ("jar-entry.txt", "jar contents"));
		var item = new TaskItem (file);
		item.SetMetadata ("ArchivePath", "assets/contents.txt");
		var jarItem = JavaArchiveItem (jar, "jar-entry.txt");
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item, jarItem],
		};
		Assert.IsTrue (task.RunTask ());
		var bytes = File.ReadAllBytes (apk);
		var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc (apk, timestamp);

		for (var build = 0; build < 2; build++) {
			Assert.IsTrue (task.RunTask ());
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (apk), "An unchanged build should preserve ZIP bytes.");
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (apk), "An unchanged build should preserve the output timestamp.");
		}
	}

	[Test]
	public void ChangedSameSizeFileWithinDosTimestampIsUpdated ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var file = Path.Combine (TempDirectory, "contents.txt");
		var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		File.WriteAllText (file, "before");
		File.SetLastWriteTimeUtc (file, timestamp);
		var item = new TaskItem (file);
		item.SetMetadata ("ArchivePath", "assets/contents.txt");
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
		};
		Assert.IsTrue (task.RunTask ());

		File.WriteAllText (file, "after!");
		File.SetLastWriteTimeUtc (file, timestamp.AddSeconds (1));
		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (apk);
		AssertEntryContents (archive, "assets/contents.txt", "after!");
	}

	[TestCase ("apk", false)]
	[TestCase ("aab", true)]
	public void UncompressedExtensionsRespectPackageFormat (string packageFormat, bool compressed)
	{
		var apk = Path.Combine (TempDirectory, $"app.{packageFormat}");
		var file = Path.Combine (TempDirectory, "contents.bin");
		File.WriteAllText (file, new string ('A', 4096));
		var item = new TaskItem (file);
		item.SetMetadata ("ArchivePath", "assets/contents.bin");
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageFormat = packageFormat,
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
			UncompressedFileExtensions = " .BIN ; dat, .so ",
		};
		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (apk);
		var entry = archive.GetEntry ("assets/contents.bin") ?? throw new InvalidOperationException ("Missing contents.bin.");
		AssertCompression (entry, compressed);
		if (compressed)
			Assert.Less (entry.CompressedLength, entry.Length);
	}

	[TestCase (true, false, true)]
	[TestCase (false, true, false)]
	public void ChangedCompressionUpdatesExistingEntry (bool before, bool after, bool compressed)
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var file = Path.Combine (TempDirectory, "contents.bin");
		File.WriteAllText (file, new string ('A', 4096));
		var item = new TaskItem (file);
		item.SetMetadata ("ArchivePath", "assets/contents.bin");
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
			UncompressedFileExtensions = before ? ".bin" : "",
		};
		Assert.IsTrue (task.RunTask ());

		task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [item],
			UncompressedFileExtensions = after ? ".bin" : "",
		};
		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (apk);
		var entry = archive.GetEntry ("assets/contents.bin") ?? throw new InvalidOperationException ("Missing contents.bin.");
		AssertCompression (entry, compressed);
		Assert.AreEqual (1, archive.Entries.Count);
		AssertEntryContents (archive, "assets/contents.bin", new string ('A', 4096));
	}

	[TestCase (CompressionLevel.NoCompression, false)]
	[TestCase (CompressionLevel.Optimal, true)]
	public void MovingManifestAndNormalizingPathsPreservesCompression (CompressionLevel compression, bool compressed)
	{
		var input = Path.Combine (TempDirectory, "resources.apk");
		var output = Path.Combine (TempDirectory, "app.aab");
		CreateArchive (input, compression, ("AndroidManifest.xml", "manifest"), (@"assets\contents.txt", "contents"));
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageFormat = "aab",
			ApkInputPath = input,
			ApkOutputPath = output,
		};
		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (output);
		Assert.IsNull (archive.GetEntry ("AndroidManifest.xml"));
		Assert.IsNull (archive.GetEntry (@"assets\contents.txt"));
		foreach (var entry in archive.Entries)
			AssertCompression (entry, compressed);
		AssertEntryContents (archive, "manifest/AndroidManifest.xml", "manifest");
		AssertEntryContents (archive, "assets/contents.txt", "contents");
	}

	[Test]
	public void DuplicateEntriesInsideJarUseFirstEntry ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var jar = Path.Combine (TempDirectory, "classes.jar");
		CreateArchive (jar, ("duplicate.txt", "first"), ("duplicate.txt", "second"));
		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = [JavaArchiveItem (jar, "duplicate.txt")],
		};
		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (apk);
		Assert.AreEqual (1, archive.Entries.Count);
		AssertEntryContents (archive, "duplicate.txt", "first");
	}

	static TaskItem JavaArchiveItem (string path, string entryName)
	{
		var item = new TaskItem ($"{path}#{entryName}");
		item.SetMetadata ("ArchivePath", entryName);
		item.SetMetadata ("JavaArchiveEntry", entryName);
		return item;
	}

	static void AssertEntryContents (ZipArchive archive, string entryName, string expected)
	{
		var entry = archive.GetEntry (entryName);
		Assert.IsNotNull (entry, $"Archive should contain '{entryName}'.");
		if (entry == null)
			return;
		using var reader = new StreamReader (entry.Open ());
		Assert.AreEqual (expected, reader.ReadToEnd (), entryName);
	}

	static void AssertCompression (ZipArchiveEntry entry, bool compressed)
	{
		Assert.AreEqual (compressed ? ZipCompressionMethod.Deflate : ZipCompressionMethod.Stored, entry.CompressionMethod);
		if (compressed)
			Assert.AreNotEqual (entry.Length, entry.CompressedLength, $"{entry.FullName} should be DEFLATE compressed.");
		else
			Assert.AreEqual (entry.Length, entry.CompressedLength, $"{entry.FullName} should be STORED.");
	}

	static void CreateArchive (string path, params (string name, string contents) [] entries) =>
		CreateArchive (path, CompressionLevel.Optimal, entries);

	static void CreateArchive (string path, CompressionLevel compression, params (string name, string contents) [] entries)
	{
		using var stream = File.Create (path);
		using var archive = new ZipArchive (stream, ZipArchiveMode.Create);
		foreach (var entry in entries) {
			using var writer = new StreamWriter (archive.CreateEntry (entry.name, compression).Open (), new UTF8Encoding (false));
			writer.Write (entry.contents);
		}
	}

	static string GetArchiveSnapshot (string path)
	{
		using var archive = ZipFile.OpenRead (path);
		return string.Join ("\n", archive.Entries
			.OrderBy (entry => entry.FullName, StringComparer.Ordinal)
			.Select (entry => {
				using var source = entry.Open ();
				using var stream = new MemoryStream ();
				source.CopyTo (stream);
				return $"{entry.FullName}:{Convert.ToBase64String (stream.ToArray ())}";
			}));
	}
}
