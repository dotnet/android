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
	public class ResolveAndroidTooling : AndroidTask
	{
		public override string TaskPrefix => "RAT";

		public string AndroidSdkPath { get; set; }

		public string AndroidSdkBuildToolsVersion { get; set; }

		public string CommandLineToolsVersion { get; set; }

		public string ProjectFilePath { get; set; }

		public string SequencePointsMode { get; set; }

		public bool AotAssemblies { get; set; }

		public bool AndroidApplication { get; set; } = true;

		public bool AndroidUseAapt2 { get; set; }

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
		public string Aapt2Version { get; set; }

		[Output]
		public string Aapt2ToolPath { get; set; }

		protected static readonly bool IsWindows = Path.DirectorySeparatorChar == '\\';
		protected static readonly string ZipAlign = IsWindows ? "zipalign.exe" : "zipalign";
		protected static readonly string Aapt = IsWindows ? "aapt.exe" : "aapt";
		protected static readonly string Aapt2 = IsWindows ? "aapt2.exe" : "aapt2";
		protected static readonly string Android = IsWindows ? "android.bat" : "android";
		protected static readonly string Lint = IsWindows ? "lint.bat" : "lint";
		protected static readonly string ApkSigner = "apksigner.jar";

		public override bool RunTask ()
		{
			AndroidApiLevel = GetMaxStableApiLevel ().ToString ();

			string toolsZipAlignPath = Path.Combine (AndroidSdkPath, "tools", ZipAlign);
			bool findZipAlign = (string.IsNullOrEmpty (ZipAlignPath) || !Directory.Exists (ZipAlignPath)) && !File.Exists (toolsZipAlignPath);

			var commandLineToolsDir = MonoAndroidHelper.AndroidSdk.GetCommandLineToolsPaths (CommandLineToolsVersion)
				.FirstOrDefault () ?? "";

			var lintPaths = new string [] {
				LintToolPath ?? string.Empty,
				commandLineToolsDir,
				Path.Combine (commandLineToolsDir, "bin"),
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
				Log.LogCodedError ("XA5205", Properties.Resources.XA5205,
						Aapt, AndroidSdkPath, Path.DirectorySeparatorChar, Android);
				return false;
			}

			if (AndroidUseAapt2) {
				if (string.IsNullOrEmpty (Aapt2ToolPath)) {
					var osBinPath = MonoAndroidHelper.GetOSBinPath ();
					var aapt2 = Path.Combine (osBinPath, Aapt2);
					if (File.Exists (aapt2))
						Aapt2ToolPath = osBinPath;
				}
				if (string.IsNullOrEmpty (Aapt2ToolPath) || !File.Exists (Path.Combine (Aapt2ToolPath, Aapt2))) {
					Log.LogCodedError ("XA0112", Properties.Resources.XA0112, Aapt2ToolPath);
				} else if (!GetAapt2Version ()) {
					Log.LogCodedError ("XA0111", Properties.Resources.XA0111, Aapt2ToolPath);
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
				Log.LogCodedError ("XA5205", Properties.Resources.XA5205,
						ZipAlign, AndroidSdkPath, Path.DirectorySeparatorChar, Android);
				return false;
			}

			if (!Validate ())
				return false;

			SequencePointsMode mode;
			if (!Aot.TryGetSequencePointsMode (SequencePointsMode ?? "None", out mode))
				Log.LogCodedError ("XA0104", Properties.Resources.XA0104, SequencePointsMode);
			AndroidSequencePointsMode = mode.ToString ();

			AndroidApiLevelName = MonoAndroidHelper.SupportedVersions.GetIdFromApiLevel (AndroidApiLevel);

			LogOutputs ();

			return !Log.HasLoggedErrors;
		}

		protected virtual bool Validate () => !string.IsNullOrEmpty (AndroidApiLevel);

		protected virtual void LogOutputs ()
		{
			Log.LogDebugMessage ($"{nameof (ResolveAndroidTooling)} Outputs:");
			Log.LogDebugMessage ($"  {nameof (AndroidApiLevel)}: {AndroidApiLevel}");
			Log.LogDebugMessage ($"  {nameof (AndroidApiLevelName)}: {AndroidApiLevelName}");
			Log.LogDebugMessage ($"  {nameof (AndroidSdkBuildToolsPath)}: {AndroidSdkBuildToolsPath}");
			Log.LogDebugMessage ($"  {nameof (AndroidSdkBuildToolsBinPath)}: {AndroidSdkBuildToolsBinPath}");
			Log.LogDebugMessage ($"  {nameof (ZipAlignPath)}: {ZipAlignPath}");
			Log.LogDebugMessage ($"  {nameof (AndroidSequencePointsMode)}: {AndroidSequencePointsMode}");
			Log.LogDebugMessage ($"  {nameof (LintToolPath)}: {LintToolPath}");
			Log.LogDebugMessage ($"  {nameof (Aapt2Version)}: {Aapt2Version}");
			Log.LogDebugMessage ($"  {nameof (Aapt2ToolPath)}: {Aapt2ToolPath}");
		}

		//  Android Asset Packaging Tool (aapt) 2:19
		//  Android Asset Packaging Tool (aapt) 2.19-6051327
		static readonly Regex Aapt2VersionRegex = new Regex (@"(?<version>[\d\:|\.]+)(\d+)?");

		bool GetAapt2Version ()
		{
			var sb = new StringBuilder ();
			var aapt2Tool = Path.Combine (Aapt2ToolPath, Aapt2);

			// Try to use a cached value for Aapt2Version
			// NOTE: this doesn't need to use GetRegisteredTaskObjectAssemblyLocal()
			// because the path to aapt2 is in the key and the value is a string.
			var key = ($"{nameof (ResolveAndroidTooling)}.{nameof (Aapt2Version)}", aapt2Tool);
			var cached = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.AppDomain) as string;
			if (!string.IsNullOrEmpty (cached)) {
				Log.LogDebugMessage ($"Using cached value for {nameof (Aapt2Version)}: {cached}");
				Aapt2Version = cached;
				return true;
			}

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
				BuildEngine4.RegisterTaskObject (key, Aapt2Version, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: false);
				return true;
			}
			return false;
		}

		protected int GetMaxStableApiLevel ()
		{
			return MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
		}
	}
}
