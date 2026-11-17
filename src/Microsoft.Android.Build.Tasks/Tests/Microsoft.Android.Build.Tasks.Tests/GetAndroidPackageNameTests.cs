using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests {

	[TestClass]
	public class GetAndroidPackageNameTests {

		[TestMethod]
		[DataRow (false, true, "com.application.id", "com.example.some_name")]
		[DataRow (true, true, null, "com.example.some_name")]
		[DataRow (false, false, null, "Example.Example")]
		[DataRow (true, false, "com.application.id", "com.application.id")]
		public void ManifestPaths (bool absolutePath, bool exists, string? packageName, string expected)
		{
			string directory = CreateProjectDirectory ();
			try {
				string path = Path.Combine (directory, "AndroidManifest.xml");
				if (exists) {
					File.WriteAllText (path, "<manifest package=\"${prefix}.some-name\" />");
				}
				string manifestFile = absolutePath ? path : "AndroidManifest.xml";
				var task = new GetAndroidPackageName {
					BuildEngine = new MockBuildEngine (Console.Out),
					TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
					ManifestFile = manifestFile,
					AssemblyName = "Example",
					PackageName = packageName,
					ManifestPlaceholders = ["prefix=com.example"],
				};

				Assert.IsTrue (task.Execute ());
				Assert.AreEqual (expected, task.PackageName);
				Assert.AreEqual (manifestFile, task.ManifestFile, "Rooting file I/O must not change the public input.");
			} finally {
				Directory.Delete (directory, recursive: true);
			}
		}

		[TestMethod]
		public void ConcurrentProjectDirectories ()
		{
			string root = CreateProjectDirectory ();
			try {
				Parallel.For (0, 2, i => {
					string directory = Path.Combine (root, $"project{i}");
					string package = $"com.project{i}";
					Directory.CreateDirectory (directory);
					File.WriteAllText (Path.Combine (directory, "AndroidManifest.xml"), $"<manifest package=\"{package}\" />");
					var task = new GetAndroidPackageName {
						BuildEngine = new MockBuildEngine (TextWriter.Null),
						TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
						ManifestFile = "AndroidManifest.xml",
						AssemblyName = "Fallback",
					};

					Assert.IsTrue (task.Execute ());
					Assert.AreEqual (package, task.PackageName);
				});
			} finally {
				Directory.Delete (root, recursive: true);
			}
		}

		static string CreateProjectDirectory ()
		{
			string directory = Path.Combine (AppContext.BaseDirectory, "GetAndroidPackageName", Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			Assert.AreNotEqual (Directory.GetCurrentDirectory (), directory);
			return directory;
		}

		[TestMethod]
		[DataRow ("com.example.for", "for")]
		[DataRow ("com.class.example", "class")]
		[DataRow ("com.example.true", "true")]
		public void ReservedJavaIdentifier_FailsWithXA4258 (string packageName, string invalidIdentifier)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAndroidPackageName {
				BuildEngine = new MockBuildEngine (Console.Out, errors),
				AssemblyName = "Example",
				PackageName = packageName,
			};

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual (1, errors.Count (error =>
				error.Code == "XA4258" &&
				error.Message.Contains (packageName) &&
				error.Message.Contains (invalidIdentifier)));
		}

		[TestMethod]
		public void ContextualKeywordInPackage_Succeeds ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var task = new GetAndroidPackageName {
				BuildEngine = new MockBuildEngine (Console.Out, errors, messages: messages),
				AssemblyName = "Example",
				PackageName = "com.example.record",
			};

			Assert.IsTrue (task.Execute ());
			Assert.IsEmpty (errors);
			Assert.AreEqual ("com.example.record", task.PackageName);
			Assert.IsEmpty (messages, "PackageName should not be dumped to the log.");
		}
	}
}
