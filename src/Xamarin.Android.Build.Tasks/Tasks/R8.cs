#nullable enable

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;

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
		public string AndroidSdkBuildToolsPath { get; set; } = "";

		// multidex
		public bool EnableMultiDex { get; set; }
		public ITaskItem []? CustomMainDexListFiles { get; set; }
		public string? MultiDexMainDexListFile { get; set; }

		// proguard-like configuration settings
		public bool EnableShrinking { get; set; } = true;
		public bool IgnoreWarnings { get; set; }
		public string? AcwMapFile { get; set; }
		public string? ProguardGeneratedReferenceConfiguration { get; set; }
		public string? ProguardGeneratedApplicationConfiguration { get; set; }
		public string? ProguardCommonXamarinConfiguration { get; set; }
		public string? ProguardMappingFileOutput { get; set; }
		public string []? ProguardConfigurationFiles { get; set; }

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

			if (EnableMultiDex) {
				if (MinSdkVersion >= 21) {
					if (CustomMainDexListFiles?.Length > 0) {
						Log.LogCodedWarning ("XA4306", Properties.Resources.XA4306);
					}
				} else if (string.IsNullOrEmpty (MultiDexMainDexListFile)) {
					Log.LogCodedWarning ("XA4305", Properties.Resources.XA4305);
				} else {
					var content = new List<string> ();
					var temp = Path.GetTempFileName ();
					tempFiles.Add (temp);
					if (CustomMainDexListFiles != null) {
						foreach (var file in CustomMainDexListFiles) {
							if (File.Exists (file.ItemSpec)) {
								content.Add (File.ReadAllText (file.ItemSpec));
							} else {
								Log.LogCodedWarning ("XA4309", file.ItemSpec, 0, Properties.Resources.XA4309, file.ItemSpec);
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
					var acwMap      = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (AcwMapFile), StringComparer.OrdinalIgnoreCase);
					var javaTypes   = new List<string> (acwMap.Values.Count);
					foreach (var v in acwMap.Values) {
						javaTypes.Add (v);
					}
					javaTypes.Sort (StringComparer.Ordinal);
					using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration)) {
						foreach (var java in javaTypes) {
							appcfg.WriteLine ($"-keep class {java} {{ *; }}");
						}
					}
				}
				if (!string.IsNullOrWhiteSpace (ProguardCommonXamarinConfiguration)) {
					using (var xamcfg = File.CreateText (ProguardCommonXamarinConfiguration)) {
						GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg.BaseStream);
						if (IgnoreWarnings) {
							xamcfg.WriteLine ("-ignorewarnings");
						}
						if (!string.IsNullOrEmpty (ProguardMappingFileOutput)) {
							xamcfg.WriteLine ("-keepattributes SourceFile");
							xamcfg.WriteLine ("-keepattributes LineNumberTable");
							xamcfg.WriteLine ($"-printmapping \"{Path.GetFullPath (ProguardMappingFileOutput)}\"");
						}
					}
				}
			} else {
				//NOTE: we may be calling r8 *only* for multi-dex, and all shrinking is disabled
				cmd.AppendSwitch ("--no-tree-shaking");
				cmd.AppendSwitch ("--no-minification");
				// Rules to turn off optimizations
				var temp = Path.GetTempFileName ();
				var lines = new List<string> {
					"-dontoptimize",
					"-dontpreverify",
					"-keepattributes **"
				};
				if (IgnoreWarnings) {
					lines.Add ("-ignorewarnings");
				}
				if (!string.IsNullOrEmpty (ProguardMappingFileOutput)) {
					lines.Add ("-keepattributes SourceFile");
					lines.Add ("-keepattributes LineNumberTable");
					lines.Add ($"-printmapping \"{Path.GetFullPath (ProguardMappingFileOutput)}\"");
				}
				File.WriteAllLines (temp, lines);
				tempFiles.Add (temp);
				cmd.AppendSwitchIfNotNull ("--pg-conf ", temp);
			}
			if (ProguardConfigurationFiles != null) {
				foreach (var file in ProguardConfigurationFiles) {
					if (File.Exists (file))
						cmd.AppendSwitchIfNotNull ("--pg-conf ", file);
					else
						Log.LogCodedWarning ("XA4304", file, 0, Properties.Resources.XA4304, file);
				}
			}

			return cmd;
		}

		// Note: We do not want to call the base.LogEventsFromTextOutput as it will incorrectly identify
		// Warnings and Info messages as errors.
		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			CheckForError (singleLine);
			Log.LogMessage (messageImportance, singleLine);
		}
	}

}
