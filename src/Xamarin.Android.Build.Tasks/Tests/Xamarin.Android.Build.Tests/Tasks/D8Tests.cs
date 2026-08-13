using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class D8Tests
	{
		MockBuildEngine engine;
		List<BuildMessageEventArgs> messages;
		string tempDir;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				messages: messages = new List<BuildMessageEventArgs> ());
			tempDir = Path.Combine (Path.GetTempPath (), "D8Tests_" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (tempDir);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir)) {
				Directory.Delete (tempDir, recursive: true);
			}
		}

		/// <summary>
		/// Tests that the D8 task creates a response file with the expected content.
		/// This test uses a test subclass to avoid actually running Java.
		/// </summary>
		[Test]
		public void ResponseFileContainsLibAndInputJars ()
		{
			// Create mock jar files for testing
			var platformJar = Path.Combine (tempDir, "android.jar");
			var inputJar1 = Path.Combine (tempDir, "input1.jar");
			var inputJar2 = Path.Combine (tempDir, "input2.jar");
			var libJar1 = Path.Combine (tempDir, "lib1.jar");
			File.WriteAllText (platformJar, "mock");
			File.WriteAllText (inputJar1, "mock");
			File.WriteAllText (inputJar2, "mock");
			File.WriteAllText (libJar1, "mock");

			var d8Task = new D8TestTask {
				BuildEngine = engine,
				JarPath = "d8.jar",
				JavaPlatformJarPath = platformJar,
				OutputDirectory = tempDir,
				JavaLibrariesToEmbed = new ITaskItem [] {
					new TaskItem (inputJar1),
					new TaskItem (inputJar2),
				},
				JavaLibrariesToReference = new ITaskItem [] {
					new TaskItem (libJar1),
				},
			};

			string commandLine = d8Task.TestGenerateCommandLineCommands ();
			string responseFilePath = d8Task.ResponseFilePath;

			try {
				// Verify response file was created
				Assert.IsNotNull (responseFilePath, "Response file path should not be null");
				FileAssert.Exists (responseFilePath, "Response file should exist");

				// Verify the response file is referenced in the command line
				Assert.IsTrue (commandLine.Contains ($"\"@{responseFilePath}\""), "Command line should quote the response file");

				// Read and verify response file content
				string [] responseFileContent = File.ReadAllLines (responseFilePath);

				// Should contain --lib entries (written as separate lines: --lib on one line, path on next)
				Assert.IsTrue (responseFileContent.Any (line => line == "--lib"), "Response file should contain --lib switch");
				Assert.IsTrue (responseFileContent.Any (line => line.Contains ("android.jar")), "Response file should contain android.jar");
				Assert.IsTrue (responseFileContent.Any (line => line.Contains ("lib1.jar")), "Response file should contain lib1.jar");

				// Should contain input jars as direct arguments (no --lib prefix)
				Assert.IsTrue (responseFileContent.Any (line => line.Contains ("input1.jar")), "Response file should contain input1.jar");
				Assert.IsTrue (responseFileContent.Any (line => line.Contains ("input2.jar")), "Response file should contain input2.jar");

			} finally {
				// Clean up response file
				if (responseFilePath != null && File.Exists (responseFilePath)) {
					File.Delete (responseFilePath);
				}
			}
		}

		/// <summary>
		/// Tests that paths with spaces are written correctly to the response file.
		/// R8/D8 response files treat each line as a complete argument, so no quoting is needed.
		/// </summary>
		[Test]
		public void ResponseFileHandlesPathsWithSpaces ()
		{
			var pathWithSpaces = Path.Combine (tempDir, "path with spaces");
			Directory.CreateDirectory (pathWithSpaces);

			// Create mock jar files for testing
			var platformJar = Path.Combine (pathWithSpaces, "android.jar");
			var inputJar = Path.Combine (pathWithSpaces, "input.jar");
			File.WriteAllText (platformJar, "mock");
			File.WriteAllText (inputJar, "mock");

			var d8Task = new D8TestTask {
				BuildEngine = engine,
				JarPath = "d8.jar",
				JavaPlatformJarPath = platformJar,
				OutputDirectory = tempDir,
				ResponseFileDirectory = pathWithSpaces,
				JavaLibrariesToEmbed = new ITaskItem [] {
					new TaskItem (inputJar),
				},
			};

			string commandLine = d8Task.TestGenerateCommandLineCommands ();
			string responseFilePath = d8Task.ResponseFilePath;

			try {
				FileAssert.Exists (responseFilePath, "Response file should exist");
				Assert.IsTrue (commandLine.Contains ($"\"@{responseFilePath}\""), "Command line should quote a response file path containing spaces");
				string responseFileContent = File.ReadAllText (responseFilePath);

				// Paths with spaces should NOT be quoted (R8/D8 treats each line as a complete argument)
				Assert.IsFalse (responseFileContent.Contains ("\""), "Response file should not contain quoted paths");
				Assert.IsTrue (responseFileContent.Contains ("path with spaces"), "Response file should contain the path with spaces");

			} finally {
				if (responseFilePath != null && File.Exists (responseFilePath)) {
					File.Delete (responseFilePath);
				}
			}
		}
	}

	/// <summary>
	/// Test subclass of D8 that exposes internal methods for testing without needing Java.
	/// </summary>
	internal class D8TestTask : D8
	{
		/// <summary>
		/// The path to the response file created by the last call to TestGenerateCommandLineCommands.
		/// </summary>
		public string ResponseFilePath { get; private set; }

		public string ResponseFileDirectory { get; set; }

		protected override string CreateResponseFilePath ()
		{
			if (!String.IsNullOrEmpty (ResponseFileDirectory)) {
				return Path.Combine (ResponseFileDirectory, Path.GetRandomFileName ());
			}
			return base.CreateResponseFilePath ();
		}

		protected override string CreateResponseFile ()
		{
			var responseFile = base.CreateResponseFile ();
			ResponseFilePath = responseFile;
			return responseFile;
		}

		/// <summary>
		/// Test method that generates command line without actually running the task.
		/// </summary>
		public string TestGenerateCommandLineCommands ()
		{
			var cmd = GetCommandLineBuilder ();
			return cmd.ToString ();
		}
	}
}
