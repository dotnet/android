using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BuildArchiveTests
	{
		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		[Test]
		public void ExistingJavaArchiveEntriesAreUpdated ()
		{
			var apk = Path.Combine (tempDirectory, "app.apk");
			var jar = Path.Combine (tempDirectory, "classes.jar");

			CreateArchive (apk, ("commonMain/default/manifest", "existing"), ("stale.txt", "stale"));
			CreateArchive (jar, ("commonMain/default/manifest", "current"));

			var item = new TaskItem ($"{jar}#commonMain/default/manifest");
			item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
			item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");

			var task = new BuildArchive {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ApkOutputPath = apk,
				FilesToAddToArchive = new ITaskItem [] { item },
			};

			Assert.IsTrue (task.RunTask (), "task should have succeeded");

			using (var archive = ZipArchive.Open (apk, FileMode.Open)) {
				archive.AssertEntryContents (apk, "commonMain/default/manifest", "current");
				archive.AssertDoesNotContainEntry (apk, "stale.txt");
			}
		}

		[Test]
		public void ExistingJavaArchiveEntriesAreSkippedWhenUpToDate ()
		{
			var apk = Path.Combine (tempDirectory, "app.apk");
			var jar = Path.Combine (tempDirectory, "classes.jar");

			CreateArchive (apk, ("commonMain/default/manifest", "current"));
			CreateArchive (jar, ("commonMain/default/manifest", "current"));

			var item = new TaskItem ($"{jar}#commonMain/default/manifest");
			item.SetMetadata ("ArchivePath", "commonMain/default/manifest");
			item.SetMetadata ("JavaArchiveEntry", "commonMain/default/manifest");
			var messages = new List<BuildMessageEventArgs> ();

			var task = new BuildArchive {
				BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
				ApkOutputPath = apk,
				FilesToAddToArchive = new ITaskItem [] { item },
			};

			Assert.IsTrue (task.RunTask (), "task should have succeeded");

			Assert.That (messages, Has.Some.Property (nameof (BuildMessageEventArgs.Message)).EqualTo ($"Skipping commonMain/default/manifest from {jar} as it is up to date."));

			using (var archive = ZipArchive.Open (apk, FileMode.Open)) {
				archive.AssertEntryContents (apk, "commonMain/default/manifest", "current");
			}
		}

		static void CreateArchive (string path, params (string name, string contents) [] entries)
		{
			using (var stream = File.Create (path))
			using (var archive = ZipArchive.Create (stream)) {
				foreach (var entry in entries) {
					archive.AddEntry (entry.name, entry.contents, encoding: Encoding.UTF8);
				}
			}
		}
	}
}
