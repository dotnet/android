// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq;

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
			AssertArguments (psi);
		}

		[Test]
		public void CreateProcessStartInfo_SingleArg ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp", "--version");
			AssertArguments (psi, "--version");
		}

		[Test]
		public void CreateProcessStartInfo_MultipleArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("tar", "-xzf", "archive.tar.gz", "-C", "/tmp/output");
			AssertArguments (psi, "-xzf", "archive.tar.gz", "-C", "/tmp/output");
		}

		[Test]
		public void CreateProcessStartInfo_ArgWithSpaces ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("cmd", "/c", "path with spaces");
			AssertArguments (psi, "/c", "path with spaces");
		}

		[Test]
		public void IsElevated_DoesNotThrow ()
		{
			// Smoke test: just verify it returns without crashing
			bool result = ProcessUtils.IsElevated ();
			Assert.That (result, Is.TypeOf<bool> ());
		}

		static void AssertArguments (ProcessStartInfo psi, params string [] expected)
		{
			if (psi.ArgumentList.Count > 0) {
				Assert.That (psi.ArgumentList, Is.EqualTo (expected));
				Assert.That (psi.Arguments, Is.Empty);
			} else {
				Assert.That (psi.Arguments, Is.EqualTo (string.Join (" ", expected.Select (argument => $"\"{argument}\""))));
			}
		}
	}
}
