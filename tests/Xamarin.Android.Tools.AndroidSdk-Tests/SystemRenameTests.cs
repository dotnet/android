// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests
{
	[TestClass]
	public class SystemRenameTests
	{
		string tempDir = "";

		[TestInitialize]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"SystemRenameTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
		}

		[TestCleanup]
		public void TearDown ()
		{
			Directory.Delete (tempDir, recursive: true);
		}

		[TestMethod]
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

		[TestMethod]
		public void MissingSourcePreservesDestination ()
		{
			if (OS.IsWindows)
				Assert.Inconclusive ("Tests Unix rename errno reporting.");

			var source = Path.Combine (tempDir, "missing-source");
			var destination = Path.Combine (tempDir, "destination");
			File.WriteAllText (destination, "old contents");

			var error = Assert.ThrowsExactly<IOException> (() => FileUtil.SystemRename (source, destination));

			Assert.AreEqual ("old contents", File.ReadAllText (destination));
			if (error is null)
				throw new InvalidOperationException ("Expected a rename failure.");

			var nativeError = error.InnerException as Win32Exception;
			if (nativeError is null)
				throw new InvalidOperationException ("Expected the captured native error.");

			Assert.AreEqual (2, nativeError.NativeErrorCode); // ENOENT on Linux and macOS.
			StringAssert.Contains (error.Message, "rename");
			StringAssert.Contains (error.Message, source);
			StringAssert.Contains (error.Message, destination);
			StringAssert.Contains (error.Message, "errno 2");
		}
	}
}
