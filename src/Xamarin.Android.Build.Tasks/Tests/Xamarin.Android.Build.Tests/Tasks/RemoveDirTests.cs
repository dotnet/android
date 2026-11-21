using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class RemoveDirTests : BaseTest
	{
		List<BuildMessageEventArgs> messages;
		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			messages = new List<BuildMessageEventArgs> ();
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		string NewFile (string fileName = "")
		{
			if (string.IsNullOrEmpty (fileName)) {
				fileName = Path.GetRandomFileName ();
			}
			var path = Path.Combine (tempDirectory, fileName);
			if (IsWindows && path.Length >= Files.MaxPath) {
				File.WriteAllText (Files.ToLongPath (path), contents: "");
			} else {
				File.WriteAllText (path, contents: "");
			}
			return path;
		}

		RemoveDirFixed CreateTask () => new RemoveDirFixed {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
			Directories = new [] { new TaskItem (tempDirectory) },
		};

		RemoveDirFixed CreateTaskWithRetry (int retryAttempts, int retryDelayMs) => new RemoveDirFixed {
			BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
			Directories = new [] { new TaskItem (tempDirectory) },
			RetryAttempts = retryAttempts,
			RetryDelayMs = retryDelayMs,
		};

		[Test]
		public void NormalDelete ()
		{
			NewFile ();
			var task = CreateTask ();
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			Assert.AreEqual (tempDirectory, task.RemovedDirectories [0].ItemSpec);
			Assert.IsEmpty (messages, "RemovedDirectories should not be dumped to the log.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void NoExist ()
		{
			Directory.Delete (tempDirectory);
			var task = CreateTask ();
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (0, task.RemovedDirectories.Length, "No changes should have been made.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void ReadonlyFile ()
		{
			var file = NewFile ();
			File.SetAttributes (file, FileAttributes.ReadOnly);
			var task = CreateTask ();
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void LongPath ()
		{
			var file = NewFile (fileName: "foo".PadRight (MaxFileName, 'N'));
			var task = CreateTask ();
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void LongPathWithTrailingDirectorySeparator ()
		{
			if (!IsWindows) {
				Assert.Ignore ("MAX_PATH only applies on Windows.");
				return;
			}

			NewFile (fileName: "foo".PadRight (MaxFileName, 'N'));
			var deletedPaths = new List<string> ();
			var task = CreateTask ();
			task.Directories = new [] { new TaskItem (tempDirectory + Path.DirectorySeparatorChar) };
			task.RetryAttempts = 2;
			task.RetryDelayMs = 0;
			task.DeleteDirectory = (path, recursive) => {
				deletedPaths.Add (path);
				if (deletedPaths.Count <= 2)
					throw new DirectoryNotFoundException ("Simulated MAX_PATH failure.");
				Directory.Delete (path, recursive);
			};

			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			Assert.AreEqual (3, deletedPaths.Count, "Delete should have been attempted three times.");
			Assert.AreEqual (Files.ToLongPath (tempDirectory), deletedPaths [1], "Long path should not have a trailing directory separator.");
			Assert.AreEqual (deletedPaths [1], deletedPaths [2], "Long path should not be prefixed more than once.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test, Category ("SmokeTests")]
		public void DirectoryInUse ()
		{
			if (OS.IsMac) {
				Assert.Ignore ("This is not an issue on macos.");
				return;
			}
			var file = NewFile ();
			var task = CreateTask ();
			using (var f = File.OpenWrite (file)) {
				Assert.IsFalse (task.Execute (), "task.Execute() should have failed.");
				Assert.AreEqual (0, task.RemovedDirectories.Length, "Changes should not have been made.");
				DirectoryAssert.Exists (tempDirectory);
			}
		}

		[Test, Category ("SmokeTests")]
		public void DirectoryInUseWithRetry ()
		{
			var task = CreateTask ();
			task.RetryDelayMs = 0;
			AssertRetriesLockedDirectory (task);
		}

		[Test]
		public void CustomRetryAttempts ()
		{
			NewFile ();
			var task = CreateTaskWithRetry (retryAttempts: 3, retryDelayMs: 100);
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void DefaultRetryBehavior ()
		{
			NewFile ();
			var task = CreateTask ();
			// Verify default behavior still works
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
			DirectoryAssert.DoesNotExist (tempDirectory);
		}

		[Test]
		public void DirectoryInUseWithCustomRetry ()
		{
			AssertRetriesLockedDirectory (CreateTaskWithRetry (retryAttempts: 3, retryDelayMs: 0));
		}

		[Test]
		public void DirectoryInUseWithNoRetry ()
		{
			AssertRetriesLockedDirectory (CreateTaskWithRetry (retryAttempts: 0, retryDelayMs: 0));
		}

		void AssertRetriesLockedDirectory (RemoveDirFixed task)
		{
			var file = NewFile ();
			using var locked = File.OpenWrite (file);
			int retryAttempts = task.RetryAttempts >= 0 ? task.RetryAttempts : Files.GetFileWriteRetryAttempts ();
			int attempts = 0;
			task.DeleteDirectory = (path, recursive) => {
				attempts++;
				if (attempts <= retryAttempts || retryAttempts == 0) {
					try {
						if (!IsWindows)
							throw new IOException ("Simulated locked directory on Unix.", unchecked ((int) 0x80070020));
						Directory.Delete (path, recursive);
						Assert.Fail ("Deleting a directory containing a locked file should fail.");
					} catch (IOException) {
						if (attempts == retryAttempts)
							locked.Dispose ();
						throw;
					}
				}
				Directory.Delete (path, recursive);
			};
			bool deleted = task.Execute ();
			Assert.AreEqual (retryAttempts + 1, attempts, "Deletion should use exactly the configured number of retries.");
			if (retryAttempts == 0) {
				Assert.IsFalse (deleted, "Deleting a locked directory without retries should fail.");
				Assert.AreEqual (0, task.RemovedDirectories.Length, "No directories should have been removed.");
				DirectoryAssert.Exists (tempDirectory);
				locked.Dispose ();
				Directory.Delete (tempDirectory, recursive: true);
			} else {
				Assert.IsTrue (deleted, "task.Execute() should have succeeded.");
				Assert.AreEqual (1, task.RemovedDirectories.Length, "Changes should have been made.");
				DirectoryAssert.DoesNotExist (tempDirectory);
			}
		}
	}
}
