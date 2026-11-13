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

		var item = JavaArchiveItem (jar, "commonMain/default/manifest");
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

		var item = JavaArchiveItem (jar, "commonMain/default/manifest");
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

		var firstItem = JavaArchiveItem (firstJar, "commonMain/default/manifest");
		var secondItem = JavaArchiveItem (secondJar, "commonMain/default/manifest");
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

		var item = JavaArchiveItem (jar, "commonMain/default/manifest");
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
	public void DuplicateFilesKeepLastItemAfterFlush ()
	{
		var apk = Path.Combine (TempDirectory, "app.aar");
		var first = Path.Combine (TempDirectory, "first.txt");
		var last = Path.Combine (TempDirectory, "last.txt");
		File.WriteAllText (first, "first");
		File.WriteAllText (last, "lastt");
		File.SetLastWriteTimeUtc (last, File.GetLastWriteTimeUtc (first).AddDays (-1));

		var items = new List<ITaskItem> { FileItem (first, "assets/shared.txt") };
		// Crosses ZipArchiveEx's 512-write limit so the first shared.txt is flushed before the last one is added.
		for (var i = 0; i < 511; i++) {
			var filler = Path.Combine (TempDirectory, $"filler{i}.txt");
			File.WriteAllText (filler, $"{i}");
			items.Add (FileItem (filler, $"assets/filler{i}.txt"));
		}
		items.Add (FileItem (last, "assets/shared.txt"));

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = items.ToArray (),
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		using var archive = ZipFile.OpenRead (apk);
		AssertEntryContents (archive, "assets/shared.txt", "lastt");
	}

	static TaskItem FileItem (string path, string archivePath)
	{
		var item = new TaskItem (path);
		item.SetMetadata ("ArchivePath", archivePath);
		return item;
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
		var entry = archive.GetEntry (entryName) ?? throw new InvalidOperationException ($"Missing archive entry '{entryName}'.");
		using var reader = new StreamReader (entry.Open ());
		Assert.AreEqual (expected, reader.ReadToEnd (), entryName);
	}

	static void CreateArchive (string path, params (string name, string contents) [] entries)
	{
		using var stream = File.Create (path);
		using var archive = new ZipArchive (stream, ZipArchiveMode.Create);
		foreach (var entry in entries) {
			using var writer = new StreamWriter (archive.CreateEntry (entry.name).Open (), new UTF8Encoding (false));
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
