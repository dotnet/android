#nullable enable

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks.JniRemapping;

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
		public string? ProguardMappingFileInput { get; set; }
		public ITaskItem []? ProguardConfigurationFiles { get; set; }
		public bool UseTrimmableNativeAotProguardConfiguration { get; set; }
		public bool GenerateSeedMapping { get; set; }
		public bool EnableObfuscation { get; set; }
		public bool ValidateProguardMappingFileInput { get; set; }
		public string? ProguardMappingRequiredEntriesFile { get; set; }
		public string? ProguardMappingRequiredReachabilityEntriesFile { get; set; }

		// User-authored AndroidJavaSource (Bind != true) .java files. These have no managed peer and are
		// therefore absent from the acw-map, so they must be kept explicitly when shrinking is enabled.
		public ITaskItem []? JavaSourceFiles { get; set; }

		protected override string MainClass => "com.android.tools.r8.R8";

		readonly List<string> tempFiles = new List<string> ();

		public override bool RunTask ()
		{
			try {
				bool result = base.RunTask ();
				if (result && ValidateProguardMappingFileInput) {
					ValidateAppliedMapping ();
				}
				return result && !Log.HasLoggedErrors;
			} finally {
				foreach (var temp in tempFiles) {
					File.Delete (temp);
				}
			}

			void ValidateAppliedMapping ()
			{
				if (ProguardMappingFileInput.IsNullOrEmpty () || !File.Exists (ProguardMappingFileInput)) {
					Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
						$"The R8 JNI seed mapping file '{ProguardMappingFileInput}' was not found.");
					return;
				}
				if (ProguardMappingFileOutput.IsNullOrEmpty () || !File.Exists (ProguardMappingFileOutput)) {
					Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
						$"The final R8 mapping file '{ProguardMappingFileOutput}' was not generated, so the applied JNI names could not be validated.");
					return;
				}
				if (ProguardMappingRequiredEntriesFile.IsNullOrEmpty () || !File.Exists (ProguardMappingRequiredEntriesFile)) {
					Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
						$"The R8 JNI rewrite manifest '{ProguardMappingRequiredEntriesFile}' was not found.");
					return;
				}
				if (ProguardMappingRequiredReachabilityEntriesFile.IsNullOrEmpty () || !File.Exists (ProguardMappingRequiredReachabilityEntriesFile)) {
					Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
						$"The post-link R8 JNI reachability manifest '{ProguardMappingRequiredReachabilityEntriesFile}' was not found.");
					return;
				}

				R8Mapping seedMapping = R8Mapping.Load (ProguardMappingFileInput);
				R8Mapping finalMapping = R8Mapping.Load (ProguardMappingFileOutput);
				LogMappingConflicts (
					seedMapping.GetCompatibilityConflicts (finalMapping, File.ReadLines (ProguardMappingRequiredEntriesFile)),
					"The final R8 mapping did not preserve the JNI seed mapping for ",
					"additional conflicts with managed JNI names");
				LogMappingConflicts (
					seedMapping.GetReachabilityConflicts (finalMapping, File.ReadLines (ProguardMappingRequiredReachabilityEntriesFile)),
					"Final R8 removed a post-link reachable JNI ",
					"additional post-link reachable JNI entries removed by final R8");
			}

			void LogMappingConflicts (IEnumerable<string> conflicts, string prefix, string overflowDescription)
			{
				int conflictCount = 0;
				foreach (string conflict in conflicts) {
					conflictCount++;
					if (conflictCount <= 20) {
						Log.LogCodedError ("XA4307", Properties.Resources.XA4307, prefix + conflict + ".");
					}
				}
				if (conflictCount > 20) {
					Log.LogCodedError ("XA4307", Properties.Resources.XA4307,
						$"The final R8 mapping contains {conflictCount - 20} {overflowDescription}.");
				}
			}
		}

		// Derive the fully-qualified Java type name from each user .java source file. Java requires the
		// public top-level type name to match the file name, so '<package>.<FileNameWithoutExtension>' is
		// the type to keep. Files that no longer exist are skipped. Only the public top-level type is kept;
		// secondary/non-public types in the same file rely on the public type's '{ *; }' or being unused.
		IEnumerable<string> GetUserJavaTypes ()
		{
			if (JavaSourceFiles == null) {
				yield break;
			}
			var seen = new HashSet<string> (StringComparer.Ordinal);
			var javaTypes = new List<string> ();
			foreach (var item in JavaSourceFiles) {
				var path = item.ItemSpec;
				if (path.IsNullOrEmpty () || !File.Exists (path)) {
					continue;
				}
				var typeName = Path.GetFileNameWithoutExtension (path);
				var package = ReadJavaPackage (path);
				if (!package.IsNullOrEmpty ()) {
					typeName = $"{package}.{typeName}";
				}
				if (seen.Add (typeName)) {
					javaTypes.Add (typeName);
				}
			}
			javaTypes.Sort (StringComparer.Ordinal);
			foreach (var javaType in javaTypes) {
				yield return javaType;
			}
		}

		internal static string? ReadJavaPackage (string path)
		{
			foreach (var raw in File.ReadLines (path)) {
				var line = raw.Trim ();
				if (line.Length == 0 || line.StartsWith ("//", StringComparison.Ordinal) || line.StartsWith ("*", StringComparison.Ordinal) || line.StartsWith ("/*", StringComparison.Ordinal)) {
					continue;
				}
				if (line.StartsWith ("package ", StringComparison.Ordinal)) {
					var end = line.IndexOf (';');
					if (end > "package ".Length) {
						return line.Substring ("package ".Length, end - "package ".Length).Trim ();
					}
				}
				// The package declaration, if present, must precede any type declaration. This is a
				// lightweight scan (not a full Java parser): the first 'import'/type keyword ends the
				// search, and earlier comment lines are skipped, so package always wins in practice.
				if (line.StartsWith ("import ", StringComparison.Ordinal) || line.Contains ("class ") || line.Contains ("interface ") || line.Contains ("enum ")) {
					break;
				}
			}
			return null;
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

			if (GenerateSeedMapping) {
				WriteArg (response, "--no-tree-shaking");
				var seedConfiguration = new List<string> {
					"-dontoptimize",
					"-dontpreverify",
					"-keepattributes **",
					$"-printmapping \"{Path.GetFullPath (ProguardMappingFileOutput ?? throw new InvalidOperationException ("ProguardMappingFileOutput is required when GenerateSeedMapping is enabled."))}\"",
				};
				if (IgnoreWarnings) {
					seedConfiguration.Add ("-ignorewarnings");
				}
				WriteConfiguration (response, seedConfiguration);
				GenerateApplicationConfiguration ();
				if (!ProguardGeneratedApplicationConfiguration.IsNullOrEmpty ()) {
					WriteArg (response, "--pg-conf");
					WriteArg (response, ProguardGeneratedApplicationConfiguration);
				}
				GenerateCommonXamarinConfiguration ();
				if (!ProguardCommonXamarinConfiguration.IsNullOrEmpty ()) {
					WriteArg (response, "--pg-conf");
					WriteArg (response, ProguardCommonXamarinConfiguration);
				}
			} else if (EnableShrinking) {
				if (UseTrimmableNativeAotProguardConfiguration && !ProguardGeneratedApplicationConfiguration.IsNullOrEmpty ()) {
					// ACW keep rules come from the DGML/acw-map-driven proguard_project_references.cfg on
					// the trimmable path. User-authored AndroidJavaSource (Bind != true) has no managed peer
					// and is absent from that map, so keep it here explicitly; otherwise R8 shrinks it away
					// (e.g. dropping large unreferenced sources so an app that needs multidex no longer does).
					using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration)) {
						appcfg.WriteLine ("# ACW keep rules are generated from NativeAOT ILC metadata.");
						foreach (var java in GetUserJavaTypes ()) {
							appcfg.WriteLine ($"-keep class {java} {{ *; }}");
						}
					}
				} else if (!AcwMapFile.IsNullOrEmpty ()) {
					var acwMap      = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (AcwMapFile), StringComparer.OrdinalIgnoreCase);
					var javaTypes = new List<string> (new HashSet<string> (acwMap.Values, StringComparer.Ordinal));
					javaTypes.Sort (StringComparer.Ordinal);
					using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration)) {
						foreach (var java in javaTypes) {
							appcfg.WriteLine ($"-keep class {java} {{ *; }}");
						}
						// User-authored AndroidJavaSource (Bind != true) has no managed peer and is absent
						// from the acw-map, so keep it explicitly; otherwise shrinking removes it.
						foreach (var java in GetUserJavaTypes ()) {
							appcfg.WriteLine ($"-keep class {java} {{ *; }}");
						}
					}
				}
				GenerateCommonXamarinConfiguration ();
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
			if (!ProguardMappingFileInput.IsNullOrEmpty ()) {
				WriteConfiguration (response, new [] {
					$"-applymapping \"{Path.GetFullPath (ProguardMappingFileInput)}\"",
				});
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

		void GenerateApplicationConfiguration ()
		{
			if (AcwMapFile.IsNullOrEmpty () || ProguardGeneratedApplicationConfiguration.IsNullOrEmpty ()) {
				return;
			}

			var acwMap = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (AcwMapFile), StringComparer.OrdinalIgnoreCase);
			var javaTypes = new List<string> (new HashSet<string> (acwMap.Values, StringComparer.Ordinal));
			javaTypes.Sort (StringComparer.Ordinal);
			using var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration);
			foreach (var java in javaTypes) {
				appcfg.WriteLine ($"-keep class {java} {{ *; }}");
			}
		}

		void GenerateCommonXamarinConfiguration ()
		{
			if (ProguardCommonXamarinConfiguration.IsNullOrWhiteSpace ()) {
				return;
			}

			using var xamcfg = File.CreateText (ProguardCommonXamarinConfiguration);
			string resourceName = UseTrimmableNativeAotProguardConfiguration ? "proguard_trimmable_nativeaot.cfg" : "proguard_xamarin.cfg";
			using (Stream resource = GetEmbeddedResourceStream (resourceName))
			using (var reader = new StreamReader (resource)) {
				while (reader.ReadLine () is string line) {
					if (EnableObfuscation && String.Equals (line.Trim (), "-dontobfuscate", StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					xamcfg.WriteLine (line);
				}
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

		void WriteConfiguration (StreamWriter response, IEnumerable<string> lines)
		{
			var temp = Path.GetTempFileName ();
			File.WriteAllLines (temp, lines);
			tempFiles.Add (temp);
			WriteArg (response, "--pg-conf");
			WriteArg (response, temp);
		}

		// ProGuard "global" options that affect the whole build and are not allowed inside
		// a library's proguard.txt (the file packaged inside an .aar's root). AGP 9.0
		// introduced the same restriction — see "Behavior changes" in the AGP 9.0 release
		// notes:
		//   https://developer.android.com/build/releases/agp-9-0-0-release-notes#behavior-changes
		// We skip the whole offending file and emit a warning naming the source library
		// so the build can still succeed.
		static readonly string [] DisallowedLibraryProguardOptions = {
			"-dontobfuscate",
			"-dontoptimize",
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
				if (trimmed.Length < candidate.Length)
					continue;
				if (!trimmed.StartsWith (candidate, StringComparison.OrdinalIgnoreCase))
					continue;
				// Require an end-of-token boundary so "-printmappingFoo" does not match "-printmapping".
				if (trimmed.Length == candidate.Length || char.IsWhiteSpace (trimmed [candidate.Length])) {
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
