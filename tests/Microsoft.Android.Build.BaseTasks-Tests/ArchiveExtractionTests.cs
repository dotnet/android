using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools.BootstrapTasks;

namespace Microsoft.Android.Build.BaseTasks.Tests;

[TestFixture]
public class ArchiveExtractionTests
{
	string directory;

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, nameof (ArchiveExtractionTests), TestContext.CurrentContext.Test.ID);
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void TearDown ()
	{
		if (Directory.Exists (directory))
			Directory.Delete (directory, recursive: true);
	}

	[TestCase ("../../../outside/payload")]
	[TestCase (@"..\..\..\outside\payload")]
	[TestCase (@"nested/..\..\..\..\outside\payload")]
	[TestCase ("../DEST/payload")]
	[TestCase ("../dest-sibling/payload")]
	public void SharedExtractionRejectsEscapingNames (string entryName)
	{
		var root = Path.Combine (directory, "one", "two", "dest");
		var zip = CreateArchive (entryName);
		using var archive = ZipFile.OpenRead (zip);
		Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "outside")));
		Assert.IsFalse (Directory.Exists (root));
	}

	[TestCase (true)]
	[TestCase (false)]
	public void BootstrapExtractionRejectsTraversalAfterStripping (bool noSubdirectory)
	{
		var root = Path.Combine (directory, "one", "two", "dest");
		var entryName = noSubdirectory ? "../../../outside/payload" : "top/../../../outside/payload";
		var zip = CreateArchive (entryName);
		var task = new UnzipDirectoryChildren {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			DestinationFolder = new TaskItem (root),
			SourceFiles = [new TaskItem (zip)],
			NoSubdirectory = noSubdirectory,
		};
		var exception = Assert.Catch (() => task.Execute ());
		Assert.IsInstanceOf<InvalidDataException> (exception?.GetBaseException ());
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "outside")));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void SelectedExtractionRejectsTraversal (bool rename)
	{
		var root = Path.Combine (directory, "one", "two", "dest");
		var entryName = rename ? "valid.txt" : "../DEST/payload";
		var zip = CreateArchive (entryName);
		var item = new TaskItem (entryName);
		if (rename)
			item.SetMetadata ("DestinationFileName", "../../../outside/payload");
		var task = new UnzipToFolder {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			Sources = [new TaskItem (zip)],
			DestinationDirectories = [new TaskItem (root)],
			Files = [item],
		};

		Assert.Throws<InvalidDataException> (() => task.RunTask ());
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "outside")));
		Assert.IsFalse (File.Exists (Path.Combine (root, "payload")));
	}

	[TestCase ("../../../../../outside/annotations.zip")]
	[TestCase (@"..\..\..\..\..\outside\annotations.zip")]
	[TestCase ("../TEST.AAR/annotations.zip")]
	public void AarExtractionRejectsTraversal (string entryName)
	{
		var zip = CreateArchive (entryName, "test.aar");
		var task = new ExtractJarsFromAar {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			Libraries = [zip],
			OutputJarsDirectory = Path.Combine (directory, "one", "two", "three", "jars"),
			OutputAnnotationsDirectory = Path.Combine (directory, "one", "two", "three", "annotations"),
			OutputReferenceJarsDirectory = Path.Combine (directory, "reference-jars"),
			OutputReferenceAnnotationsDirectory = Path.Combine (directory, "reference-annotations"),
		};

		Assert.Throws<InvalidDataException> (() => task.RunTask ());
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "outside")));
	}

	[TestCase ("../../../outside/payload")]
	[TestCase (@"..\..\..\outside\payload")]
	[TestCase ("../../../outside/")]
	public void ApiReferenceExtractionRejectsTraversal (string entryName)
	{
		var compatibility = Path.Combine (directory, "one");
		var root = Path.Combine (compatibility, "reference", "net10.0");
		Directory.CreateDirectory (Path.GetDirectoryName (root));
		CreateArchive (entryName, Path.Combine ("one", "reference", "contract.zip"));
		var implementation = Path.Combine (directory, "implementation");
		Directory.CreateDirectory (implementation);
		var task = new CheckApiCompatibility {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			ApiLevel = "v17.2",
			LastStableApiLevel = "v17.2",
			TargetImplementationPath = implementation,
			ApiCompatibilityPath = compatibility,
			TargetFramework = "net10.0",
		};

		Assert.Throws<InvalidDataException> (() => task.Execute ());
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "outside")));
	}

	[TestCase ("")]
	[TestCase (".")]
	[TestCase ("..")]
	[TestCase ("\0")]
	[TestCase ("/absolute/file")]
	[TestCase (@"\absolute\file")]
	[TestCase ("C:/absolute/file")]
	[TestCase (@"C:\absolute\file")]
	[TestCase ("C:relative")]
	[TestCase ("//server/share/file")]
	[TestCase (@"\\server\share\file")]
	[TestCase ("file:stream")]
	[TestCase ("nested/../file")]
	[TestCase ("nested/")]
	public void ExtractionPathRejectsUnsafeForms (string entryName)
	{
		var root = Path.Combine (directory, "dest");
		Assert.Throws<InvalidDataException> (() => Files.GetArchiveExtractionPath (root, entryName));
		Assert.IsFalse (Directory.Exists (root));
	}

	[TestCase ("nested/file")]
	[TestCase (@"nested\file")]
	[TestCase (@"nested/./file")]
	public void ExtractionPathNormalizesValidRelativeNames (string entryName)
	{
		var root = Path.Combine (directory, "dest");
		Assert.AreEqual (Path.Combine (root, "nested", "file"), Files.GetArchiveExtractionPath (root + Path.DirectorySeparatorChar, entryName));
	}

	[Test]
	public void ExtractionPathAcceptsRootDirectoryEntries ()
	{
		Assert.AreEqual (directory, Files.GetArchiveExtractionPath (directory, "./", isDirectory: true));
		Assert.Throws<InvalidDataException> (() => Files.GetArchiveExtractionPath (directory, "./"));
	}

	[TestCase ("shared")]
	[TestCase ("selected")]
	[TestCase ("bootstrap")]
	[TestCase ("aar")]
	[TestCase ("api")]
	public void ExtractorsRejectRootedEntries (string extractor)
	{
		var outside = Path.Combine (directory, "outside");
		var entryName = Path.Combine (outside, "annotations.zip");
		var root = Path.Combine (directory, "one", "two", "dest");
		var zip = CreateArchive (entryName);
		var engine = new MockBuildEngine (TestContext.Out);
		switch (extractor) {
			case "shared":
				using (var archive = ZipFile.OpenRead (zip))
					Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));
				break;
			case "selected":
				var selected = new UnzipToFolder {
					BuildEngine = engine,
					Sources = [new TaskItem (zip)],
					DestinationDirectories = [new TaskItem (root)],
					Files = [new TaskItem (entryName)],
				};
				Assert.Throws<InvalidDataException> (() => selected.RunTask ());
				break;
			case "bootstrap":
				var bootstrap = new UnzipDirectoryChildren {
					BuildEngine = engine,
					SourceFiles = [new TaskItem (zip)],
					DestinationFolder = new TaskItem (root),
					NoSubdirectory = true,
				};
				var failure = Assert.Catch (() => bootstrap.Execute ());
				Assert.IsInstanceOf<InvalidDataException> (failure?.GetBaseException ());
				break;
			case "aar":
				var aar = new ExtractJarsFromAar {
					BuildEngine = engine,
					Libraries = [zip],
					OutputJarsDirectory = root,
					OutputAnnotationsDirectory = root,
					OutputReferenceJarsDirectory = Path.Combine (directory, "references"),
					OutputReferenceAnnotationsDirectory = Path.Combine (directory, "reference-annotations"),
				};
				Assert.Throws<InvalidDataException> (() => aar.RunTask ());
				break;
			case "api":
				var compatibility = Path.Combine (directory, "one");
				var reference = Path.Combine (compatibility, "reference");
				Directory.CreateDirectory (reference);
				File.Move (zip, Path.Combine (reference, "contract.zip"));
				var implementation = Path.Combine (directory, "implementation");
				Directory.CreateDirectory (implementation);
				var api = new CheckApiCompatibility {
					BuildEngine = engine,
					ApiLevel = "v17.2",
					LastStableApiLevel = "v17.2",
					TargetImplementationPath = implementation,
					ApiCompatibilityPath = compatibility,
					TargetFramework = "net10.0",
				};
				Assert.Throws<InvalidDataException> (() => api.Execute ());
				break;
			default:
				throw new InvalidOperationException ($"Unknown extractor '{extractor}'.");
		}
		Assert.IsFalse (Directory.Exists (outside));
	}

	[TestCase ("../outside")]
	[TestCase (@"..\outside")]
	public void BootstrapExtractionValidatesDestinationMetadata (string destination)
	{
		var root = Path.Combine (directory, "one", "two", "dest");
		var zip = CreateArchive ("top/payload");
		var item = new TaskItem (zip);
		item.SetMetadata ("DestDir", destination);
		var task = new UnzipDirectoryChildren {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			SourceFiles = [item],
			DestinationFolder = new TaskItem (root),
		};
		var failure = Assert.Catch (() => task.Execute ());
		Assert.IsInstanceOf<InvalidDataException> (failure?.GetBaseException ());
		Assert.IsFalse (Directory.Exists (Path.GetFullPath (Path.Combine (root, destination))));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void BootstrapExtractionKeepsValidDirectoryLayout (bool noSubdirectory)
	{
		var root = Path.Combine (directory, "dest");
		var zip = CreateArchive (@"top\nested\payload");
		var item = new TaskItem (zip);
		item.SetMetadata ("DestDir", "tools");
		var task = new UnzipDirectoryChildren {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			SourceFiles = [item],
			DestinationFolder = new TaskItem (root),
			NoSubdirectory = noSubdirectory,
		};

		Assert.IsTrue (task.Execute ());

		var filename = noSubdirectory ? Path.Combine (root, "tools", "top", "nested", "payload") : Path.Combine (root, "tools", "nested", "payload");
		Assert.AreEqual ("payload", File.ReadAllText (filename));
	}

	[Test]
	public void SelectedExtractionPreservesRenameAndOverwritesInsideDestination ()
	{
		var root = Path.Combine (directory, "dest");
		Directory.CreateDirectory (Path.Combine (root, "nested"));
		var zip = CreateArchive ("original");
		File.WriteAllText (Path.Combine (root, "nested", "renamed"), "old");
		var item = new TaskItem ("original");
		item.SetMetadata ("DestinationFileName", @"nested\renamed");
		var task = new UnzipToFolder {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			Sources = [new TaskItem (zip)],
			DestinationDirectories = [new TaskItem (root)],
			Files = [item],
		};

		Assert.IsTrue (task.RunTask ());
		Assert.AreEqual ("payload", File.ReadAllText (Path.Combine (root, "nested", "renamed")));
	}

	[Test]
	public void SelectedFileCannotReplaceTheDestinationRoot ()
	{
		var root = Path.Combine (directory, "dest");
		var zip = CreateArchive ("file");
		var item = new TaskItem ("file");
		item.SetMetadata ("DestinationFileName", "./");
		var task = new UnzipToFolder {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			Sources = [new TaskItem (zip)],
			DestinationDirectories = [new TaskItem (root)],
			Files = [item],
		};

		Assert.Throws<InvalidDataException> (() => task.RunTask ());
		Assert.IsTrue (Directory.Exists (root));
	}

	[Test]
	public void SharedExtractionValidatesAllEntriesBeforeChangingDestination ()
	{
		var root = Path.Combine (directory, "one", "two", "dest");
		Directory.CreateDirectory (root);
		var existing = Path.Combine (root, "existing");
		File.WriteAllText (existing, "keep");
		var zip = CreateArchive ("valid");
		using (var archive = ZipFile.Open (zip, ZipArchiveMode.Update))
			archive.CreateEntry ("../../../outside/payload");

		using (var archive = ZipFile.OpenRead (zip))
			Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));

		Assert.IsFalse (File.Exists (Path.Combine (root, "valid")));
		Assert.AreEqual ("keep", File.ReadAllText (existing));
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void ExtractionRejectsLinkedDescendantsButAllowsConfiguredRootLink ()
	{
		var root = Path.Combine (directory, "dest");
		var outside = Path.Combine (directory, "outside");
		Directory.CreateDirectory (root);
		Directory.CreateDirectory (outside);
		File.WriteAllText (Path.Combine (outside, "payload"), "untouched");
		var link = Path.Combine (root, "linked");
		Directory.CreateSymbolicLink (link, outside);
		var zip = CreateArchive ("linked/payload");
		using (var archive = ZipFile.OpenRead (zip))
			Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));
		Assert.AreEqual ("untouched", File.ReadAllText (Path.Combine (outside, "payload")));
		Directory.Delete (link);

		var rootLink = Path.Combine (directory, "root-link");
		Directory.CreateSymbolicLink (rootLink, root);
		Assert.AreEqual (Path.Combine (rootLink, "payload"), Files.GetArchiveExtractionPath (rootLink, "payload"));
		Directory.Delete (rootLink);
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void ExtractionRejectsDanglingFileLinks ()
	{
		var root = Path.Combine (directory, "dest");
		Directory.CreateDirectory (root);
		var outside = Path.Combine (directory, "outside");
		File.CreateSymbolicLink (Path.Combine (root, "payload"), outside);
		var zip = CreateArchive ("payload");

		using (var archive = ZipFile.OpenRead (zip))
			Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));

		Assert.IsFalse (File.Exists (outside));
		File.Delete (Path.Combine (root, "payload"));
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void CleanupCannotFollowLinkedDirectories ()
	{
		var root = Path.Combine (directory, "dest");
		var outside = Path.Combine (directory, "outside");
		Directory.CreateDirectory (root);
		Directory.CreateDirectory (outside);
		File.WriteAllText (Path.Combine (outside, "payload"), "keep");
		var link = Path.Combine (root, "linked");
		Directory.CreateSymbolicLink (link, outside);
		var zip = Path.Combine (directory, "empty.zip");
		using (ZipFile.Open (zip, ZipArchiveMode.Create)) { }

		using (var archive = ZipFile.OpenRead (zip))
			Assert.Throws<InvalidDataException> (() => Files.ExtractAll (archive, root));

		Assert.AreEqual ("keep", File.ReadAllText (Path.Combine (outside, "payload")));
		Directory.Delete (link);
	}

	string CreateArchive (string entryName, string filename = "input.zip")
	{
		var zip = Path.Combine (directory, filename);
		Directory.CreateDirectory (Path.GetDirectoryName (zip));
		using var archive = ZipFile.Open (zip, ZipArchiveMode.Create);
		var entry = archive.CreateEntry (entryName);
		using var writer = new StreamWriter (entry.Open ());
		if (!entryName.EndsWith ("/", StringComparison.Ordinal))
			writer.Write ("payload");
		return zip;
	}
}
