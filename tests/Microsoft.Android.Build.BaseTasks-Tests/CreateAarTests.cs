using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests;

[TestFixture]
[NonParallelizable]
public class CreateAarTests
{
	string directory;

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, nameof (CreateAarTests), TestContext.CurrentContext.Test.ID);
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[Test]
	public void LargeInputsAreStreamedInsteadOfBuffered ()
	{
		var input = Path.Combine (directory, "input.jar");
		const int length = 16 * 1024 * 1024;
		using (var stream = File.Create (input))
			stream.SetLength (length);
		var task = CreateTask ();
		task.JarFiles = [new TaskItem (input)];
		var before = GC.GetAllocatedBytesForCurrentThread ();

		Assert.IsTrue (task.RunTask ());

		var allocated = GC.GetAllocatedBytesForCurrentThread () - before;
		Assert.Less (allocated, length / 2, "Writing an AAR should not buffer its uncompressed input.");
		using var archive = ZipFile.OpenRead (task.OutputFile);
		Assert.AreEqual (length, archive.Entries [0].Length);
	}

	[Test]
	public void DuplicateAssetPathsKeepTheLastInput ()
	{
		var first = Path.Combine (directory, "first.txt");
		var second = Path.Combine (directory, "second.txt");
		File.WriteAllText (first, "first");
		File.WriteAllText (second, "last");
		var firstItem = new TaskItem (first);
		var secondItem = new TaskItem (second);
		firstItem.SetMetadata ("Link", Path.Combine ("Assets", "same.txt"));
		secondItem.SetMetadata ("Link", Path.Combine ("Assets", "same.txt"));
		var task = CreateTask ();
		task.AndroidAssets = [firstItem, secondItem];

		Assert.IsTrue (task.RunTask ());

		using var archive = ZipFile.OpenRead (task.OutputFile);
		Assert.AreEqual (1, archive.Entries.Count);
		Assert.AreEqual ("assets/same.txt", archive.Entries [0].FullName);
		using var reader = new StreamReader (archive.Entries [0].Open ());
		Assert.AreEqual ("last", reader.ReadToEnd ());
	}

	CreateAar CreateTask () => new CreateAar {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		AssetDirectory = "Assets",
		PrefixProperty = "MonoAndroidAssetsPrefix",
		OutputFile = Path.Combine (directory, "library.aar"),
	};
}
