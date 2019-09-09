using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
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
		[Required]
		public string BaseZip { get; set; }

		[Required]
		public string Output { get; set; }

		public string UncompressedFileExtensions { get; set; }

		string temp;

		public override bool Execute ()
		{
			temp = Path.GetTempFileName ();
			try {
				var uncompressed = new List<string> {
					"typemap.mj",
					"typemap.jm",
					"assemblies/**",
				};
				if (!string.IsNullOrEmpty (UncompressedFileExtensions)) {
					//NOTE: these are file extensions, that need converted to glob syntax
					var split = UncompressedFileExtensions.Split (new char [] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var extension in split) {
						uncompressed.Add ("**/*" + extension);
					}
				}
				var json = JsonConvert.SerializeObject (new {
					compression = new {
						uncompressedGlob = uncompressed,
					}
				});
				Log.LogDebugMessage ("BundleConfig.json: {0}", json);
				File.WriteAllText (temp, json);

				//NOTE: bundletool will not overwrite
				if (File.Exists (Output))
					File.Delete (Output);

				base.Execute ();
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
