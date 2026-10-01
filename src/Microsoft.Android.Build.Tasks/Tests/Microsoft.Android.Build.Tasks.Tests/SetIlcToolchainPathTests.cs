using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[NonParallelizable]
public class SetIlcToolchainPathTests : BaseTest
{
	[Test]
	public void NdkToolsPrecedeOtherToolsOnPath ()
	{
		string binDirectory = Path.Combine (Root, "temp", TestName, "ndk", "bin");
		Directory.CreateDirectory (binDirectory);
		string? originalPath = Environment.GetEnvironmentVariable ("PATH");
		try {
			var task = new SetIlcToolchainPath {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ToolchainBinDirectory = binDirectory,
			};
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (Path.GetFullPath (binDirectory) + Path.PathSeparator + originalPath, Environment.GetEnvironmentVariable ("PATH"));
		} finally {
			Environment.SetEnvironmentVariable ("PATH", originalPath);
		}
	}

	[Test]
	public void MissingDirectoryDoesNotChangePath ()
	{
		string? originalPath = Environment.GetEnvironmentVariable ("PATH");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new SetIlcToolchainPath {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			ToolchainBinDirectory = Path.Combine (Root, "temp", TestName, "missing"),
		};
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA5101", errors [0].Code);
		Assert.AreEqual (originalPath, Environment.GetEnvironmentVariable ("PATH"));
	}
}
