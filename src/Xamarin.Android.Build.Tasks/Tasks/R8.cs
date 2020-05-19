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
		public override string TaskPrefix => "R8S";

		[Required]
		public string AndroidSdkBuildToolsPath { get; set; }
		[Required]
		public string AndroidSdkDirectory { get; set; }

		// proguard-like configuration settings
		public bool IgnoreWarnings { get; set; }
		public string AcwMapFile { get; set; }
		public string ProguardGeneratedReferenceConfiguration { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }
		public string ProguardCommonXamarinConfiguration { get; set; }
		public string ProguardConfigurationFiles { get; set; }

		protected override string MainClass => "com.android.tools.r8.R8";

		readonly List<string> tempFiles = new List<string> ();

		public override bool RunTask ()
		{
			try {
				return base.RunTask ();
			} finally {
				foreach (var temp in tempFiles) {
					File.Delete (temp);
				}
			}
		}

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();

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
			if (!string.IsNullOrWhiteSpace (ProguardCommonXamarinConfiguration)) {
				using (var xamcfg = File.CreateText (ProguardCommonXamarinConfiguration)) {
					GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg.BaseStream);
					if (IgnoreWarnings) {
						xamcfg.WriteLine ("-ignorewarnings");
					}
				}
			}
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
						Log.LogCodedWarning ("XA4304", file, 0, Properties.Resources.XA4304, file);
				}
			}

			return cmd;
		}
	}
	
}
