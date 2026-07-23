// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
		public void IsUnderDirectory_ChildPath_ReturnsTrue ()
		{
			var parent = Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs");
			var child = Path.Combine (parent, "java", "jdk-21");
			Assert.IsTrue (FileUtil.IsUnderDirectory (child, parent));
		}

		[Test]
		public void IsUnderDirectory_ExactMatch_ReturnsTrue ()
		{
			var dir = Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs");
			Assert.IsTrue (FileUtil.IsUnderDirectory (dir, dir));
		}

		[Test]
		public void IsUnderDirectory_SiblingPath_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine ($"{Path.DirectorySeparatorChar}opt", "data", "java"),
				Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs")));
		}

		[Test]
		public void IsUnderDirectory_DifferentRoot_ReturnsFalse ()
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine ($"{Path.DirectorySeparatorChar}other", "java"),
				Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs")));
		}

		[TestCase (null, "/dir")]
		[TestCase ("/dir", null)]
		[TestCase ("", "/dir")]
		[TestCase ("/dir", "")]
		[TestCase (null, null)]
		public void IsUnderDirectory_NullOrEmpty_ReturnsFalse (string path, string directory)
		{
			Assert.IsFalse (FileUtil.IsUnderDirectory (path!, directory!));
		}

		[Test]
		public void IsUnderDirectory_CaseInsensitive ()
		{
			var parent = Path.Combine ($"{Path.DirectorySeparatorChar}opt", "Programs");
			var child = Path.Combine ($"{Path.DirectorySeparatorChar}opt", "PROGRAMS", "java");
			Assert.IsTrue (FileUtil.IsUnderDirectory (child, parent));
		}

		[Test]
		public void IsUnderDirectory_PartialDirNameMatch_ReturnsFalse ()
		{
			var parent = Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs");
			Assert.IsFalse (FileUtil.IsUnderDirectory (
				Path.Combine ($"{Path.DirectorySeparatorChar}opt", "programs-extra", "java"),
				parent));
		}
	}
}
