#nullable enable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Invokes `bundletool` to create an Android App Bundle (.aab file)
	///
	/// Usage: bundletool build-bundle --modules=base.zip --output=foo.aab --config=BundleConfig.json
	/// </summary>
	public class BuildAppBundle : BundleTool
	{
		public override string TaskPrefix => "BAB";

		static readonly string [] UncompressedByDefault = new [] {
			// .NET for Android specific files
			"assemblies/**",
			// Android specific files, listed here:
			// https://github.com/google/bundletool/blob/5ac94cb61e949f135c50f6ce52bbb5f00e8e959f/src/main/java/com/android/tools/build/bundletool/io/ApkSerializerHelper.java#L111-L115
			"**/*.3g2",
			"**/*.3gp",
			"**/*.3gpp",
			"**/*.3gpp2",
			"**/*.aac",
			"**/*.amr",
			"**/*.awb",
			"**/*.gif",
			"**/*.imy",
			"**/*.jet",
			"**/*.jpeg",
			"**/*.jpg",
			"**/*.m4a",
			"**/*.m4v",
			"**/*.mid",
			"**/*.midi",
			"**/*.mkv",
			"**/*.mp2",
			"**/*.mp3",
			"**/*.mp4",
			"**/*.mpeg",
			"**/*.mpg",
			"**/*.ogg",
			"**/*.png",
			"**/*.rtttl",
			"**/*.smf",
			"**/*.wav",
			"**/*.webm",
			"**/*.wma",
			"**/*.wmv",
			"**/*.xmf",
		};

		[Required]
		public string BaseZip { get; set; } = "";

		public string? CustomBuildConfigFile { get; set; }

		public string []? Modules { get; set; }

		public ITaskItem []? MetaDataFiles { get; set; }

		[Required]
		public string Output { get; set; } = "";

		public string? UncompressedFileExtensions { get; set; }

		string? temp;
		string? metaDataStagingDirectory;
		string? stagingDirectoryToWorkingDirectory;
		readonly List<string> bundleToolMetaDataFiles = new List<string> ();
		readonly List<StagedMetaDataFile> stagedMetaDataFiles = new List<StagedMetaDataFile> ();
		internal IReadOnlyList<string> BundleToolMetaDataFiles => bundleToolMetaDataFiles;
		internal string? MetaDataStagingDirectory => metaDataStagingDirectory;

		// ZipArchiveMode.Update keeps replacement entries in memory until the archive is disposed.
		internal long MetaDataRestoreBatchSizeLimit { get; set; } = 100 * 1024 * 1024;
		internal int MetaDataRestoreBatchFilesLimit { get; set; } = 512;

		public override bool RunTask ()
		{
			temp = Path.GetTempFileName ();
			try {
				var uncompressed = new List<string> (UncompressedByDefault);
				if (!UncompressedFileExtensions.IsNullOrEmpty ()) {
					//NOTE: these are file extensions, that need converted to glob syntax
					var split = UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var extension in split) {
						uncompressed.Add ("**/*" + extension);
					}
				}

				JsonNode? json = JsonNode.Parse ("{}");
				if (!CustomBuildConfigFile.IsNullOrEmpty () && File.Exists (CustomBuildConfigFile)) {
					using Stream fs = File.OpenRead (CustomBuildConfigFile);
					using JsonDocument doc = JsonDocument.Parse (fs, new JsonDocumentOptions { AllowTrailingCommas = true });
					json = doc.RootElement.ToNode ();
				}
				var jsonAddition = new {
					compression = new {
						uncompressedGlob = uncompressed,
					}
				};

				var jsonAdditionDoc = JsonSerializer.SerializeToNode (jsonAddition);

				var mergedJson = json.Merge (jsonAdditionDoc);
				var output = mergedJson?.ToJsonString (new JsonSerializerOptions { WriteIndented = true });

				Log.LogDebugMessage ($"BundleConfig.json: {output}");
				File.WriteAllText (temp, output);
				PrepareMetaDataFiles ();
				JarPath = GetToolPath (JarPath);

				//NOTE: bundletool will not overwrite
				if (File.Exists (Output))
					File.Delete (Output);

				base.RunTask ();
				if (!Log.HasLoggedErrors)
					RestoreMetaDataFileNames ();
			} finally {
				File.Delete (temp);
				DeleteMetaDataStagingDirectory ();
			}

			return !Log.HasLoggedErrors;
		}

		internal override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("build-bundle");
			var modules = new List<string> ();
			modules.Add (GetToolPath (BaseZip));
			if (Modules != null && Modules.Any ())
				modules.AddRange (Modules.Select (GetToolPath));
			cmd.AppendSwitchIfNotNull ("--modules ", string.Join (",", modules));
			cmd.AppendSwitchIfNotNull ("--output ", GetToolPath (Output));
			cmd.AppendSwitchIfNotNull ("--config ", temp);
			foreach (var file in bundleToolMetaDataFiles) {
				cmd.AppendSwitchIfNotNull ("--metadata-file=", file);
			}
			return cmd;
		}

		// Staged metadata arguments are relative to this directory so the command line stays ASCII
		// even when the staging directory's full path is not.
		protected override string? GetWorkingDirectory () =>
			metaDataStagingDirectory ?? base.GetWorkingDirectory ();

		// Paths that were relative to the project directory must be rebased onto the staging directory,
		// preferably with a relative `..` prefix so ASCII paths stay ASCII.
		string GetToolPath (string path)
		{
			if (metaDataStagingDirectory == null || path.IsNullOrEmpty () || Path.IsPathRooted (path))
				return path;
			if (stagingDirectoryToWorkingDirectory != null)
				return Path.Combine (stagingDirectoryToWorkingDirectory, path);
			return Path.GetFullPath (Path.Combine (WorkingDirectory, path));
		}

		internal void PrepareMetaDataFiles ()
		{
			bundleToolMetaDataFiles.Clear ();
			stagedMetaDataFiles.Clear ();
			DeleteMetaDataStagingDirectory ();
			var unstagedMetaDataFiles = new List<string> ();
			foreach (var file in MetaDataFiles ?? []) {
				if (file.ItemSpec.All (character => character <= 0x7f)) {
					unstagedMetaDataFiles.Add (file.ItemSpec);
					continue;
				}

				var separator = file.ItemSpec.IndexOf (':');
				if (separator < 0 || separator == file.ItemSpec.Length - 1) {
					unstagedMetaDataFiles.Add (file.ItemSpec);
					continue;
				}

				// Java on Windows can replace non-ASCII command-line characters before BundleTool sees them.
				var archivePath = file.ItemSpec.Substring (0, separator);
				var originalSourcePath = file.ItemSpec.Substring (separator + 1);
				var directorySeparator = archivePath.IndexOf ('/');
				if (directorySeparator < 0) {
					unstagedMetaDataFiles.Add (file.ItemSpec);
					continue;
				}
				var stagingDirectory = GetOrCreateMetaDataStagingDirectory ();
				var sourceFileName = $"metadata-{Guid.NewGuid ():N}.bin";
				var temporaryArchivePath = $"{archivePath.Substring (0, directorySeparator)}/xamarin-android-{Guid.NewGuid ():N}";
				stagedMetaDataFiles.Add (new StagedMetaDataFile {
					ArchivePath = $"BUNDLE-METADATA/{archivePath}",
					SourcePath = Path.Combine (stagingDirectory, sourceFileName),
					TemporaryArchivePath = $"BUNDLE-METADATA/{temporaryArchivePath}",
				});
				File.Copy (originalSourcePath, Path.Combine (stagingDirectory, sourceFileName), overwrite: true);
				bundleToolMetaDataFiles.Add ($"{temporaryArchivePath}:{sourceFileName}");
			}

			foreach (var file in unstagedMetaDataFiles) {
				var separator = file.IndexOf (':');
				if (metaDataStagingDirectory == null || separator < 0 || separator == file.Length - 1) {
					bundleToolMetaDataFiles.Add (file);
					continue;
				}
				bundleToolMetaDataFiles.Add ($"{file.Substring (0, separator)}:{GetToolPath (file.Substring (separator + 1))}");
			}
		}

		string GetOrCreateMetaDataStagingDirectory ()
		{
			if (metaDataStagingDirectory != null)
				return metaDataStagingDirectory;

			var stagingDirectoryName = $"bundletool-metadata-{Guid.NewGuid ():N}";
			string directory;
			if (IntermediateOutputPath.IsNullOrEmpty ()) {
				directory = Path.Combine (Path.GetTempPath (), stagingDirectoryName);
				stagingDirectoryToWorkingDirectory = null;
			} else {
				directory = Path.Combine (WorkingDirectory, IntermediateOutputPath, stagingDirectoryName);
				stagingDirectoryToWorkingDirectory = GetRelativePathToParent (IntermediateOutputPath);
			}
			directory = Path.GetFullPath (directory);
			Directory.CreateDirectory (directory);
			metaDataStagingDirectory = directory;
			return directory;
		}

		// Returns the `..` path from `<relativeDirectory>/<child>` back to the working directory,
		// or null when it cannot be expressed without resolving `..` segments in `relativeDirectory`.
		static string? GetRelativePathToParent (string relativeDirectory)
		{
			if (Path.IsPathRooted (relativeDirectory))
				return null;
			var segments = relativeDirectory.Split (new [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
				.Where (segment => segment != ".")
				.ToList ();
			if (segments.Any (segment => segment == ".."))
				return null;
			return string.Join (Path.DirectorySeparatorChar.ToString (), Enumerable.Repeat ("..", segments.Count + 1));
		}

		void DeleteMetaDataStagingDirectory ()
		{
			if (metaDataStagingDirectory == null)
				return;
			if (Directory.Exists (metaDataStagingDirectory))
				Directory.Delete (metaDataStagingDirectory, recursive: true);
			metaDataStagingDirectory = null;
			stagingDirectoryToWorkingDirectory = null;
		}

		internal void RestoreMetaDataFileNames ()
		{
			int index = 0;
			while (index < stagedMetaDataFiles.Count) {
				// Reopen the archive between batches to bound the memory retained by Update mode.
				using var bundle = ZipFile.Open (Output, ZipArchiveMode.Update);
				long batchSize = 0;
				int batchFiles = 0;
				do {
					var file = stagedMetaDataFiles [index++];
					var temporaryEntry = bundle.GetEntry (file.TemporaryArchivePath);
					if (temporaryEntry == null)
						throw new InvalidOperationException ($"Bundle metadata entry '{file.TemporaryArchivePath}' was not found.");

					var entry = bundle.CreateEntry (file.ArchivePath, CompressionLevel.Optimal);
					entry.LastWriteTime = temporaryEntry.LastWriteTime;
					batchSize += temporaryEntry.Length;
					batchFiles++;
					using (var input = temporaryEntry.Open ())
					using (var output = entry.Open ())
						input.CopyTo (output);
					temporaryEntry.Delete ();
				} while (index < stagedMetaDataFiles.Count && batchSize < MetaDataRestoreBatchSizeLimit && batchFiles < MetaDataRestoreBatchFilesLimit);
			}
		}

		sealed class StagedMetaDataFile
		{
			public string ArchivePath { get; set; } = "";
			public string SourcePath { get; set; } = "";
			public string TemporaryArchivePath { get; set; } = "";
		}
	}
}
