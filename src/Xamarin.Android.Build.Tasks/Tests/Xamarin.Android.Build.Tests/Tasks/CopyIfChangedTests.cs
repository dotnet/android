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
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		string NewFile (string contents = null)
		{
			var path = Path.Combine (tempDirectory, Path.GetRandomFileName ());
			if (!string.IsNullOrEmpty (contents)) {
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
		}
	}
}
