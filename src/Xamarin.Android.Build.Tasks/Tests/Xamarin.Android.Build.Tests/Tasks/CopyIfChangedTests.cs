using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class CopyIfChangedTests
	{
		string tempDirectory;

		[TestInitialize]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		}

		[TestCleanup]
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

		[TestMethod]
		public void DestinationNoExist ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ();
			var messages = new List<BuildMessageEventArgs> ();

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out, messages: messages),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			Assert.AreEqual (dest, task.ModifiedFiles [0].ItemSpec);
			Assert.IsEmpty (messages, "ModifiedFiles should not be dumped to the log.");
			CollectionAssert.AreEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));
		}

		[TestMethod]
		public void NoChange ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now);
			var messages = new List<BuildMessageEventArgs> ();

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out, messages: messages),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "No changes should have been made.");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("it was not changed.")));
			Assert.IsFalse (messages.Any (m => m.Message == " ModifiedFiles:"));
		}

		[TestMethod]
		public void DestinationOlder ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			CollectionAssert.AreEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));
		}

		[TestMethod]
		public void DestinationOlderNoLengthCheck ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
				CompareFileLengths = false,
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			CollectionAssert.AreEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));
		}

		[TestMethod]
		public void SourceOlder ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "No changes should have been made.");
		}

		[TestMethod]
		public void SourceOlderDifferentLength ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foofoo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			CollectionAssert.AreEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));
		}

		[TestMethod]
		public void SourceOlderDifferentLengthButNoLengthCheck ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foofoo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now.AddSeconds (-1)); // source is older
			File.SetLastWriteTimeUtc (dest, now);

			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
				CompareFileLengths = false,
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.ModifiedFiles.Length, "Changes should not have been made.");
			CollectionAssert.AreNotEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));
		}

		[TestMethod]
		public void CaseChanges ()
		{
			var src = NewFile (contents: "Foo");
			var dest = NewFile (contents: "foo", fileName: "foo");
			var now = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (src, now);
			File.SetLastWriteTimeUtc (dest, now.AddSeconds (-1)); // destination is older

			dest = dest.Replace ("foo", "Foo");
			var task = new CopyIfChanged {
				BuildEngine = new MockBuildEngine (Console.Out),
				SourceFiles = ToArray (src),
				DestinationFiles = ToArray (dest),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.ModifiedFiles.Length, "Changes should have been made.");
			CollectionAssert.AreEqual (File.ReadAllBytes (src), File.ReadAllBytes (dest));

			var files = Directory.GetFiles (Path.GetDirectoryName (dest), "Foo");
			Assert.AreEqual ("Foo", Path.GetFileName (files [0]), "File name should match");
		}
	}
}
