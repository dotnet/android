// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class ProcessUtilsTests
	{
		[Test]
		public void CreateProcessStartInfo_SetsFileName ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual ("myapp", psi.FileName);
		}

		[Test]
		public void CreateProcessStartInfo_SetsShellAndWindow ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.IsFalse (psi.UseShellExecute, "UseShellExecute should be false");
			Assert.IsTrue (psi.CreateNoWindow, "CreateNoWindow should be true");
		}

		[Test]
		public void CreateProcessStartInfo_NoArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual (0, psi.ArgumentList.Count);
		}

		[Test]
		public void CreateProcessStartInfo_SingleArg ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp", "--version");
			Assert.AreEqual (1, psi.ArgumentList.Count);
			Assert.AreEqual ("--version", psi.ArgumentList [0]);
		}

		[Test]
		public void CreateProcessStartInfo_MultipleArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("tar", "-xzf", "archive.tar.gz", "-C", "/tmp/output");
			Assert.AreEqual (4, psi.ArgumentList.Count);
			Assert.AreEqual ("-xzf", psi.ArgumentList [0]);
			Assert.AreEqual ("archive.tar.gz", psi.ArgumentList [1]);
			Assert.AreEqual ("-C", psi.ArgumentList [2]);
			Assert.AreEqual ("/tmp/output", psi.ArgumentList [3]);
		}

		[Test]
		public void CreateProcessStartInfo_ArgWithSpaces ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("cmd", "/c", "path with spaces");
			Assert.AreEqual (2, psi.ArgumentList.Count);
			Assert.AreEqual ("path with spaces", psi.ArgumentList [1]);
		}

		[Test]
		public void IsElevated_DoesNotThrow ()
		{
			// Smoke test: just verify it returns without crashing
			bool result = ProcessUtils.IsElevated ();
			Assert.That (result, Is.TypeOf<bool> ());
		}
	}
}
