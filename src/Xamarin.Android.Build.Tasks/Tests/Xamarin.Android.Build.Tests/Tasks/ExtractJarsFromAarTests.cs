using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class ExtractJarsFromAarTests : BaseTest
	{
		List<BuildErrorEventArgs>? errors;
		List<BuildMessageEventArgs>? messages;
		MockBuildEngine? engine;
		string? path;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());

			path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
			Directory.CreateDirectory (path);
		}

		[Test]
		public void PathTraversalInJarEntry ()
		{
			var aarPath = CreateAarWithEntry ("../../relative.jar");
			var jarOutputDir = Path.Combine (path, "jars");
			var annotationOutputDir = Path.Combine (path, "annotations");
			Directory.CreateDirectory (jarOutputDir);
			Directory.CreateDirectory (annotationOutputDir);

			var task = new ExtractJarsFromAar {
				BuildEngine = engine,
				OutputJarsDirectory = jarOutputDir,
				OutputAnnotationsDirectory = annotationOutputDir,
				Libraries = [aarPath],
			};

			Assert.IsTrue (task.Execute (), "Task should succeed, skipping the traversal entry.");
			Assert.IsEmpty (errors, "No errors should be logged.");

			// Verify no file was written outside the target directory
			var escapedPath = Path.GetFullPath (Path.Combine (path, "relative.jar"));
			Assert.IsFalse (File.Exists (escapedPath), "File should not be written outside target directory.");
		}

		[Test]
		public void PathTraversalInAnnotationsEntry ()
		{
			var aarPath = CreateAarWithEntry ("../../annotations.zip");
			var jarOutputDir = Path.Combine (path, "jars");
			var annotationOutputDir = Path.Combine (path, "annotations");
			Directory.CreateDirectory (jarOutputDir);
			Directory.CreateDirectory (annotationOutputDir);

			var task = new ExtractJarsFromAar {
				BuildEngine = engine,
				OutputJarsDirectory = jarOutputDir,
				OutputAnnotationsDirectory = annotationOutputDir,
				Libraries = [aarPath],
			};

			Assert.IsTrue (task.Execute (), "Task should succeed, skipping the traversal entry.");
			Assert.IsEmpty (errors, "No errors should be logged.");
		}

		[Test]
		public void ValidJarEntry ()
		{
			var aarPath = CreateAarWithEntry ("libs/helper.jar");
			var jarOutputDir = Path.Combine (path, "jars");
			var annotationOutputDir = Path.Combine (path, "annotations");
			Directory.CreateDirectory (jarOutputDir);
			Directory.CreateDirectory (annotationOutputDir);

			var task = new ExtractJarsFromAar {
				BuildEngine = engine,
				OutputJarsDirectory = jarOutputDir,
				OutputAnnotationsDirectory = annotationOutputDir,
				Libraries = [aarPath],
			};

			Assert.IsTrue (task.Execute (), "Task should succeed for valid entry.");
			Assert.IsEmpty (errors, "No errors should be logged.");
		}

		string CreateAarWithEntry (string entryName)
		{
			var aarPath = Path.Combine (path, "test.aar");
			using (var stream = new FileStream (aarPath, FileMode.Create))
			using (var archive = new ZipArchive (stream, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry (entryName);
				using (var entryStream = entry.Open ())
				using (var writer = new StreamWriter (entryStream)) {
					writer.Write ("dummy content");
				}
			}
			return aarPath;
		}
	}
}
