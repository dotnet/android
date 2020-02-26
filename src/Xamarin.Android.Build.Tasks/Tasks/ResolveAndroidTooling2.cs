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
	/// ResolveAndroidTooling2 does lot of the grunt work ResolveSdks used to do:
	/// - Modify TargetFrameworkVersion
	/// - Find the paths of various Android tooling that other tasks need to call
	/// </summary>
	public class ResolveAndroidTooling2 : AndroidTask
	{
		public override string TaskPrefix => "RAT";

		public string AndroidSdkPath { get; set; }

		public string AndroidSdkBuildToolsVersion { get; set; }

		public string ProjectFilePath { get; set; }

		public string SequencePointsMode { get; set; }

		public bool AotAssemblies { get; set; }

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

		[Output]
		public string Aapt2ToolPath { get; set; }

		static readonly bool IsWindows = Path.DirectorySeparatorChar == '\\';
		static readonly string ZipAlign = IsWindows ? "zipalign.exe" : "zipalign";
		static readonly string Aapt = IsWindows ? "aapt.exe" : "aapt";
		static readonly string Aapt2 = IsWindows ? "aapt2.exe" : "aapt2";
		static readonly string Android = IsWindows ? "android.bat" : "android";
		static readonly string Lint = IsWindows ? "lint.bat" : "lint";
		static readonly string ApkSigner = "apksigner.jar";

		public override bool RunTask ()
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
				Log.LogCodedError ("XA5205", Properties.Resources.XA5205,
						Aapt, AndroidSdkPath, Path.DirectorySeparatorChar, Android);
				return false;
			}

			ApkSignerJar = Path.Combine (AndroidSdkBuildToolsBinPath, "lib", ApkSigner);
			AndroidUseApkSigner = File.Exists (ApkSignerJar);

			if (string.IsNullOrEmpty (Aapt2ToolPath)) {
				var osBinPath = MonoAndroidHelper.GetOSBinPath ();
				var aapt2 = Path.Combine (osBinPath, Aapt2);
				if (File.Exists (aapt2))
					Aapt2ToolPath = osBinPath;
			}

			bool aapt2Installed = !string.IsNullOrEmpty (Aapt2ToolPath) && File.Exists (Path.Combine (Aapt2ToolPath, Aapt2));
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
				Log.LogCodedError ("XA5205", Properties.Resources.XA5205,
						ZipAlign, AndroidSdkPath, Path.DirectorySeparatorChar, Android);
				return false;
			}

			SequencePointsMode mode;
			if (!Aot.TryGetSequencePointsMode (SequencePointsMode ?? "None", out mode))
				Log.LogCodedError ("XA0104", "Invalid Sequence Point mode: {0}", SequencePointsMode);
			AndroidSequencePointsMode = mode.ToString ();

			Log.LogDebugMessage ($"{nameof (ResolveAndroidTooling2)} Outputs:");
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
			var aapt2Tool = Path.Combine (Aapt2ToolPath, Aapt2);

			// Try to use a cached value for Aapt2Version
			var key = ($"{nameof (ResolveAndroidTooling2)}.{nameof (Aapt2Version)}", aapt2Tool);
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
	}
}
