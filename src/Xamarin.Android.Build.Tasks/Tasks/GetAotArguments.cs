using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Java.Interop.Tools.Diagnostics;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// The <Aot/> task subclasses this in "legacy" Xamarin.Android.
	/// Called directly in .NET 5 to populate %(AotArguments) metadata.
	/// </summary>
	public class GetAotArguments : AndroidAsyncTask
	{
		public override string TaskPrefix => "GAOT";

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public string AndroidAotMode { get; set; }

		[Required]
		public string AotOutputDirectory { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		/// <summary>
		/// Will be blank in .NET 6+
		/// </summary>
		public string ManifestFile { get; set; }

		/// <summary>
		/// $(AndroidMinimumSupportedApiLevel) in .NET 6+
		/// </summary>
		public string MinimumSupportedApiLevel { get; set; }

		public string RuntimeIdentifier { get; set; }

		public string AndroidNdkDirectory { get; set; }

		public bool EnableLLVM { get; set; }

		public string AndroidSequencePointsMode { get; set; }

		public ITaskItem [] Profiles { get; set; }

		public bool UsingAndroidNETSdk { get; set; }

		public string AotAdditionalArguments { get; set; }

		[Output]
		public string Arguments { get; set; }

		[Output]
		public string OutputDirectory { get; set; }

		protected AotMode AotMode;
		protected SequencePointsMode SequencePointsMode;
		protected string SdkBinDirectory;

		public static bool GetAndroidAotMode(string androidAotMode, out AotMode aotMode)
		{
			aotMode = AotMode.Normal;

			switch ((androidAotMode ?? string.Empty).ToLowerInvariant().Trim())
			{
			case "":
			case "none":
				aotMode = AotMode.None;
				return true;
			case "normal":
				aotMode = AotMode.Normal;
				return true;
			case "hybrid":
				aotMode = AotMode.Hybrid;
				return true;
			case "full":
				aotMode = AotMode.Full;
				return true;
			case "interpreter":
				// We don't do anything here for this mode, this is just to set the flag for the XA
				// runtime to initialize Mono in the interpreter "AOT" mode.
				aotMode = AotMode.Interp;
				return true;
			}

			return false;
		}

		public static bool TryGetSequencePointsMode (string value, out SequencePointsMode mode)
		{
			mode = SequencePointsMode.None;
			switch ((value ?? string.Empty).ToLowerInvariant ().Trim ()) {
			case "none":
				mode = SequencePointsMode.None;
				return true;
			case "normal":
				mode = SequencePointsMode.Normal;
				return true;
			case "offline":
				mode = SequencePointsMode.Offline;
				return true;
			}
			return false;
		}

		public override Task RunTaskAsync ()
		{
			NdkTools? ndk = NdkTools.Create (AndroidNdkDirectory, Log);
			if (ndk == null) {
				return Task.CompletedTask; // NdkTools.Create will log appropriate error
			}

			bool hasValidAotMode = GetAndroidAotMode (AndroidAotMode, out AotMode);
			if (!hasValidAotMode) {
				LogCodedError ("XA3002", Properties.Resources.XA3002, AndroidAotMode);
				return Task.CompletedTask;
			}

			if (AotMode == AotMode.Interp) {
				LogDebugMessage ("Interpreter AOT mode enabled");
				return Task.CompletedTask;
			}

			TryGetSequencePointsMode (AndroidSequencePointsMode, out SequencePointsMode);

			SdkBinDirectory = MonoAndroidHelper.GetOSBinPath ();

			var abi = AndroidRidAbiHelper.RuntimeIdentifierToAbi (RuntimeIdentifier);
			if (string.IsNullOrEmpty (abi)) {
				Log.LogCodedError ("XA0035", Properties.Resources.XA0035, RuntimeIdentifier);
				return Task.CompletedTask;
			}

			(_, string outdir, string mtriple, AndroidTargetArch arch) = GetAbiSettings (abi);
			string toolPrefix = GetToolPrefix (ndk, arch, out int level);

			Arguments = string.Join (",", GetAotOptions (ndk, arch, level, outdir, mtriple, toolPrefix));
			OutputDirectory = outdir;
			return Task.CompletedTask;
		}

		protected string GetToolPrefix (NdkTools ndk, AndroidTargetArch arch, out int level)
		{
			level = 0;
			return EnableLLVM
				? ndk.GetNdkToolPrefixForAOT (arch, level = GetNdkApiLevel (ndk, arch))
				: Path.Combine (AndroidBinUtilsDirectory, $"{ndk.GetArchDirName (arch)}-");
		}

		int GetNdkApiLevel (NdkTools ndk, AndroidTargetArch arch)
		{
			AndroidAppManifest manifest = null;
			if (!string.IsNullOrEmpty (ManifestFile)) {
				manifest = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);
			}

			int level;
			if (manifest?.MinSdkVersion != null) {
				level       = manifest.MinSdkVersion.Value;
			} else if (int.TryParse (MinimumSupportedApiLevel, out level)) {
				// level already set
			} else if (int.TryParse (AndroidApiLevel, out level)) {
				// level already set
			} else {
				// Probably not ideal!
				level       = MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
			}

			// Some Android API levels do not exist on the NDK level. Workaround this my mapping them to the
			// most appropriate API level that does exist.
			if (level == 6 || level == 7) level = 5;
			else if (level == 10) level = 9;
			else if (level == 11) level = 12;
			else if (level == 20) level = 19;
			else if (level == 22) level = 21;
			else if (level == 23) level = 21;

			// API levels below level 21 do not provide support for 64-bit architectures.
			if (ndk.IsNdk64BitArch (arch) && level < 21) {
				level = 21;
			}

			// We perform a downwards API level lookup search since we might not have hardcoded the correct API
			// mapping above and we do not want to crash needlessly.
			for (; level >= 5; level--) {
				try {
					ndk.GetDirectoryPath (NdkToolchainDir.PlatformLib, arch, level);
					break;
				} catch (InvalidOperationException ex) {
					// Path not found, continue searching...
					continue;
				}
			}

			return level;
		}

		protected (string aotCompiler, string outdir, string mtriple, AndroidTargetArch arch) GetAbiSettings (string abi)
		{
			switch (abi) {
				case "armeabi-v7a":
					return (
						Path.Combine (SdkBinDirectory, "cross-arm"),
						Path.Combine (AotOutputDirectory, "armeabi-v7a"),
						"armv7-linux-gnueabi",
						AndroidTargetArch.Arm
					);

				case "arm64":
				case "arm64-v8a":
				case "aarch64":
					return (
						Path.Combine (SdkBinDirectory, "cross-arm64"),
						Path.Combine (AotOutputDirectory, "arm64-v8a"),
						"aarch64-linux-android",
						AndroidTargetArch.Arm64
					);

				case "x86":
					return (
						Path.Combine (SdkBinDirectory, "cross-x86"),
						Path.Combine (AotOutputDirectory, "x86"),
						"i686-linux-android",
						AndroidTargetArch.X86
					);

				case "x86_64":
					return (
						Path.Combine (SdkBinDirectory, "cross-x86_64"),
						Path.Combine (AotOutputDirectory, "x86_64"),
						"x86_64-linux-android",
						AndroidTargetArch.X86_64
					);

				// case "mips":
				default:
					throw new Exception ("Unsupported Android target architecture ABI: " + abi);
			}
		}

		/// <summary>
		/// Returns a list of parameters to pass to the --aot switch
		/// </summary>
		protected List<string> GetAotOptions (NdkTools ndk, AndroidTargetArch arch, int level, string outdir, string mtriple, string toolPrefix)
		{
			List<string> aotOptions = new List<string> ();

			if (Profiles != null && Profiles.Length > 0) {
				aotOptions.Add ("profile-only");
				foreach (var p in Profiles) {
					var fp = Path.GetFullPath (p.ItemSpec);
					aotOptions.Add ($"profile={fp}");
				}
			}
			if (!string.IsNullOrEmpty (AotAdditionalArguments))
				aotOptions.Add (AotAdditionalArguments);
			if (SequencePointsMode == SequencePointsMode.Offline)
				aotOptions.Add ($"msym-dir={outdir}");
			if (AotMode != AotMode.Normal)
				aotOptions.Add (AotMode.ToString ().ToLowerInvariant ());

			aotOptions.Add ("asmwriter");
			aotOptions.Add ($"mtriple={mtriple}");
			aotOptions.Add ($"tool-prefix={toolPrefix}");

			string ldName;
			if (EnableLLVM) {
				ldName = ndk.GetToolPath (NdkToolKind.Linker, arch, level);
				if (!string.IsNullOrEmpty (ldName)) {
					ldName = Path.GetFileName (ldName);
					if (ldName.IndexOf ('-') >= 0) {
						ldName = ldName.Substring (ldName.LastIndexOf ("-", StringComparison.Ordinal) + 1);
					}
				}
			} else {
				ldName = "ld";
			}
			string ldFlags = GetLdFlags (ndk, arch, level, toolPrefix);

			// MUST be before `ld-flags`, otherwise Mono fails to parse it on Windows
			if (!string.IsNullOrEmpty (ldName)) {
				aotOptions.Add ($"ld-name={ldName}");
			}
			if (!string.IsNullOrEmpty (ldFlags)) {
				aotOptions.Add ($"ld-flags={ldFlags}");
			}

			return aotOptions;
		}

		string GetLdFlags(NdkTools ndk, AndroidTargetArch arch, int level, string toolPrefix)
		{
			var toolchainPath = toolPrefix.Substring (0, toolPrefix.LastIndexOf (Path.DirectorySeparatorChar));
			var ldFlags = string.Empty;
			if (EnableLLVM) {
				if (string.IsNullOrEmpty (AndroidNdkDirectory)) {
					return null;
				}

				string androidLibPath = string.Empty;
				try {
					androidLibPath = ndk.GetDirectoryPath (NdkToolchainDir.PlatformLib, arch, level);
				} catch (InvalidOperationException ex) {
					Diagnostic.Error (5101, ex.Message);
				}

				string toolchainLibDir;
				if (ndk.UsesClang) {
					if (ndk.NoBinutils) {
						toolchainLibDir = String.Empty;
					} else {
						toolchainLibDir = GetNdkToolchainLibraryDir (ndk, toolchainPath, arch);
					}
				} else
					toolchainLibDir = GetNdkToolchainLibraryDir (ndk, toolchainPath);

				var libs = new List<string> ();
				if (ndk.UsesClang) {
					if (!String.IsNullOrEmpty (toolchainLibDir)) {
						libs.Add ($"-L{toolchainLibDir.TrimEnd ('\\')}");
					}
					libs.Add ($"-L{androidLibPath.TrimEnd ('\\')}");

					if (arch == AndroidTargetArch.Arm) {
						// Needed for -lunwind to work
						string compilerLibDir = Path.Combine (toolchainPath, "..", "sysroot", "usr", "lib", ndk.GetArchDirName (arch));
						libs.Add ($"-L{compilerLibDir.TrimEnd ('\\')}");
					}
				}

				if (!String.IsNullOrEmpty (toolchainLibDir)) {
					libs.Add (Path.Combine (toolchainLibDir, "libgcc.a"));
				}
				libs.Add (Path.Combine (androidLibPath, "libc.so"));
				libs.Add (Path.Combine (androidLibPath, "libm.so"));

				if (UsingAndroidNETSdk) {
					// NOTE: in .NET 6+ use space for the delimiter and escape spaces in paths
					var escaped = libs.Select (l => l.Replace (" ", "\\ "));
					ldFlags = string.Join (" ", escaped);
				} else {
					ldFlags = $"\\\"{string.Join ("\\\";\\\"", libs)}\\\"";
				}
			}
			return ldFlags;
		}

		static string GetNdkToolchainLibraryDir (NdkTools ndk, string binDir, string archDir = null)
		{
			var baseDir = Path.GetFullPath (Path.Combine (binDir, ".."));

			string libDir = Path.Combine (baseDir, "lib", "gcc");
			if (!String.IsNullOrEmpty (archDir))
				libDir = Path.Combine (libDir, archDir);

			var gccLibDir = Directory.EnumerateDirectories (libDir).ToList ();
			gccLibDir.Sort ();

			var libPath = gccLibDir.LastOrDefault ();
			if (libPath == null) {
				goto no_toolchain_error;
			}

			if (ndk.UsesClang)
				return libPath;

			gccLibDir = Directory.EnumerateDirectories (libPath).ToList ();
			gccLibDir.Sort ();

			libPath = gccLibDir.LastOrDefault ();
			if (libPath == null) {
				goto no_toolchain_error;
			}

			return libPath;

		no_toolchain_error:
			throw new Exception ("Could not find a valid NDK compiler toolchain library path");
		}

		static string GetNdkToolchainLibraryDir (NdkTools ndk, string binDir, AndroidTargetArch arch)
		{
			return GetNdkToolchainLibraryDir (ndk, binDir, ndk.GetArchDirName (arch));
		}
	}
}
