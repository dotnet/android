#nullable enable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
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
			"typemap.mj",
			"typemap.jm",
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

				Log.LogDebugMessage ("BundleConfig.json: {0}", output);
				File.WriteAllText (temp, output);

				//NOTE: bundletool will not overwrite
				if (File.Exists (Output))
					File.Delete (Output);

				base.RunTask ();
			} finally {
				File.Delete (temp);
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
			foreach (var file in MetaDataFiles ?? []) {
				cmd.AppendSwitch ($"--metadata-file={file.ItemSpec}");
			}
			return cmd;
		}
	}
}
