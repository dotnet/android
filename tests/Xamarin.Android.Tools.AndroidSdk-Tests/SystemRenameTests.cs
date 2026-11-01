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

		[Test]
		public void ReplacesDestination ()
		{
			var source = Path.Combine (tempDir, "source");
			var destination = Path.Combine (tempDir, "destination");
			File.WriteAllText (source, "new contents");
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

			Assert.AreEqual ("old contents", File.ReadAllText (destination));
			if (error is null)
				throw new InvalidOperationException ("Expected a rename failure.");

			var nativeError = error.InnerException as Win32Exception;
			if (nativeError is null)
				throw new InvalidOperationException ("Expected the captured native error.");

			Assert.AreEqual (2, nativeError.NativeErrorCode); // ENOENT on Linux and macOS.
			StringAssert.Contains ("rename", error.Message);
			StringAssert.Contains (source, error.Message);
			StringAssert.Contains (destination, error.Message);
			StringAssert.Contains ("errno 2", error.Message);
		}
	}
}
