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
		public ITaskItem []? ProguardConfigurationFiles { get; set; }
		public bool UseTrimmableNativeAotProguardConfiguration { get; set; }

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

		/// <summary>
		/// Override CreateResponseFile to add R8-specific arguments to the response file.
		/// This ensures all arguments are passed via response file to avoid command line length limits.
		/// </summary>
		protected override string CreateResponseFile ()
		{
			// First, get the base response file path and write base D8 arguments
			var responseFile = base.CreateResponseFile ();

			// Now append R8-specific arguments to the response file
			using var response = new StreamWriter (responseFile, append: true, encoding: Files.UTF8withoutBOM);

			if (EnableMultiDex) {
				if (MinSdkVersion >= 21) {
					if (CustomMainDexListFiles?.Length > 0) {
						Log.LogCodedWarning ("XA4306", Properties.Resources.XA4306);
					}
				} else if (MultiDexMainDexListFile.IsNullOrEmpty ()) {
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

					WriteArg (response, "--main-dex-list");
					WriteArg (response, temp);
					WriteArg (response, "--main-dex-rules");
					WriteArg (response, Path.Combine (AndroidSdkBuildToolsPath, "mainDexClasses.rules"));
					WriteArg (response, "--main-dex-list-output");
					WriteArg (response, MultiDexMainDexListFile);
				}
			}

			if (EnableShrinking) {
				if (UseTrimmableNativeAotProguardConfiguration && !ProguardGeneratedApplicationConfiguration.IsNullOrEmpty ()) {
					File.WriteAllText (ProguardGeneratedApplicationConfiguration, "# ACW keep rules are generated from NativeAOT ILC metadata.\n");
				} else if (!AcwMapFile.IsNullOrEmpty ()) {
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
				if (!ProguardCommonXamarinConfiguration.IsNullOrWhiteSpace ()) {
					using (var xamcfg = File.CreateText (ProguardCommonXamarinConfiguration)) {
						if (UseTrimmableNativeAotProguardConfiguration) {
							using var stream = GetEmbeddedResourceStream ("proguard_trimmable_nativeaot.cfg");
							stream.CopyTo (xamcfg.BaseStream);
						} else {
							using var stream = GetEmbeddedResourceStream ("proguard_xamarin.cfg");
							stream.CopyTo (xamcfg.BaseStream);
						}
						if (IgnoreWarnings) {
							xamcfg.WriteLine ("-ignorewarnings");
						}
						if (!ProguardMappingFileOutput.IsNullOrEmpty ()) {
							xamcfg.WriteLine ("-keepattributes SourceFile");
							xamcfg.WriteLine ("-keepattributes LineNumberTable");
							xamcfg.WriteLine ($"-printmapping \"{Path.GetFullPath (ProguardMappingFileOutput)}\"");
						}
					}
				}
			} else {
				//NOTE: we may be calling r8 *only* for multi-dex, and all shrinking is disabled
				WriteArg (response, "--no-tree-shaking");
				WriteArg (response, "--no-minification");
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
				if (!ProguardMappingFileOutput.IsNullOrEmpty ()) {
					lines.Add ("-keepattributes SourceFile");
					lines.Add ("-keepattributes LineNumberTable");
					lines.Add ($"-printmapping \"{Path.GetFullPath (ProguardMappingFileOutput)}\"");
				}
				File.WriteAllLines (temp, lines);
				tempFiles.Add (temp);
				WriteArg (response, "--pg-conf");
				WriteArg (response, temp);
			}
			if (ProguardConfigurationFiles != null) {
				foreach (var item in ProguardConfigurationFiles) {
					var file = item.ItemSpec;
					if (!File.Exists (file)) {
						Log.LogCodedWarning ("XA4304", file, 0, Properties.Resources.XA4304, file);
						continue;
					}
					if (HasDisallowedLibraryProguardOption (item, out var option)) {
						Log.LogCodedWarning ("XA4322", file, 0, Properties.Resources.XA4322,
							option, file, DescribeProguardSource (item));
						continue;
					}
					WriteArg (response, "--pg-conf");
					WriteArg (response, file);
				}
			}

			return responseFile;
		}

		// ProGuard "global" options that affect the whole build and are not allowed inside
		// a library's proguard.txt (the file packaged inside an .aar's root). AGP 9.0
		// introduced the same restriction — see "Behavior changes" in the AGP 9.0 release
		// notes:
		//   https://developer.android.com/build/releases/agp-9-0-0-release-notes#behavior-changes
		// We skip the whole offending file and emit a warning naming the source library
		// so the build can still succeed.
		static readonly string [] DisallowedLibraryProguardOptions = {
			"-dump",
			"-printconfiguration",
			"-printmapping",
			"-printseeds",
			"-printusage",
		};

		bool HasDisallowedLibraryProguardOption (ITaskItem item, out string option)
		{
			option = "";
			// Only library-provided proguard.txt files (extracted from .aar) carry OriginalFile
			// metadata. Skip files we generate ourselves or that the user added directly.
			if (item.GetMetadata ("OriginalFile").IsNullOrEmpty ()) {
				return false;
			}
			foreach (var raw in File.ReadLines (item.ItemSpec)) {
				if (TryGetDisallowedOption (raw, out var found)) {
					option = found;
					return true;
				}
			}
			return false;
		}

		internal static bool TryGetDisallowedOption (string line, out string option)
		{
			var trimmed = line.TrimStart ();
			foreach (var candidate in DisallowedLibraryProguardOptions) {
				if (trimmed.StartsWith (candidate, StringComparison.OrdinalIgnoreCase)) {
					option = candidate;
					return true;
				}
			}
			option = "";
			return false;
		}

		static string DescribeProguardSource (ITaskItem item)
		{
			var packageId = item.GetMetadata ("NuGetPackageId");
			if (!packageId.IsNullOrEmpty ()) {
				var version = item.GetMetadata ("NuGetPackageVersion");
				return version.IsNullOrEmpty ()
					? $"NuGet package '{packageId}'"
					: $"NuGet package '{packageId}' {version}";
			}
			var originalFile = item.GetMetadata ("OriginalFile");
			if (!originalFile.IsNullOrEmpty ()) {
				return $"'{originalFile}'";
			}
			return $"'{item.ItemSpec}'";
		}

		Stream GetEmbeddedResourceStream (string resourceName)
		{
			var stream = GetType ().Assembly.GetManifestResourceStream (resourceName);
			if (stream == null) {
				throw new InvalidOperationException ($"Missing embedded resource '{resourceName}'.");
			}
			return stream;
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
