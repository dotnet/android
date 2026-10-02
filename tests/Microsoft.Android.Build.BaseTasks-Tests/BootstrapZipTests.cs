using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using NUnit.Framework;
using BootstrapZip = Xamarin.Android.Tools.BootstrapTasks.Zip;

namespace Microsoft.Android.Build.BaseTasks.Tests;

[TestFixture]
public class BootstrapZipTests
{
	string directory;

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, nameof (BootstrapZipTests), TestContext.CurrentContext.Test.ID);
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ReplacingEntriesRemovesEveryExistingDuplicate (bool nested)
	{
		var sourceRoot = Path.Combine (directory, "source");
		var sourceDirectory = nested ? Path.Combine (sourceRoot, "nested") : sourceRoot;
		Directory.CreateDirectory (sourceDirectory);
		var source = Path.Combine (sourceDirectory, "payload.txt");
		File.WriteAllText (source, "current");
		var archivePath = Path.Combine (directory, "archive.zip");
		var entryName = nested ? "nested/payload.txt" : "payload.txt";
		using (var archive = ZipFile.Open (archivePath, ZipArchiveMode.Create)) {
			WriteEntry (archive, entryName, "first stale entry");
			WriteEntry (archive, entryName, "second stale entry");
			WriteEntry (archive, "keep.txt", "unchanged");
		}
		var task = new BootstrapZip {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			File = new TaskItem (archivePath),
			Prefix = new TaskItem (sourceRoot),
			Entries = [new TaskItem (source)],
		};

		Assert.IsTrue (task.Execute ());

		using var result = ZipFile.OpenRead (archivePath);
		Assert.AreEqual (1, result.Entries.Count (entry => entry.FullName == entryName),
			"Replacement must remove every stale duplicate, not just the first one.");
		Assert.AreEqual (2, result.Entries.Count);
		var replacedEntry = result.GetEntry (entryName) ?? throw new InvalidOperationException ($"Missing '{entryName}'.");
		using var reader = new StreamReader (replacedEntry.Open ());
		Assert.AreEqual ("current", reader.ReadToEnd ());
		var retainedEntry = result.GetEntry ("keep.txt") ?? throw new InvalidOperationException ("Missing 'keep.txt'.");
		using var retained = new StreamReader (retainedEntry.Open ());
		Assert.AreEqual ("unchanged", retained.ReadToEnd ());
	}

	static void WriteEntry (ZipArchive archive, string name, string contents)
	{
		using var writer = new StreamWriter (archive.CreateEntry (name).Open ());
		writer.Write (contents);
	}
}
