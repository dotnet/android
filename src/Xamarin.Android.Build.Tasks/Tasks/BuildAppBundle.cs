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
		readonly List<string> bundleToolMetaDataFiles = new List<string> ();
		readonly List<StagedMetaDataFile> stagedMetaDataFiles = new List<StagedMetaDataFile> ();
		internal IReadOnlyList<string> BundleToolMetaDataFiles => bundleToolMetaDataFiles;

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

				//NOTE: bundletool will not overwrite
				if (File.Exists (Output))
					File.Delete (Output);

				base.RunTask ();
				if (!Log.HasLoggedErrors)
					RestoreMetaDataFileNames ();
			} finally {
				File.Delete (temp);
				foreach (var file in stagedMetaDataFiles)
					File.Delete (file.SourcePath);
			}

			return !Log.HasLoggedErrors;
		}

		internal override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("build-bundle");
			var modules = new List<string> ();
			modules.Add (BaseZip);
			if (Modules != null && Modules.Any ())
				modules.AddRange (Modules);
			cmd.AppendSwitchIfNotNull ("--modules ", string.Join (",", modules));
			cmd.AppendSwitchIfNotNull ("--output ", Output);
			cmd.AppendSwitchIfNotNull ("--config ", temp);
			foreach (var file in bundleToolMetaDataFiles) {
				cmd.AppendSwitchIfNotNull ("--metadata-file=", file);
			}
			return cmd;
		}

		internal void PrepareMetaDataFiles ()
		{
			bundleToolMetaDataFiles.Clear ();
			stagedMetaDataFiles.Clear ();
			foreach (var file in MetaDataFiles ?? []) {
				if (file.ItemSpec.All (character => character <= 0x7f)) {
					bundleToolMetaDataFiles.Add (file.ItemSpec);
					continue;
				}

				var separator = file.ItemSpec.IndexOf (':');
				if (separator < 0 || separator == file.ItemSpec.Length - 1) {
					bundleToolMetaDataFiles.Add (file.ItemSpec);
					continue;
				}

				// Java on Windows can replace non-ASCII command-line characters before BundleTool sees them.
				var archivePath = file.ItemSpec.Substring (0, separator);
				var originalSourcePath = file.ItemSpec.Substring (separator + 1);
				var directorySeparator = archivePath.IndexOf ('/');
				if (directorySeparator < 0) {
					bundleToolMetaDataFiles.Add (file.ItemSpec);
					continue;
				}
				var sourcePath = Path.GetTempFileName ();
				var temporaryArchivePath = $"{archivePath.Substring (0, directorySeparator)}/xamarin-android-{Guid.NewGuid ():N}";
				stagedMetaDataFiles.Add (new StagedMetaDataFile {
					ArchivePath = $"BUNDLE-METADATA/{archivePath}",
					SourcePath = sourcePath,
					TemporaryArchivePath = $"BUNDLE-METADATA/{temporaryArchivePath}",
				});
				File.Copy (originalSourcePath, sourcePath, overwrite: true);
				bundleToolMetaDataFiles.Add ($"{temporaryArchivePath}:{sourcePath}");
			}
		}

		internal void RestoreMetaDataFileNames ()
		{
			if (stagedMetaDataFiles.Count == 0)
				return;

			using var bundle = ZipFile.Open (Output, ZipArchiveMode.Update);
			foreach (var file in stagedMetaDataFiles) {
				var temporaryEntry = bundle.GetEntry (file.TemporaryArchivePath);
				if (temporaryEntry == null)
					throw new InvalidOperationException ($"Bundle metadata entry '{file.TemporaryArchivePath}' was not found.");

				var entry = bundle.CreateEntry (file.ArchivePath, CompressionLevel.Optimal);
				entry.LastWriteTime = temporaryEntry.LastWriteTime;
				using (var input = temporaryEntry.Open ())
				using (var output = entry.Open ())
					input.CopyTo (output);
				temporaryEntry.Delete ();
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
