using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

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
			// Xamarin.Android specific files
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
		public string BaseZip { get; set; }

		public string CustomBuildConfigFile { get; set; }

		[Required]
		public string Output { get; set; }

		public string UncompressedFileExtensions { get; set; }

		string temp;

		public override bool RunTask ()
		{
			temp = Path.GetTempFileName ();
			try {
				var uncompressed = new List<string> (UncompressedByDefault);
				if (!string.IsNullOrEmpty (UncompressedFileExtensions)) {
					//NOTE: these are file extensions, that need converted to glob syntax
					var split = UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var extension in split) {
						uncompressed.Add ("**/*" + extension);
					}
				}

				var json = JObject.FromObject(new { });
				if(!string.IsNullOrWhiteSpace(CustomBuildConfigFile) && File.Exists(CustomBuildConfigFile))
				{
					try
					{
						using (StreamReader file = File.OpenText(CustomBuildConfigFile))
						using (JsonTextReader reader = new JsonTextReader(file))
						{
							json = (JObject)JToken.ReadFrom(reader);
						}
					}
					catch (Exception e)
					{ } // Do nothing for now
				}
				var jsonAddition = JObject.FromObject(new {
					compression = new {
						uncompressedGlob = uncompressed,
					}
				});

				var mergeSettings = new JsonMergeSettings() {
					MergeArrayHandling = MergeArrayHandling.Replace,
					MergeNullValueHandling = MergeNullValueHandling.Ignore
				};
				json.Merge(jsonAddition, mergeSettings);
				Log.LogDebugMessage ("BundleConfig.json: {0}", json);
				File.WriteAllText (temp, json.ToString());

				//NOTE: bundletool will not overwrite
				if (File.Exists (Output))
					File.Delete (Output);

				base.RunTask ();
			} finally {
				File.Delete (temp);
			}

			return !Log.HasLoggedErrors;
		}

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("build-bundle");
			cmd.AppendSwitchIfNotNull ("--modules ", BaseZip);
			cmd.AppendSwitchIfNotNull ("--output ", Output);
			cmd.AppendSwitchIfNotNull ("--config ", temp);
			return cmd;
		}
	}
}
