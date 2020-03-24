using System;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class CopyIfChangedTests
	{
		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		string NewFile (string contents = null, string fileName = "")
		{
			if (string.IsNullOrEmpty (fileName)) {
				fileName = Path.GetRandomFileName ();
			}
			var path = Path.Combine (tempDirectory, fileName);
			if (!string.IsNullOrEmpty (contents)) {
				Directory.CreateDirectory (Path.GetDirectoryName (path));
				File.WriteAllText (path, contents);
			}
			return path;
		}

		ITaskItem [] ToArray (string path)
		{
			return new [] { new TaskItem (path) };
		}

		[Test]
		public void DestinationNoExist ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ();

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void NoChange ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "No changes should have been made.");
		}

		[Test]
		public void DestinationOlder ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void DestinationOlderNoLengthCheck ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
				CompareFileLengths = false,
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void SourceOlder ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "No changes should have been made.");
		}

		[Test]
		public void SourceOlderDifferentLength ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foofoo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void SourceOlderDifferentLengthButNoLengthCheck ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foofoo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
				CompareFileLengths = false,
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "Changes should not have been made.");
			FileAssert.AreNotEqual (src, dest);
		}

		[Test]
		public void CaseChanges ()
		{
			var src = NewFile (contents: "Foo");
			var dest = NewFile (contents: "foo", fileName: "foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			dest = dest.Replace ("foo", "Foo");
			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			FileAssert.AreEqual (src, dest);

			var files = Directory.GetFiles (Path.GetDirectoryName (dest), "Foo");
			Assert.AreEqual ("Foo", Path.GetFileName (files [0]), "File name should match");
		}
	}
}
