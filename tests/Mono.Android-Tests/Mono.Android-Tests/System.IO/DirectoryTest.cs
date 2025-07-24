using System;
using System.IO;

using NUnit.Framework;

namespace System.IOTests;

[TestFixture]
public class DirectoryTest
{
	[Test]
	public void GetFiles ()
	{
		var directory = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
		Assert.IsTrue (Directory.Exists (directory), "Directory does not exist: " + directory);

		// Write a file
		File.WriteAllText (Path.Combine (directory, "testfile.txt"), "This is a test file.");

		// Verify that GetFiles returns the file
		string [] files = Directory.GetFiles (directory);
		Assert.IsNotNull (files, "GetFiles returned null for directory: " + directory);
		Assert.IsTrue (files.Length > 0, "No files found in directory: " + directory);
	}
}
