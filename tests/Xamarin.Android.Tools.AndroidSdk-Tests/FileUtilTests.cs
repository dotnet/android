// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class FileUtilTests
	{
		string tempDir = null!;
		Action<TraceLevel, string> logger = null!;

		[SetUp]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"FileUtilTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
			logger = (level, msg) => TestContext.WriteLine ($"[{level}] {msg}");
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}

		[Test]
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

		[Test]
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

		[Test]
		public void MoveWithRollback_SourceDoesNotExist_RestoresBackup ()
		{
			var source = Path.Combine (tempDir, "nonexistent");
			var target = Path.Combine (tempDir, "target");

			// Create an existing target that should be backed up and restored
			Directory.CreateDirectory (target);
			File.WriteAllText (Path.Combine (target, "original.txt"), "preserve me");

			Assert.Throws<DirectoryNotFoundException> (() => FileUtil.MoveWithRollback (source, target, logger));

			// The original target should be restored from backup
			Assert.IsTrue (Directory.Exists (target), "Target should be restored");
			Assert.AreEqual ("preserve me", File.ReadAllText (Path.Combine (target, "original.txt")));
		}

		[Test]
		public void MoveWithRollback_SourceDoesNotExist_NoExistingTarget_Throws ()
		{
			var source = Path.Combine (tempDir, "nonexistent");
			var target = Path.Combine (tempDir, "also-nonexistent");

			Assert.Throws<DirectoryNotFoundException> (() => FileUtil.MoveWithRollback (source, target, logger));
			Assert.IsFalse (Directory.Exists (target));
		}

		[Test]
		public void MoveWithRollback_ValidationSuccess_RemovesOnlyItsBackup ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			var unrelatedBackup = target + ".old-unrelated";
			Directory.CreateDirectory (source);
			Directory.CreateDirectory (target);
			Directory.CreateDirectory (unrelatedBackup);
			File.WriteAllText (Path.Combine (target, "original.txt"), "original");

			FileUtil.MoveWithRollback (source, target, logger, () => {
				var backups = Directory.GetDirectories (tempDir, "target.old-*");
				Assert.AreEqual (2, backups.Length, "Keep the original until validation succeeds");
			});

			Assert.That (Directory.GetDirectories (tempDir, "target.old-*"), Is.EquivalentTo (new [] { unrelatedBackup }));
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void MoveWithRollback_ValidationFailure_RollsBack (bool existingTarget, bool canceled)
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "replacement.txt"), "replacement");
			if (existingTarget) {
				Directory.CreateDirectory (target);
				File.WriteAllText (Path.Combine (target, "original.txt"), "original");
			}
			Exception failure = canceled ? new OperationCanceledException () : new InvalidOperationException ("Invalid installation");

			var actual = Assert.Catch (() => FileUtil.MoveWithRollback (source, target, logger, () => throw failure));

			Assert.AreSame (failure, actual);
			Assert.AreEqual (existingTarget, Directory.Exists (target));
			if (existingTarget)
				Assert.AreEqual ("original", File.ReadAllText (Path.Combine (target, "original.txt")));
			Assert.IsFalse (File.Exists (Path.Combine (target, "replacement.txt")));
			Assert.IsEmpty (Directory.GetDirectories (tempDir, "target.old-*"));
		}

		[TestCase (false)]
		[TestCase (true)]
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

		[Test]
		public void SystemRename_DirectoryTarget_ThrowsAndPreservesSource ()
		{
			var source = Path.Combine (tempDir, "source");
			var target = Path.Combine (tempDir, "target");
			File.WriteAllText (source, "preserve");
			Directory.CreateDirectory (target);

			var error = Assert.Throws<IOException> (() => FileUtil.SystemRename (source, target));

			Assert.AreEqual ("preserve", File.ReadAllText (source));
			Assert.IsTrue (Directory.Exists (target));
			if (!OS.IsWindows) {
				Assert.IsNotNull (error);
				Assert.That (error.Message, Does.Contain (source).And.Contain (target).And.Contain ("errno"));
				Assert.That (error.InnerException, Is.TypeOf<Win32Exception> ());
				Assert.AreNotEqual (0, ((Win32Exception) error.InnerException).NativeErrorCode);
			}
		}

		[Test]
		public void IsUnderDirectory_ChildPath_ReturnsTrue ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var child = Path.Combine (parent, "java", "jdk-21");
			Assert.IsTrue (FileUtil.IsUnderDirectory (child, parent));
		}

		[Test]
		public void IsUnderDirectory_ExactMatch_ReturnsTrue ()
		{
			var directory = Path.Combine (tempDir, "programs");
			Assert.IsTrue (FileUtil.IsUnderDirectory (directory, directory));
		}

		[Test]
		public void IsUnderDirectory_SiblingPath_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine (tempDir, "data", "java"),
				Path.Combine (tempDir, "programs")));
		}

		[Test]
		public void IsUnderDirectory_DifferentRoot_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine (Path.GetPathRoot (tempDir), "other", "java"),
				Path.Combine (tempDir, "programs")));
		}

		[Test]
		public void IsUnderDirectory_PartialDirNameMatch_ReturnsFalse ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var sibling = Path.Combine (tempDir, "programs-extra", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (sibling, parent));
		}

		[TestCase (null, "/dir")]
		[TestCase ("/dir", null)]
		[TestCase ("", "/dir")]
		[TestCase ("/dir", "")]
		[TestCase (null, null)]
		public void IsUnderDirectory_NullOrEmpty_ReturnsFalse (string path, string directory)
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (path, directory));
		}

		[Test]
		public void IsUnderDirectory_CaseSensitive ()
		{
			var parent = Path.Combine (tempDir, "Programs");
			var child = Path.Combine (tempDir, "PROGRAMS", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (child, parent));
		}

		[Test]
		public void IsUnderDirectory_CanonicalCaseMismatch_ReturnsFalse ()
		{
			var parent = Path.Combine (tempDir, "programs");
			var sibling = Path.Combine (parent, "..", "PROGRAMS", "java");
			Assert.IsFalse (FileUtil.IsUnderDirectory (sibling, parent));
		}

	}
}
