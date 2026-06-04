#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Tools.Zip;

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
		if (!tempDirectory.IsNullOrEmpty () && Directory.Exists (tempDirectory))
			Directory.Delete (tempDirectory, recursive: true);
	}

	[Test]
	public void ExistingJavaArchiveEntriesAreUpdated ()
	{
		var apk = Path.Combine (TempDirectory, "app.apk");
		var jar = Path.Combine (TempDirectory, "classes.jar");

		CreateArchive (apk, ("commonMain/default/manifest", "existing"), ("stale.txt", "stale"));
		CreateArchive (jar, ("commonMain/default/manifest", "current"));

		var item = new TaskItem ($"{jar}#commonMain/default/manifest");
		item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
		item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");

		var task = new BuildArchive {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApkOutputPath = apk,
			FilesToAddToArchive = new ITaskItem [] { item },
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		using (var archive = ZipArchive.Open (apk, FileMode.Open)) {
			archive.AssertEntryContents (apk, "commonMain/default/manifest", "current");
			archive.AssertDoesNotContainEntry (apk, "stale.txt");
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
			FilesToAddToArchive = new ITaskItem [] { item },
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ($"Skipping commonMain/default/manifest from {jar} as it is up to date."));

		using (var archive = ZipArchive.Open (apk, FileMode.Open)) {
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
			FilesToAddToArchive = new ITaskItem [] { firstItem, secondItem },
		};

		Assert.IsTrue (task.RunTask (), "task should have succeeded");

		Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ("Failed to add jar entry commonMain/default/manifest from second.jar: the same file already exists in the apk"));

		using (var archive = ZipArchive.Open (apk, FileMode.Open)) {
			archive.AssertEntryContents (apk, "commonMain/default/manifest", "first");
			archive.AssertDoesNotContainEntry (apk, "stale.txt");
		}
	}

	static void CreateArchive (string path, params (string name, string contents) [] entries)
	{
		using (var stream = File.Create (path))
		using (var archive = ZipArchive.Create (stream)) {
			foreach (var entry in entries) {
				archive.AddEntry (entry.name, entry.contents, encoding: Encoding.UTF8);
			}
		}
	}
}
