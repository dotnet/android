// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class SystemRenameTests
	{
		string tempDir = "";

		[SetUp]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"SystemRenameTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDir, recursive: true);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ReplacesDestination (bool existingDestination)
		{
			var source = Path.Combine (tempDir, "source");
			var destination = Path.Combine (tempDir, "destination");
			File.WriteAllText (source, "new contents");
			if (existingDestination)
				File.WriteAllText (destination, "old contents");

			FileUtil.SystemRename (source, destination);

			Assert.AreEqual ("new contents", File.ReadAllText (destination));
			Assert.IsFalse (File.Exists (source));
		}

		[Test]
		public void MissingSourcePreservesDestination ()
		{
			if (OS.IsWindows)
				Assert.Ignore ("Tests Unix rename errno reporting.");

			var source = Path.Combine (tempDir, "missing-source");
			var destination = Path.Combine (tempDir, "destination");
			File.WriteAllText (destination, "old contents");

			var error = Assert.Throws<IOException> (() => FileUtil.SystemRename (source, destination));

			AssertRenameError (error, source, destination, 2); // ENOENT on Linux and macOS.
			Assert.AreEqual ("old contents", File.ReadAllText (destination));
		}

		[Test]
		public void DirectorySourcePreservesDestination ()
		{
			if (OS.IsWindows)
				Assert.Ignore ("Tests Unix rename failure without a permissions dependency.");

			var source = Path.Combine (tempDir, "source");
			var destination = Path.Combine (tempDir, "destination");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "contents"), "source contents");
			File.WriteAllText (destination, "old contents");

			var error = Assert.Throws<IOException> (() => FileUtil.SystemRename (source, destination));

			AssertRenameError (error, source, destination);
			Assert.AreEqual ("old contents", File.ReadAllText (destination));
			Assert.AreEqual ("source contents", File.ReadAllText (Path.Combine (source, "contents")));
		}

		[Test]
		public void ManifestWriteFailurePreservesDestinationAndDeletesTemporaryFile ()
		{
			if (OS.IsWindows)
				Assert.Ignore ("Tests manifest cleanup after a Unix rename failure.");

			var destination = Path.Combine (tempDir, "AndroidManifest.xml");
			Directory.CreateDirectory (destination);
			var original = Path.Combine (destination, "contents");
			File.WriteAllText (original, "old contents");
			var manifest = AndroidAppManifest.Create ("com.example.test", "Test", new AndroidVersions (new AndroidVersion [0]));

			var error = Assert.Throws<IOException> (() => manifest.WriteToFile (destination));

			AssertRenameError (error, FileUtil.GetTempFilenameForWrite (destination), destination);
			Assert.AreEqual ("old contents", File.ReadAllText (original));
			Assert.IsFalse (File.Exists (FileUtil.GetTempFilenameForWrite (destination)));
		}

		[Test]
		public void ManifestWriteReplacesDestination ()
		{
			var destination = Path.Combine (tempDir, "AndroidManifest.xml");
			File.WriteAllText (destination, "old contents");
			var versions = new AndroidVersions (new AndroidVersion [0]);
			var manifest = AndroidAppManifest.Create ("com.example.test", "Test", versions);

			manifest.WriteToFile (destination);

			var written = AndroidAppManifest.Load (destination, versions);
			Assert.AreEqual (manifest.PackageName, written.PackageName);
			Assert.AreEqual (manifest.ApplicationLabel, written.ApplicationLabel);
			Assert.IsFalse (File.Exists (FileUtil.GetTempFilenameForWrite (destination)));
		}

		static void AssertRenameError (IOException error, string source, string destination, int? expectedErrno = null)
		{
			Assert.IsNotNull (error);
			if (error is null)
				throw new InvalidOperationException ("Expected a rename failure.");

			var nativeError = error.InnerException as Win32Exception;
			Assert.IsNotNull (nativeError);
			if (nativeError is null)
				throw new InvalidOperationException ("Expected the captured native error.");

			Assert.Greater (nativeError.NativeErrorCode, 0);
			if (expectedErrno.HasValue)
				Assert.AreEqual (expectedErrno.Value, nativeError.NativeErrorCode);
			StringAssert.Contains ("rename", error.Message);
			StringAssert.Contains (source, error.Message);
			StringAssert.Contains (destination, error.Message);
			StringAssert.Contains ($"errno {nativeError.NativeErrorCode}", error.Message);
			StringAssert.Contains (nativeError.Message, error.Message);
		}
	}
}
