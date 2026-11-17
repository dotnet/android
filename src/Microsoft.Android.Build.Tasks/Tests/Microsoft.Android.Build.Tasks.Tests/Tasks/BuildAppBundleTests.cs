#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class BuildAppBundleTests
	{
		MockBuildEngine? engine;
		string tempDir = "";

		MockBuildEngine Engine => engine ?? throw new InvalidOperationException ("SetUp did not run.");

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, messages: new List<BuildMessageEventArgs> ());
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
		public void BuildAppBundleCommand ()
		{
			var task = new BuildAppBundle {
				BaseZip = "base.zip",
				Output = "foo.aab",
				JarPath = "bundletool.jar",
			};
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ("-jar bundletool.jar build-bundle --modules base.zip --output foo.aab", cmd);
		}

		[Test]
		public void StagesUnicodeMetadataForBundleTool ()
		{
			var sourcePath = Path.Combine (tempDir, "libテスト\u2002Me.dbg.so");
			var output = Path.Combine (tempDir, "output.aab");
			var archivePath = "com.android.tools.build.debugsymbols/android-arm64/libテスト\u2002Me.so.dbg";
			File.WriteAllText (sourcePath, "symbols");

			var task = new BuildAppBundle {
				BuildEngine = Engine,
				JarPath = Path.Combine (tempDir, "bundletool.jar"),
				BaseZip = Path.Combine (tempDir, "base.zip"),
				Output = output,
				IntermediateOutputPath = tempDir,
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
			var stagingDirectory = task.MetaDataStagingDirectory
				?? throw new AssertionException ("Metadata should be staged in a private directory.");
			var temporarySourcePath = Path.Combine (stagingDirectory, metadata.Substring (separator + 1));
			FileAssert.Exists (temporarySourcePath);

			try {
				using (var outputBundle = ZipFile.Open (output, ZipArchiveMode.Create)) {
					var entry = outputBundle.CreateEntry (temporaryArchivePath);
					using var writer = new StreamWriter (entry.Open ());
					writer.Write ("symbols");
				}

				task.RestoreMetaDataFileNames ();

				using var restoredBundle = ZipFile.OpenRead (output);
				Assert.IsNull (restoredBundle.GetEntry (temporaryArchivePath), "Temporary metadata entry should be removed.");
				var restored = restoredBundle.GetEntry ("BUNDLE-METADATA/" + archivePath)
					?? throw new AssertionException ("Metadata entry should use its original Unicode path.");
				using var reader = new StreamReader (restored.Open ());
				Assert.AreEqual ("symbols", reader.ReadToEnd ());
			} finally {
				File.Delete (temporarySourcePath);
			}
		}

		[Test]
		public void StagesMetadataWithAsciiArgumentsUnderNonAsciiDirectory ()
		{
			var intermediateDir = Path.Combine (tempDir, "テスト\u2002obj");
			Directory.CreateDirectory (intermediateDir);
			var sourcePath = Path.Combine (intermediateDir, "libテスト.dbg.so");
			File.WriteAllText (sourcePath, "symbols");

			var task = new BuildAppBundle {
				BuildEngine = Engine,
				JarPath = Path.Combine (tempDir, "bundletool.jar"),
				BaseZip = Path.Combine (intermediateDir, "base.zip"),
				Output = Path.Combine (intermediateDir, "output.aab"),
				IntermediateOutputPath = intermediateDir,
				MetaDataFiles = new ITaskItem [] {
					new TaskItem ($"com.android.tools.build.debugsymbols/arm64-v8a/libテスト.so.dbg:{sourcePath}"),
				},
			};

			task.PrepareMetaDataFiles ();
			var stagingDirectory = task.MetaDataStagingDirectory
				?? throw new AssertionException ("Metadata should be staged in a private directory.");
			StringAssert.Contains ("テスト", stagingDirectory, "Test requires a non-ASCII staging directory path.");

			var metadata = task.BundleToolMetaDataFiles.Single ();
			Assert.IsTrue (metadata.All (c => c <= 0x7f), $"Metadata argument should be ASCII: {metadata}");
			var stagedPath = metadata.Substring (metadata.IndexOf (':') + 1);
			Assert.IsFalse (Path.IsPathRooted (stagedPath), $"Staged metadata path should be relative to the tool working directory: {stagedPath}");
			FileAssert.Exists (Path.Combine (stagingDirectory, stagedPath));

			var commandLine = task.GetCommandLineBuilder ().ToString ();
			var metadataSwitch = $"--metadata-file={metadata}";
			StringAssert.Contains (metadataSwitch, commandLine);
			StringAssert.Contains (Path.Combine (intermediateDir, "base.zip"), commandLine, "Other tool paths should remain absolute.");
		}

		[Test]
		public void RebasesRelativeMetadataPathsOntoStagingDirectory ()
		{
			var workingDirectory = Directory.GetCurrentDirectory ();
			var projectDir = Path.Combine (workingDirectory, "BuildAppBundleTests_" + Guid.NewGuid ().ToString ("N"));
			try {
				var mappingPath = Path.Combine (projectDir, "bin", "mapping.txt");
				Directory.CreateDirectory (Path.GetDirectoryName (mappingPath) ?? throw new InvalidOperationException ());
				File.WriteAllText (mappingPath, "mapping");
				var unicodeSourcePath = Path.Combine (projectDir, "libテスト.dbg.so");
				File.WriteAllText (unicodeSourcePath, "symbols");
				var relativeProjectDir = Path.GetRelativePath (workingDirectory, projectDir);

				var task = new BuildAppBundle {
					BuildEngine = Engine,
					JarPath = Path.Combine (tempDir, "bundletool.jar"),
					BaseZip = Path.Combine (relativeProjectDir, "base.zip"),
					Output = Path.Combine (relativeProjectDir, "bin", "output.aab"),
					IntermediateOutputPath = Path.Combine (relativeProjectDir, "obj"),
					MetaDataFiles = new ITaskItem [] {
						new TaskItem ($"com.android.tools.build.obfuscation/proguard.map:{Path.Combine (relativeProjectDir, "bin", "mapping.txt")}"),
						new TaskItem ($"com.android.tools.build.debugsymbols/arm64-v8a/libテスト.so.dbg:{unicodeSourcePath}"),
					},
				};

				task.PrepareMetaDataFiles ();
				var stagingDirectory = task.MetaDataStagingDirectory
					?? throw new AssertionException ("Unicode metadata should be staged.");

				foreach (var metadata in task.BundleToolMetaDataFiles) {
					Assert.IsTrue (metadata.All (c => c <= 0x7f), $"Metadata argument should be ASCII: {metadata}");
					var source = metadata.Substring (metadata.IndexOf (':') + 1);
					FileAssert.Exists (Path.Combine (stagingDirectory, source), $"Metadata source should resolve from the BundleTool working directory: {metadata}");
				}

				var commandLine = task.GetCommandLineBuilder ().ToString ();
				Assert.IsTrue (commandLine.All (c => c <= 0x7f), $"Command line should be ASCII: {commandLine}");
				var expectedOutput = Path.Combine ("..", "..", "..", relativeProjectDir, "bin", "output.aab");
				StringAssert.Contains (expectedOutput, commandLine, "Relative output path should be rebased onto the staging directory.");
				Assert.AreEqual (Path.GetFullPath (Path.Combine (projectDir, "bin", "output.aab")), Path.GetFullPath (Path.Combine (stagingDirectory, expectedOutput)));
			} finally {
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, recursive: true);
			}
		}

		[Test]
		public void StagesConfigWithAsciiArgumentUnderNonAsciiTemporaryDirectory ()
		{
			var workingDirectory = Directory.GetCurrentDirectory ();
			var projectDir = Path.Combine (workingDirectory, "BuildAppBundleTests_" + Guid.NewGuid ().ToString ("N"));
			var temporaryDirectory = Path.Combine (tempDir, "Tëmp-ünïcode");
			Directory.CreateDirectory (temporaryDirectory);
			try {
				var mappingPath = Path.Combine (projectDir, "bin", "mapping.txt");
				Directory.CreateDirectory (Path.GetDirectoryName (mappingPath) ?? throw new InvalidOperationException ());
				File.WriteAllText (mappingPath, "mapping");
				var relativeProjectDir = Path.GetRelativePath (workingDirectory, projectDir);

				var task = new BuildAppBundle {
					BuildEngine = Engine,
					JarPath = Path.Combine (tempDir, "bundletool.jar"),
					BaseZip = Path.Combine (relativeProjectDir, "base.zip"),
					Output = Path.Combine (relativeProjectDir, "bin", "output.aab"),
					IntermediateOutputPath = Path.Combine (relativeProjectDir, "obj"),
					TemporaryDirectory = temporaryDirectory,
					MetaDataFiles = new ITaskItem [] {
						new TaskItem ($"com.android.tools.build.obfuscation/proguard.map:{Path.Combine (relativeProjectDir, "bin", "mapping.txt")}"),
					},
				};

				task.PrepareConfigFile ("{}");
				task.PrepareMetaDataFiles ();

				var stagingDirectory = task.MetaDataStagingDirectory
					?? throw new AssertionException ("Config should be staged when the temporary directory is not ASCII.");
				var configFile = task.ConfigFile
					?? throw new AssertionException ("Config file should be created.");
				FileAssert.Exists (configFile);

				var commandLine = task.GetCommandLineBuilder ().ToString ();
				Assert.IsTrue (commandLine.All (c => c <= 0x7f), $"Command line should be ASCII: {commandLine}");
				Assert.IsFalse (commandLine.Contains (temporaryDirectory), "Command line should not reference the non-ASCII temporary directory.");
				StringAssert.Contains ("--config BundleConfig.json", commandLine);
				Assert.AreEqual (Path.GetFullPath (configFile), Path.GetFullPath (Path.Combine (stagingDirectory, "BundleConfig.json")));

				var metadata = task.BundleToolMetaDataFiles.Single ();
				FileAssert.Exists (Path.Combine (stagingDirectory, metadata.Substring (metadata.IndexOf (':') + 1)), $"Metadata source should resolve from the BundleTool working directory: {metadata}");
			} finally {
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, recursive: true);
			}
		}

		[Test]
		public void RestoresMetadataEntriesInBatches ()
		{
			const int count = 5;
			var output = Path.Combine (tempDir, "output.aab");
			var items = new List<ITaskItem> ();
			for (int i = 0; i < count; i++) {
				var sourcePath = Path.Combine (tempDir, $"libテスト{i}.dbg.so");
				File.WriteAllText (sourcePath, $"symbols{i}");
				items.Add (new TaskItem ($"com.android.tools.build.debugsymbols/arm64-v8a/libテスト{i}.so.dbg:{sourcePath}"));
			}

			var task = new BuildAppBundle {
				BuildEngine = Engine,
				JarPath = Path.Combine (tempDir, "bundletool.jar"),
				BaseZip = Path.Combine (tempDir, "base.zip"),
				Output = output,
				IntermediateOutputPath = tempDir,
				MetaDataFiles = items.ToArray (),
				MetaDataRestoreBatchFilesLimit = 2,
			};

			task.PrepareMetaDataFiles ();
			Assert.AreEqual (count, task.BundleToolMetaDataFiles.Count);
			using (var bundle = ZipFile.Open (output, ZipArchiveMode.Create)) {
				var other = bundle.CreateEntry ("base/manifest/AndroidManifest.xml");
				using (var writer = new StreamWriter (other.Open ()))
					writer.Write ("manifest");
				for (int i = 0; i < count; i++) {
					var metadata = task.BundleToolMetaDataFiles [i];
					var entry = bundle.CreateEntry ("BUNDLE-METADATA/" + metadata.Substring (0, metadata.IndexOf (':')));
					using var writer = new StreamWriter (entry.Open ());
					writer.Write ($"symbols{i}");
				}
			}

			task.RestoreMetaDataFileNames ();

			using var restoredBundle = ZipFile.OpenRead (output);
			Assert.AreEqual (count + 1, restoredBundle.Entries.Count, "Temporary metadata entries should be replaced.");
			Assert.IsFalse (restoredBundle.Entries.Any (e => e.FullName.Contains ("xamarin-android-")), "Temporary metadata entries should be removed.");
			for (int i = 0; i < count; i++) {
				var restored = restoredBundle.GetEntry ($"BUNDLE-METADATA/com.android.tools.build.debugsymbols/arm64-v8a/libテスト{i}.so.dbg")
					?? throw new AssertionException ($"Metadata entry {i} should use its original Unicode path.");
				using var reader = new StreamReader (restored.Open ());
				Assert.AreEqual ($"symbols{i}", reader.ReadToEnd ());
			}
			var manifest = restoredBundle.GetEntry ("base/manifest/AndroidManifest.xml")
				?? throw new AssertionException ("Manifest entry should be preserved.");
			using (var reader = new StreamReader (manifest.Open ()))
				Assert.AreEqual ("manifest", reader.ReadToEnd ());
		}
	}
}
