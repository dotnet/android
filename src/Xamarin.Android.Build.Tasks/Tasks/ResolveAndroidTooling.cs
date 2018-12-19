using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// ResolveAndroidTooling does lot of the grunt work ResolveSdks used to do:
	/// - Modify TargetFrameworkVersion
	/// - Calculate ApiLevel and ApiLevelName
	/// - Find the paths of various Android tooling that other tasks need to call
	/// </summary>
	public class ResolveAndroidTooling : Task
	{
		public string AndroidSdkPath { get; set; }

		public string AndroidSdkBuildToolsVersion { get; set; }

		public string ProjectFilePath { get; set; }

		public string SequencePointsMode { get; set; }

		public bool UseLatestAndroidPlatformSdk { get; set; }

		public bool AotAssemblies { get; set; }

		[Output]
		public string TargetFrameworkVersion { get; set; }

		[Output]
		public string AndroidApiLevel { get; set; }

		[Output]
		public string AndroidApiLevelName { get; set; }

		[Output]
		public string AndroidSdkBuildToolsPath { get; set; }

		[Output]
		public string AndroidSdkBuildToolsBinPath { get; set; }

		[Output]
		public string ZipAlignPath { get; set; }

		[Output]
		public string AndroidSequencePointsMode { get; set; }

		[Output]
		public string LintToolPath { get; set; }

		[Output]
		public string ApkSignerJar { get; set; }

		[Output]
		public bool AndroidUseApkSigner { get; set; }

		[Output]
		public bool AndroidUseAapt2 { get; set; }

		[Output]
		public string Aapt2Version { get; set; }

		static readonly bool IsWindows = Path.DirectorySeparatorChar == '\\';
		static readonly string ZipAlign = IsWindows ? "zipalign.exe" : "zipalign";
		static readonly string Aapt = IsWindows ? "aapt.exe" : "aapt";
		static readonly string Aapt2 = IsWindows ? "aapt2.exe" : "aapt2";
		static readonly string Android = IsWindows ? "android.bat" : "android";
		static readonly string Lint = IsWindows ? "lint.bat" : "lint";
		static readonly string ApkSigner = "apksigner.jar";

		public override bool Execute ()
		{
			string toolsZipAlignPath = Path.Combine (AndroidSdkPath, "tools", ZipAlign);
			bool findZipAlign = (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) && !File.Exists (toolsZipAlignPath);

			var lintPaths = new string [] {
				LintToolPath ?? string.Empty,
				Path.Combine (AndroidSdkPath, "tools"),
				Path.Combine (AndroidSdkPath, "tools", "bin"),
			};

			LintToolPath = null;
			foreach (var path in lintPaths) {
				if (File.Exists (Path.Combine (path, Lint))) {
					LintToolPath = path;
					break;
				}
			}

			foreach (var dir in MonoAndroidHelper.AndroidSdk.GetBuildToolsPaths (AndroidSdkBuildToolsVersion)) {
				Log.LogDebugMessage ("Trying build-tools path: {0}", dir);
				if (dir == null || !Directory.Exists (dir))
					continue;

				var toolsPaths = new string [] {
					Path.Combine (dir),
					Path.Combine (dir, "bin"),
				};

				string aapt = toolsPaths.FirstOrDefault (x => File.Exists (Path.Combine (x, Aapt)));
				if (string.IsNullOrEmpty (aapt)) {
					Log.LogDebugMessage ("Could not find `{0}`; tried: {1}", Aapt,
						string.Join (Path.PathSeparator.ToString (), toolsPaths.Select (x => Path.Combine (x, Aapt))));
					continue;
				}
				AndroidSdkBuildToolsPath = Path.GetFullPath (dir);
				AndroidSdkBuildToolsBinPath = Path.GetFullPath (aapt);

				string zipalign = toolsPaths.FirstOrDefault (x => File.Exists (Path.Combine (x, ZipAlign)));
				if (findZipAlign && string.IsNullOrEmpty (zipalign)) {
					Log.LogDebugMessage ("Could not find `{0}`; tried: {1}", ZipAlign,
						string.Join (Path.PathSeparator.ToString (), toolsPaths.Select (x => Path.Combine (x, ZipAlign))));
					continue;
				} else
					break;
			}

			if (string.IsNullOrEmpty (AndroidSdkBuildToolsPath)) {
				Log.LogCodedError ("XA5205",
						string.Format (
							"Cannot find `{0}`. Please install the Android SDK Build-tools package with the `{1}{2}tools{2}{3}` program.",
							Aapt, AndroidSdkPath, Path.DirectorySeparatorChar, Android));
				return false;
			}

			ApkSignerJar = Path.Combine (AndroidSdkBuildToolsBinPath, "lib", ApkSigner);
			AndroidUseApkSigner = File.Exists (ApkSignerJar);

			bool aapt2Installed = File.Exists (Path.Combine (AndroidSdkBuildToolsBinPath, Aapt2));
			if (aapt2Installed && AndroidUseAapt2) {
				if (!GetAapt2Version ()) {
					AndroidUseAapt2 = false;
					aapt2Installed = false;
					Log.LogCodedWarning ("XA0111", "Could not get the `aapt2` version. Disabling `aapt2` support. Please check it is installed correctly.");
				}
			}
			if (AndroidUseAapt2) {
				if (!aapt2Installed) {
					AndroidUseAapt2 = false;
					Log.LogCodedWarning ("XA0112", "`aapt2` is not installed. Disabling `aapt2` support. Please check it is installed correctly.");
				}
			}

			if (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) {
				ZipAlignPath = new [] {
						Path.Combine (AndroidSdkBuildToolsPath),
						Path.Combine (AndroidSdkBuildToolsBinPath),
						Path.Combine (AndroidSdkPath, "tools"),
					}
					.Where (p => File.Exists (Path.Combine (p, ZipAlign)))
					.FirstOrDefault ();
			}
			if (string.IsNullOrEmpty (ZipAlignPath)) {
				Log.LogCodedError ("XA5205",
						string.Format (
							"Cannot find `{0}`. Please install the Android SDK Build-tools package with the `{1}{2}tools{2}{3}` program.",
							ZipAlign, AndroidSdkPath, Path.DirectorySeparatorChar, Android));
				return false;
			}

			if (!ValidateApiLevels ())
				return false;

			if (!MonoAndroidHelper.SupportedVersions.FrameworkDirectories.Any (p => Directory.Exists (Path.Combine (p, TargetFrameworkVersion)))) {
				Log.LogError (
					subcategory: string.Empty,
					errorCode: "XA0001",
					helpKeyword: string.Empty,
					file: ProjectFilePath,
					lineNumber: 0,
					columnNumber: 0,
					endLineNumber: 0,
					endColumnNumber: 0,
					message: "Unsupported or invalid $(TargetFrameworkVersion) value of '{0}'. Please update your Project Options.",
					messageArgs: new []{
						TargetFrameworkVersion,
					}
				);
				return false;
			}

			int apiLevel;
			if (int.TryParse (AndroidApiLevel, out apiLevel)) {
				if (apiLevel < 26)
					Log.LogCodedWarning ("XA0113", $"Google Play requires that new applications must use a TargetFrameworkVersion of v8.0 (API level 26) or above. You are currently targeting {TargetFrameworkVersion} (API level {AndroidApiLevel}).");
				if (apiLevel < 26)
					Log.LogCodedWarning ("XA0114", $"Google Play requires that application updates must use a TargetFrameworkVersion of v8.0 (API level 26) or above. You are currently targeting {TargetFrameworkVersion} (API level {AndroidApiLevel}).");
				if (apiLevel < 19)
					Log.LogCodedWarning ("XA0117", $"The TargetFrameworkVersion {TargetFrameworkVersion} is deprecated. Please update it to be v4.4 or higher.");
			}

			SequencePointsMode mode;
			if (!Aot.TryGetSequencePointsMode (SequencePointsMode ?? "None", out mode))
				Log.LogCodedError ("XA0104", "Invalid Sequence Point mode: {0}", SequencePointsMode);
			AndroidSequencePointsMode = mode.ToString ();

			AndroidApiLevelName = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (AndroidApiLevel);

			Log.LogDebugMessage ($"{nameof (ResolveAndroidTooling)} Outputs:");
			Log.LogDebugMessage ($"  {nameof (TargetFrameworkVersion)}: {TargetFrameworkVersion}");
			Log.LogDebugMessage ($"  {nameof (AndroidApiLevel)}: {AndroidApiLevel}");
			Log.LogDebugMessage ($"  {nameof (AndroidApiLevelName)}: {AndroidApiLevelName}");
			Log.LogDebugMessage ($"  {nameof (AndroidSdkBuildToolsPath)}: {AndroidSdkBuildToolsPath}");
			Log.LogDebugMessage ($"  {nameof (AndroidSdkBuildToolsBinPath)}: {AndroidSdkBuildToolsBinPath}");
			Log.LogDebugMessage ($"  {nameof (ZipAlignPath)}: {ZipAlignPath}");
			Log.LogDebugMessage ($"  {nameof (AndroidSequencePointsMode)}: {AndroidSequencePointsMode}");
			Log.LogDebugMessage ($"  {nameof (LintToolPath)}: {LintToolPath}");
			Log.LogDebugMessage ($"  {nameof (ApkSignerJar)}: {ApkSignerJar}");
			Log.LogDebugMessage ($"  {nameof (AndroidUseApkSigner)}: {AndroidUseApkSigner}");
			Log.LogDebugMessage ($"  {nameof (AndroidUseAapt2)}: {AndroidUseAapt2}");
			Log.LogDebugMessage ($"  {nameof (Aapt2Version)}: {Aapt2Version}");

			return !Log.HasLoggedErrors;
		}

		//  Android Asset Packaging Tool (aapt) 2:19
		static readonly Regex Aapt2VersionRegex = new Regex (@"(?<version>[\d\:]+)(\d+)?");

		bool GetAapt2Version ()
		{
			var sb = new StringBuilder ();
			var aapt2Tool = Path.Combine (AndroidSdkBuildToolsBinPath, Aapt2);
			try {
				MonoAndroidHelper.RunProcess (aapt2Tool, "version", (s, e) => {
					if (!string.IsNullOrEmpty (e.Data))
						sb.AppendLine (e.Data);
				}, (s, e) => {
					if (!string.IsNullOrEmpty (e.Data))
						sb.AppendLine (e.Data);
				}
				);
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				return false;
			}
			var versionInfo = sb.ToString ();
			var versionNumberMatch = Aapt2VersionRegex.Match (versionInfo);
			Log.LogDebugMessage ($"`{aapt2Tool} version` returned: ```{versionInfo}```");
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value.Replace (":", "."), out Version versionNumber)) {
				Aapt2Version = versionNumber.ToString ();
				return true;
			}
			return false;
		}

		bool ValidateApiLevels ()
		{
			// Priority:
			//    $(UseLatestAndroidPlatformSdk) > $(AndroidApiLevel) > $(TargetFrameworkVersion)
			//
			// If $(TargetFrameworkVersion) isn't set, and $(AndroidApiLevel) isn't
			// set, act as if $(UseLatestAndroidPlatformSdk) is True
			//
			// If $(UseLatestAndroidPlatformSdk) is true, we do as it says: use the
			// latest installed version.
			//
			// Otherwise, if $(AndroidApiLevel) is set, use it and set $(TargetFrameworkVersion).
			//    Rationale: monodroid/samples/xbuild.make uses $(AndroidApiLevel)
			//    to build for a specific API level.
			// Otherwise, if $(TargetFrameworkVersion) is set, use it and set $(AndroidApiLevel).

			UseLatestAndroidPlatformSdk = UseLatestAndroidPlatformSdk ||
				(string.IsNullOrWhiteSpace (AndroidApiLevel) && string.IsNullOrWhiteSpace (TargetFrameworkVersion));

			if (UseLatestAndroidPlatformSdk) {
				int maxInstalled = GetMaxInstalledApiLevel ();
				int maxSupported = GetMaxStableApiLevel ();
				AndroidApiLevel = maxInstalled.ToString ();
				if (maxInstalled > maxSupported) {
					Log.LogDebugMessage ($"API Level {maxInstalled} is greater than the maximum supported API level of {maxSupported}. " +
						"Support for this API will be added in a future release.");
				}
				if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
					var userSelected = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
					// overwrite using user version only if it is 
					// above the maxStableApi and a valid apiLevel.
					if (userSelected != null && userSelected > maxSupported && userSelected <= maxInstalled) {
						maxInstalled =
							maxSupported = userSelected.Value;
						AndroidApiLevel = userSelected.ToString ();
					}
				}

				for (int apiLevel = maxSupported; apiLevel >= MonoAndroidHelper.SupportedVersions.MinStableVersion.ApiLevel; apiLevel--) {
					var id = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (apiLevel);
					var apiPlatformDir = MonoAndroidHelper.AndroidSdk.TryGetPlatformDirectoryFromApiLevel (id, MonoAndroidHelper.SupportedVersions);
					if (apiPlatformDir != null && Directory.Exists (apiPlatformDir)) {
						var targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (id);
						if (targetFramework != null && MonoAndroidHelper.SupportedVersions.InstalledBindingVersions.Any (b => b.FrameworkVersion == targetFramework)) {
							AndroidApiLevel = apiLevel.ToString ();
							TargetFrameworkVersion = targetFramework;
							break;
						}
					}
				}
				return TargetFrameworkVersion != null;
			}

			if (!string.IsNullOrWhiteSpace (TargetFrameworkVersion)) {
				TargetFrameworkVersion = TargetFrameworkVersion.Trim ();
				string id = MonoAndroidHelper.SupportedVersions.GetIdFromFrameworkVersion (TargetFrameworkVersion);
				if (id == null) {
					Log.LogCodedError ("XA0000",
							"Could not determine API level for $(TargetFrameworkVersion) of '{0}'.",
							TargetFrameworkVersion);
					return false;
				}
				AndroidApiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id).ToString ();
				return true;
			}

			if (!string.IsNullOrWhiteSpace (AndroidApiLevel)) {
				AndroidApiLevel = AndroidApiLevel.Trim ();
				TargetFrameworkVersion = GetTargetFrameworkVersionFromApiLevel ();
				return TargetFrameworkVersion != null;
			}

			Log.LogCodedError ("XA0000", "Could not determine $(AndroidApiLevel) or $(TargetFrameworkVersion); SHOULD NOT BE REACHED.");
			return false;
		}

		int GetMaxInstalledApiLevel ()
		{
			int maxApiLevel = int.MinValue;
			string platformsDir = Path.Combine (AndroidSdkPath, "platforms");
			if (Directory.Exists (platformsDir)) {
				var apiIds = Directory.EnumerateDirectories (platformsDir)
					.Select (platformDir => Path.GetFileName (platformDir))
					.Where (dir => dir.StartsWith ("android-", StringComparison.OrdinalIgnoreCase))
					.Select (dir => dir.Substring ("android-".Length))
					.Select (apiName => MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (apiName));
				foreach (var id in apiIds) {
					int? v = MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (id);
					if (!v.HasValue)
						continue;
					maxApiLevel = Math.Max (maxApiLevel, v.Value);
				}
			}
			if (maxApiLevel < 0)
				Log.LogCodedError ("XA5300",
						"No Android platforms installed at '{0}'. Please install an SDK Platform with the `{1}{2}tools{2}{3}` program.",
						platformsDir, Path.DirectorySeparatorChar, Android);
			return maxApiLevel;
		}

		int GetMaxStableApiLevel ()
		{
			return MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
		}

		string GetMaxSupportedApiLevel (string apiLevel)
		{
			int level = 0;
			if (!int.TryParse (apiLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
				return apiLevel;
			var referenceAssemblyPaths = MonoAndroidHelper.TargetFrameworkDirectories;
			if (referenceAssemblyPaths == null)
				return apiLevel;
			foreach (string versionedDir in referenceAssemblyPaths) {
				string parent = Path.GetDirectoryName (versionedDir.TrimEnd (Path.DirectorySeparatorChar));
				for (int l = level; l > 0; l--) {
					string tfv = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (l);
					if (tfv == null)
						continue;
					string dir = Path.Combine (parent, tfv);
					if (Directory.Exists (dir))
						return l.ToString ();
				}
			}
			return apiLevel;
		}

		string GetTargetFrameworkVersionFromApiLevel ()
		{
			string targetFramework = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromId (AndroidApiLevel);
			if (targetFramework != null)
				return targetFramework;
			Log.LogCodedError ("XA0000",
					"Could not determine $(TargetFrameworkVersion) for API level '{0}.'",
					AndroidApiLevel);
			return null;
		}
	}
}
