// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests
{
	[TestClass]
	public class FileUtilTests
	{
		string tempDir = null!;
		Action<TraceLevel, string> logger = null!;

		[TestInitialize]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"FileUtilTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
			logger = (level, msg) => Console.WriteLine ($"[{level}] {msg}");
		}

		[TestCleanup]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}

		[TestMethod]
		public void MoveWithRollback_NewTarget_Succeeds ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "file.txt"), "hello");

			FileUtil.MoveWithRollback (source, target, logger);

			Assert.IsFalse (Directory.Exists (source), "Source should no longer exist");
			Assert.IsTrue (Directory.Exists (target), "Target should exist");
			Assert.AreEqual ("hello", File.ReadAllText (Path.Combine (target, "file.txt")));
		}

		[TestMethod]
		public void MoveWithRollback_ExistingTarget_BacksUpAndReplaces ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "new.txt"), "new");

			Directory.CreateDirectory (target);
			File.WriteAllText (Path.Combine (target, "old.txt"), "old");

			FileUtil.MoveWithRollback (source, target, logger);

			Assert.IsFalse (Directory.Exists (source), "Source should no longer exist");
			Assert.IsTrue (File.Exists (Path.Combine (target, "new.txt")), "New file should exist");
			Assert.IsFalse (File.Exists (Path.Combine (target, "old.txt")), "Old file should be gone");
		}

		[TestMethod]
		public void MoveWithRollback_SourceDoesNotExist_RestoresBackup ()
		{
			var source = Path.Combine (tempDir, "nonexistent");
			var target = Path.Combine (tempDir, "target");

			// Create an existing target that should be backed up and restored
			Directory.CreateDirectory (target);
			File.WriteAllText (Path.Combine (target, "original.txt"), "preserve me");

			Assert.ThrowsExactly<DirectoryNotFoundException> (() => FileUtil.MoveWithRollback (source, target, logger));

			// The original target should be restored from backup
			Assert.IsTrue (Directory.Exists (target), "Target should be restored");
			Assert.AreEqual ("preserve me", File.ReadAllText (Path.Combine (target, "original.txt")));
		}

		[TestMethod]
		public void MoveWithRollback_SourceDoesNotExist_NoExistingTarget_Throws ()
		{
			var source = Path.Combine (tempDir, "nonexistent");
			var target = Path.Combine (tempDir, "also-nonexistent");

			Assert.ThrowsExactly<DirectoryNotFoundException> (() => FileUtil.MoveWithRollback (source, target, logger));
			Assert.IsFalse (Directory.Exists (target));
		}

		[TestMethod]
		public void MoveWithRollback_CancelledAfterValidation_RestoresBackup ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "new.txt"), "new");
			Directory.CreateDirectory (target);
			File.WriteAllText (Path.Combine (target, "old.txt"), "old");
			using var cts = new CancellationTokenSource ();
			Action validate = () => {
				Assert.IsTrue (File.Exists (Path.Combine (target, "new.txt")));
				var backups = Directory.GetDirectories (tempDir, "target.old-*");
				Assert.AreEqual (1, backups.Length, "Backup must survive through validation");
				Assert.AreEqual ("old", File.ReadAllText (Path.Combine (backups [0], "old.txt")));
				cts.Cancel ();
			};

			Assert.ThrowsExactly<OperationCanceledException> (() => FileUtil.MoveWithRollback (source, target, logger, validate, cts.Token));

			Assert.AreEqual ("old", File.ReadAllText (Path.Combine (target, "old.txt")));
			Assert.IsFalse (File.Exists (Path.Combine (target, "new.txt")));
			Assert.IsEmpty (Directory.GetDirectories (tempDir, "target.old-*"));
		}

		[TestMethod]
		public void MoveWithRollback_RollbackFailure_PreservesBackupAndBothErrors ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			Directory.CreateDirectory (source);
			Directory.CreateDirectory (target);
			File.WriteAllText (Path.Combine (target, "old.txt"), "old");
			var validationError = new InvalidOperationException ("Validation failed");

			var error = Assert.ThrowsExactly<AggregateException> (() => FileUtil.MoveWithRollback (source, target, logger, validate: () => {
				Directory.Delete (target);
				File.WriteAllText (target, "blocking file");
				throw validationError;
			}));

			Assert.IsNotNull (error);
			if (error is null)
				throw new InvalidOperationException ("Expected rollback failure");
			Assert.AreEqual (2, error.InnerExceptions.Count);
			Assert.AreSame (validationError, error.InnerExceptions [0]);
			var backups = Directory.GetDirectories (tempDir, "target.old-*");
			Assert.AreEqual (1, backups.Length);
			Assert.AreEqual ("old", File.ReadAllText (Path.Combine (backups [0], "old.txt")));
			Assert.AreEqual ("blocking file", File.ReadAllText (target));
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void SystemRename_Success (bool existingTarget)
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			File.WriteAllText (source, "replacement");
			if (existingTarget)
				File.WriteAllText (target, "original");

			FileUtil.SystemRename (source, target);

			Assert.IsFalse (File.Exists (source));
			Assert.AreEqual ("replacement", File.ReadAllText (target));
		}

		[TestMethod]
		public void SystemRename_DirectoryTarget_ThrowsAndPreservesSource ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			File.WriteAllText (source, "preserve");
			Directory.CreateDirectory (target);

			var error = Assert.ThrowsExactly<IOException> (() => FileUtil.SystemRename (source, target));

			Assert.AreEqual ("preserve", File.ReadAllText (source));
			Assert.IsTrue (Directory.Exists (target));
			if (!OS.IsWindows) {
				Assert.IsNotNull (error);
				Assert.Contains (source, error.Message);
				Assert.Contains (target, error.Message);
				Assert.Contains ("errno", error.Message);
				Assert.AreEqual (typeof (Win32Exception), ((object) error.InnerException)?.GetType ());
				Assert.AreNotEqual (0, ((Win32Exception) error.InnerException).NativeErrorCode);
			}
		}

		[TestMethod]
		public void IsUnderDirectory_ChildPath_ReturnsTrue ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var child = Path.Combine (parent, "java", "jdk-21");
			Assert.IsTrue (FileUtil.IsUnderDirectory (child, parent));
		}

		[TestMethod]
		public void IsUnderDirectory_ExactMatch_ReturnsTrue ()
		{
			var directory = Path.Combine (tempDir, "programs");
			Assert.IsTrue (FileUtil.IsUnderDirectory (directory, directory));
		}

		[TestMethod]
		public void IsUnderDirectory_SiblingPath_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine (tempDir, "data", "java"),
				Path.Combine (tempDir, "programs")));
		}

		[TestMethod]
		public void IsUnderDirectory_DifferentRoot_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine (Path.GetPathRoot (tempDir), "other", "java"),
				Path.Combine (tempDir, "programs")));
		}

		[TestMethod]
		public void IsUnderDirectory_PartialDirNameMatch_ReturnsFalse ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var sibling = Path.Combine (tempDir, "programs-extra", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (sibling, parent));
		}

		[TestMethod]
		[DataRow (null, "/dir")]
		[DataRow ("/dir", null)]
		[DataRow ("", "/dir")]
		[DataRow ("/dir", "")]
		[DataRow (null, null)]
		public void IsUnderDirectory_NullOrEmpty_ReturnsFalse (string path, string directory)
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (path, directory));
		}

		[TestMethod]
		public void IsUnderDirectory_CaseSensitive ()
		{
			var parent = Path.Combine (tempDir, "Programs");
			var child = Path.Combine (tempDir, "PROGRAMS", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (child, parent));
		}

		[TestMethod]
		public void IsUnderDirectory_CanonicalCaseMismatch_ReturnsFalse ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var sibling = Path.Combine (parent, "..", "PROGRAMS", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (sibling, parent));
		}

	}
}
