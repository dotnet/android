using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class SyncExtractedAarFilesTests : BaseTest
	{
		List<BuildErrorEventArgs>? errors;
		MockBuildEngine? engine;
		string path = "";
		string staging = "";
		string output = "";

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, errors: errors = new List<BuildErrorEventArgs> ());
			path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
			if (Directory.Exists (path))
				Directory.Delete (path, recursive: true);
			staging = Path.Combine (path, "staging");
			output = Path.Combine (path, "obj");
			Directory.CreateDirectory (staging);
			Directory.CreateDirectory (output);
		}

		SyncExtractedAarFiles CreateTask () => new SyncExtractedAarFiles {
			BuildEngine = engine,
			SourceDirectory = staging,
			DestinationDirectory = output,
			Directories = ["library_project_jars"],
		};

		void WriteStaged (string relativePath, string content)
		{
			var file = Path.Combine (staging, "library_project_jars", relativePath);
			var directory = Path.GetDirectoryName (file);
			if (directory != null)
				Directory.CreateDirectory (directory);
			File.WriteAllText (file, content);
		}

		[Test]
		public void BackslashEntryIsWrittenAsNestedPath ()
		{
			if (IsWindows)
				Assert.Ignore ("Backslashes are directory separators on Windows.");

			// Unzip writes a `libs\helper.jar` entry as a literal file name on Unix.
			WriteStaged (Path.Combine ("foo.aar", "libs\\helper.jar"), "helper");

			Assert.IsTrue (CreateTask ().Execute (), "Task should succeed.");
			var expected = Path.Combine (output, "library_project_jars", "foo.aar", "libs", "helper.jar");
			FileAssert.Exists (expected);
			Assert.AreEqual ("helper", File.ReadAllText (expected));
			FileAssert.DoesNotExist (Path.Combine (output, "library_project_jars", "foo.aar", "libs\\helper.jar"));
		}

		[Test]
		public void UnchangedFileKeepsTimestampAndStaleFilesAreDeleted ()
		{
			WriteStaged (Path.Combine ("foo.aar", "classes.jar"), "classes");
			WriteStaged (Path.Combine ("foo.aar", "libs", "old.jar"), "old");
			Assert.IsTrue (CreateTask ().Execute (), "First run should succeed.");

			var classes = Path.Combine (output, "library_project_jars", "foo.aar", "classes.jar");
			var old = Path.Combine (output, "library_project_jars", "foo.aar", "libs", "old.jar");
			var timestamp = new DateTime (2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (classes, timestamp);

			Directory.Delete (staging, recursive: true);
			WriteStaged (Path.Combine ("foo.aar", "classes.jar"), "classes");
			Assert.IsTrue (CreateTask ().Execute (), "Second run should succeed.");

			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (classes), "Unchanged file should not be rewritten.");
			FileAssert.DoesNotExist (old, "Stale file should be deleted.");
		}

		[Test]
		public void StaleFileComparisonUsesPlatformCaseSensitivity ()
		{
			WriteStaged (Path.Combine ("foo.aar", "Classes.jar"), "classes");
			Assert.IsTrue (CreateTask ().Execute (), "First run should succeed.");

			Directory.Delete (staging, recursive: true);
			WriteStaged (Path.Combine ("foo.aar", "classes.jar"), "classes");
			Assert.IsTrue (CreateTask ().Execute (), "Second run should succeed.");

			var names = Directory.GetFiles (Path.Combine (output, "library_project_jars", "foo.aar")).Select (Path.GetFileName).ToArray ();
			Assert.AreEqual (1, names.Length, "Only one file should remain.");
			// Windows paths are case-insensitive, so the existing file is kept; elsewhere the stale `Classes.jar` is replaced.
			Assert.AreEqual (IsWindows ? "Classes.jar" : "classes.jar", names [0]);
		}

		[Test]
		public void LinkedOutputDirectoryIsRejected ()
		{
			var outside = Path.Combine (path, "outside");
			Directory.CreateDirectory (outside);
			Directory.CreateDirectory (Path.Combine (output, "library_project_jars"));
			var link = Path.Combine (output, "library_project_jars", "foo.aar");
			try {
				Directory.CreateSymbolicLink (link, outside);
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
				Assert.Ignore ($"Unable to create a symbolic link: {e.Message}");
			}
			WriteStaged (Path.Combine ("foo.aar", "classes.jar"), "classes");

			Assert.IsFalse (CreateTask ().Execute (), "Task should reject linked output directories.");
			Assert.AreEqual (1, errors?.Count, "The unsafe destination should be reported as a build error.");
			FileAssert.DoesNotExist (Path.Combine (outside, "classes.jar"), "No file should be written through the link.");
		}

		[Test]
		public void LinkedCategoryDirectoryIsRejected ()
		{
			var outside = Path.Combine (path, "outside");
			Directory.CreateDirectory (outside);
			File.WriteAllText (Path.Combine (outside, "keep.jar"), "keep");
			try {
				Directory.CreateSymbolicLink (Path.Combine (output, "library_project_jars"), outside);
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
				Assert.Ignore ($"Unable to create a symbolic link: {e.Message}");
			}

			Assert.IsFalse (CreateTask ().Execute (), "Task should reject a linked category directory.");
			FileAssert.Exists (Path.Combine (outside, "keep.jar"), "No file should be deleted through the link.");
		}
	}
}
