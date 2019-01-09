using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task invokes r8 in order to:
	/// - Compile to dex format + code shrinking (replacement for proguard)
	/// - Enable multi-dex, even if code shrinking is not used
	/// </summary>
	public class R8 : D8
	{
		[Required]
		public string AndroidSdkBuildToolsPath { get; set; }
		[Required]
		public string AndroidSdkDirectory { get; set; }

		// multidex
		public bool EnableMultiDex { get; set; }
		public ITaskItem [] CustomMainDexListFiles { get; set; }
		public string MultiDexMainDexListFile { get; set; }

		// proguard-like configuration settings
		public bool EnableShrinking { get; set; } = true;
		public string AcwMapFile { get; set; }
		public string ProguardGeneratedReferenceConfiguration { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }
		public string ProguardCommonXamarinConfiguration { get; set; }
		public string ProguardConfigurationFiles { get; set; }

		protected override string MainClass => "com.android.tools.r8.SwissArmyKnife";

		string temp;

		public override bool Execute ()
		{
			try {
				temp = Path.GetTempFileName ();
				return base.Execute ();
			} finally {
				if (!string.IsNullOrEmpty (temp))
					File.Delete (temp);
			}
		}

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();

			if (EnableMultiDex) {
				if (string.IsNullOrEmpty (MultiDexMainDexListFile)) {
					Log.LogCodedWarning ("XA4305", $"MultiDex is enabled, but '{nameof (MultiDexMainDexListFile)}' was not specified.");
				} else {
					var content = new List<string> ();
					if (CustomMainDexListFiles != null) {
						foreach (var file in CustomMainDexListFiles) {
							if (File.Exists (file.ItemSpec)) {
								content.Add (File.ReadAllText (file.ItemSpec));
							} else {
								Log.LogCodedWarning ("XA4305", file.ItemSpec, 0, $"'MultiDexMainDexList' file '{file.ItemSpec}' does not exist.");
							}
						}
					}
					File.WriteAllText (temp, string.Concat (content));

					cmd.AppendSwitchIfNotNull ("--main-dex-list ", temp);
					cmd.AppendSwitchIfNotNull ("--main-dex-rules ", Path.Combine (AndroidSdkBuildToolsPath, "mainDexClasses.rules"));
					cmd.AppendSwitchIfNotNull ("--main-dex-list-output ", MultiDexMainDexListFile);
				}
			}

			if (EnableShrinking) {
				if (!string.IsNullOrEmpty (AcwMapFile)) {
					var acwLines = File.ReadAllLines (AcwMapFile);
					using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration)) {
						for (int i = 0; i + 2 < acwLines.Length; i += 3) {
							try {
								var line = acwLines [i + 2];
								var java = line.Substring (line.IndexOf (';') + 1);
								appcfg.WriteLine ("-keep class " + java + " { *; }");
							} catch {
								// skip invalid lines
							}
						}
					}
				}
				if (!string.IsNullOrWhiteSpace (ProguardCommonXamarinConfiguration))
					using (var xamcfg = File.Create (ProguardCommonXamarinConfiguration))
						GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg);
				if (!string.IsNullOrEmpty (ProguardConfigurationFiles)) {
					var configs = ProguardConfigurationFiles
						.Replace ("{sdk.dir}", AndroidSdkDirectory + Path.DirectorySeparatorChar)
						.Replace ("{intermediate.common.xamarin}", ProguardCommonXamarinConfiguration)
						.Replace ("{intermediate.references}", ProguardGeneratedReferenceConfiguration)
						.Replace ("{intermediate.application}", ProguardGeneratedApplicationConfiguration)
						.Replace ("{project}", string.Empty) // current directory anyways.
						.Split (';')
						.Select (s => s.Trim ())
						.Where (s => !string.IsNullOrWhiteSpace (s));
					foreach (var file in configs) {
						if (File.Exists (file))
							cmd.AppendSwitchIfNotNull ("--pg-conf ", file);
						else
							Log.LogCodedWarning ("XA4304", file, 0, "Proguard configuration file '{0}' was not found.", file);
					}
				}
			} else {
				//NOTE: we may be calling r8 *only* for multi-dex, and all shrinking is disabled
				cmd.AppendSwitch ("--no-tree-shaking");
				cmd.AppendSwitch ("--no-minification");
			}

			return cmd;
		}
	}
	
}
