using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BuildAppBundleTests
	{
		MockBuildEngine engine;
		List<BuildMessageEventArgs> messages;
		string tempDir;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				messages: messages = new List<BuildMessageEventArgs> ());
			tempDir = Path.Combine (Path.GetTempPath (), "BuildAppBundleTests_" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (tempDir);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}

		[Test]
		public void StagesUnicodeMetadataForBundleTool ()
		{
			var sourcePath = Path.Combine (tempDir, "libテスト\u2002Me.dbg.so");
			var output = Path.Combine (tempDir, "output.aab");
			var archivePath = "com.android.tools.build.debugsymbols/android-arm64/libテスト\u2002Me.so.dbg";
			File.WriteAllText (sourcePath, "symbols");

			var task = new BuildAppBundle {
				BuildEngine = engine,
				JarPath = Path.Combine (tempDir, "bundletool.jar"),
				BaseZip = Path.Combine (tempDir, "base.zip"),
				Output = output,
				MetaDataFiles = new ITaskItem [] {
					new TaskItem ($"{archivePath}:{sourcePath}"),
				},
			};

			task.PrepareMetaDataFiles ();
			var commandLine = task.GetCommandLineBuilder ().ToString ();
			Assert.IsFalse (commandLine.Contains ("テスト"), "BundleTool command line should contain only ASCII metadata paths.");
			Assert.IsFalse (commandLine.Contains ("\u2002"), "BundleTool command line should not contain Unicode whitespace.");

			var metadata = task.BundleToolMetaDataFiles.Single ();
			var separator = metadata.IndexOf (':');
			Assert.Greater (separator, 0, $"Expected staged metadata argument in: {metadata}");
			var temporaryArchivePath = "BUNDLE-METADATA/" + metadata.Substring (0, separator);
			var temporarySourcePath = metadata.Substring (separator + 1);

			try {
				using (var outputBundle = ZipFile.Open (output, ZipArchiveMode.Create)) {
					var entry = outputBundle.CreateEntry (temporaryArchivePath);
					using var writer = new StreamWriter (entry.Open ());
					writer.Write ("symbols");
				}

				task.RestoreMetaDataFileNames ();

				using var restoredBundle = ZipFile.OpenRead (output);
				Assert.IsNull (restoredBundle.GetEntry (temporaryArchivePath), "Temporary metadata entry should be removed.");
				var restored = restoredBundle.GetEntry ("BUNDLE-METADATA/" + archivePath);
				Assert.IsNotNull (restored, "Metadata entry should use its original Unicode path.");
				using var reader = new StreamReader (restored.Open ());
				Assert.AreEqual ("symbols", reader.ReadToEnd ());
			} finally {
				File.Delete (temporarySourcePath);
			}
		}
	}

}
