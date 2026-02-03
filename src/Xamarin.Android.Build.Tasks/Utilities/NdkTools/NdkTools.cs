#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public abstract class NdkTools
	{
		// Target triples used in various places (tools prefix, include directory naming etc)
		static readonly Dictionary<AndroidTargetArch, string> archTriples = new Dictionary<AndroidTargetArch, string> {
			{ AndroidTargetArch.Arm, "arm-linux-androideabi" },
			{ AndroidTargetArch.Arm64, "aarch64-linux-android" },
			{ AndroidTargetArch.X86, "i686-linux-android" },
			{ AndroidTargetArch.X86_64, "x86_64-linux-android" },
		};

		// Architecture names as used by the `platforms/*` directories pre NDK r22
		static readonly Dictionary<AndroidTargetArch, string> archPlatforms = new Dictionary<AndroidTargetArch, string> {
			{ AndroidTargetArch.Arm, "arm" },
			{ AndroidTargetArch.Arm64, "arm64" },
			{ AndroidTargetArch.X86, "x86" },
			{ AndroidTargetArch.X86_64, "x86_64" },
		};

		protected Dictionary<NdkToolKind, string> NdkToolNames = new Dictionary<NdkToolKind, string> {
			{ NdkToolKind.Assembler, "as" },
			{ NdkToolKind.Linker, "ld" },
			{ NdkToolKind.Strip, "strip" },
		};

		string? osBinPath;

		public NdkVersion Version { get; }
		public string NdkRootDirectory { get; }
		public bool UsesClang { get; protected set; }
		public bool NoBinutils { get; protected set; }

		// We can't use MonoAndroidHelper.AndroidSdk.AndroidNdkHostPlatform here since it's
		// not initialized while running tests and attempts to use cause a NREX
		// We could call `MonoAndroidHelper.RefreshSupportedVersions` but since this NdkTools
		// instance already knows the location of NDK, it would be just a waste of time.
		protected string HostPlatform => GetNdkHostPlatform ();
		protected bool IsWindows => OS.IsWindows;

		protected TaskLoggingHelper? Log { get; }

		public string OSBinPath {
			get => osBinPath ?? MonoAndroidHelper.GetOSBinPath ();
			set {
				if (String.IsNullOrEmpty (value)) {
					osBinPath = null;
				} else {
					osBinPath = value;
				}
			}
		}

		protected NdkTools (NdkVersion version, TaskLoggingHelper? log = null)
		{
			Log = log;
			Version = version;
		}

		protected NdkTools (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log = null) : this (version, log)
		{
			if (String.IsNullOrEmpty (androidNdkPath)) {
				throw new ArgumentException ("must be a non-empty string", nameof (androidNdkPath));
			}

			NdkRootDirectory = androidNdkPath;
		}

		public static NdkTools Create (string androidNdkPath, bool logErrors = true, TaskLoggingHelper? log = null)
		{
			if (String.IsNullOrEmpty (androidNdkPath) || !Directory.Exists (androidNdkPath)) {
				if (logErrors)
					log?.LogCodedError ("XA5104", Properties.Resources.XA5104);
				return new NullNdkTools (log);
			}

			NdkVersion? version = ReadVersion (androidNdkPath, logErrors, log);
			if (version == null) {
				return new NullNdkTools (log);
			}

			if (version.Main.Major < 14) {
				if (log != null) {
					if (logErrors)
						log.LogCodedError ("XA5104", Properties.Resources.XA5104);
					log.LogDebugMessage ($"Unsupported NDK version {version}");
				}
			} else if (version.Main.Major < 16) {
				// old, non-clang, no unified headers
				return new NdkToolsNoClangNoUnifiedHeaders (androidNdkPath, version, log);
			} else if (version.Main.Major < 19) {
				// old, non-clang, with unified headers
				return new NdkToolsNoClangWithUnifiedHeaders (androidNdkPath, version, log);
			} else if (version.Main.Major < 22) {
				return new NdkToolsWithClangWithPlatforms (androidNdkPath, version, log);
			} else if (version.Main.Major == 22) {
				return new NdkToolsWithClangNoPlatforms (androidNdkPath, version, log);
			}

			return new NdkToolsWithClangNoBinutils (androidNdkPath, version, log);
		}

		public abstract string GetToolPath (NdkToolKind kind, AndroidTargetArch arch, int apiLevel);
		public abstract string GetToolPath (string name, AndroidTargetArch arch, int apiLevel);
		public abstract int GetMinimumApiLevelFor (AndroidTargetArch arch, AndroidRuntime runtime);
		public abstract bool ValidateNdkPlatform (Action<string> logMessage, Action<string, string> logError, AndroidTargetArch arch, bool enableLLVM);

		public bool ValidateNdkPlatform (AndroidTargetArch arch, bool enableLLVM)
		{
			return ValidateNdkPlatform ((m) => Log?.LogMessage (m), (c, m) => Log?.LogCodedError (c, m), arch, enableLLVM);
		}

		public string GetArchDirName (AndroidTargetArch arch)
		{
			return GetArchTriple (arch);;
		}

		public virtual IEnumerable<int> GetSupportedPlatforms ()
		{
			// This works until NDK r22
			foreach (var platform in Directory.EnumerateDirectories (Path.Combine (NdkRootDirectory, "platforms"))) {
				var androidApi = Path.GetFileName (platform);
				int api = -1;
				if (int.TryParse (androidApi.Replace ("android-", String.Empty), out api)) {
					yield return api;
				}
			}
		}

		// This call is very specific as it needs to return full path to the location where the arch-prefixed tools
		// reside, but WITHOUT the actual tool name. This is required by Mono's AOT LLVM backend.
		public string GetNdkToolPrefixForAOT (AndroidTargetArch arch, int apiLevel)
		{
			string path = GetToolPath (NdkToolKind.Assembler, arch, apiLevel);
			return path.Substring (0, path.LastIndexOf ("-", StringComparison.Ordinal) + 1);;
		}

		// Work around for a bug in NDK r19 before its 'c' release. See NdkToolsWithClangWithPlatforms.ctor
		public virtual string GetCompilerTargetParameters (AndroidTargetArch arch, int apiLevel, bool forCPlusPlus = false)
		{
			return String.Empty;
		}

		public virtual string GetClangDeviceLibraryPath ()
		{
			throw new NotSupportedException ();
		}

		public static string GetBinutilsToolchainPrefix (AndroidTargetArch arch)
		{
			return $"{GetArchTriple (arch)}-";
		}

		public virtual string GetNdkToolchainPrefix (AndroidTargetArch arch)
		{
			string triple;
			switch (arch) {
				case AndroidTargetArch.Arm:
				case AndroidTargetArch.Arm64:
				case AndroidTargetArch.X86:
				case AndroidTargetArch.X86_64:
					triple = GetArchTriple (arch);
					break;

				default:
					// return empty. Since this method returns the "prefix", the resulting
					// tool path just becomes the tool name i.e. "gcc" becomes "gcc".
					// This should work for any custom arbitrary platform.
					return String.Empty;
			}

			return $"{triple}-";
		}

		public bool IsNdk64BitArch (AndroidTargetArch arch)
		{
			return arch == AndroidTargetArch.Arm64 || arch == AndroidTargetArch.X86_64;
		}

		public string GetDirectoryPath (NdkToolchainDir dir, AndroidTargetArch arch, int apiLevel)
		{
			string? path = null;
			switch (dir) {
				case NdkToolchainDir.AsmInclude: // optional
					path = GetAsmIncludeDirPath (arch, apiLevel);
					if (String.IsNullOrEmpty (path)) {
						return String.Empty;
					}
					break;

				case NdkToolchainDir.PlatformInclude:
					path = GetPlatformIncludeDirPath (arch, apiLevel);
					break;

				case NdkToolchainDir.PlatformLib:
					path = GetPlatformLibPath (arch, apiLevel);
					break;

				default:
					throw new InvalidOperationException ($"Unsupported NDK toolchain directory {dir}");
			}

			if (String.IsNullOrEmpty (path)) {
				throw new InvalidOperationException ($"NDK toolchain directory {dir} is required");
			}

			return EnsureDirectoryExists (path);
		}

		protected virtual string GetAsmIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return String.Empty;
		}

		protected virtual string GetPlatformLibPath (AndroidTargetArch arch, int apiLevel)
		{
			// This works until NDK r22
			string libDir = arch == AndroidTargetArch.X86_64 ? "lib64" : "lib";
			return Path.Combine (NdkRootDirectory, "platforms", $"android-{apiLevel}", $"arch-{GetPlatformArch (arch)}", "usr", libDir);
		}

		protected abstract string GetPlatformIncludeDirPath (AndroidTargetArch arch, int apiLevel);

		protected string? GetExecutablePath (string toolPath, bool mustExist)
		{
			string? executablePath = null;
			if (IsWindows) {
				// We can't just use `File.Exists (toolPath)` since the Windows NDK contains extension-less
				// Unix shell scripts which would fail to work if an attempt to execute them would be made.
				//
				// Also, the NDK r19+ workaround (see NdkToolsWithClangWithPlatforms.ctor) will cause `toolPath`
				// here to end with .exe when looking for the compiler and we can save some time by not letting
				// `MonoAndroidHelper.GetExecutablePath` iterate over all %PATHEXT% extensions only to return the
				// original tool name
				if (Path.HasExtension (toolPath) && File.Exists (toolPath)) {
					executablePath = toolPath;
				} else {
					string toolDir = Path.GetDirectoryName (toolPath);
					executablePath = Path.Combine (toolDir, MonoAndroidHelper.GetExecutablePath (toolDir, Path.GetFileName (toolPath)));
				}
			} else if (File.Exists (toolPath)) {
				executablePath = toolPath;
			}

			if (mustExist && executablePath.IsNullOrEmpty ()) {
				throw new InvalidOperationException ($"Required tool '{toolPath}' not found");
			}

			return executablePath;
		}

		protected virtual string GetToolName (NdkToolKind kind)
		{
			if (!NdkToolNames.TryGetValue (kind, out string? toolName) || String.IsNullOrEmpty (toolName)) {
				throw new InvalidOperationException ($"Unsupported NDK tool '{kind}'");
			}

			return toolName;
		}

		protected static string GetArchTriple (AndroidTargetArch arch)
		{
			if (archTriples.TryGetValue (arch, out string? triple) && !String.IsNullOrEmpty (triple)) {
				return triple;
			}

			throw new InvalidOperationException ($"Unsupported NDK architecture '{arch}'");
		}

		protected static string GetPlatformArch (AndroidTargetArch arch)
		{
			if (archPlatforms.TryGetValue (arch, out string? name) && !String.IsNullOrEmpty (name)) {
				return name;
			}

			throw new InvalidOperationException ($"Unsupported NDK architecture '{arch}'");
		}

		protected static int GetApiLevel (AndroidTargetArch arch, AndroidRuntime runtime)
		{
			int minValue = 0;
			string archName = GetPlatformArch (arch);

			Dictionary<string, int> apiLevels = runtime == AndroidRuntime.MonoVM ? XABuildConfig.ArchAPILevels : XABuildConfig.ArchAPILevelsNonMono;
			if (!apiLevels.TryGetValue (archName, out minValue)) {
				throw new InvalidOperationException ($"Unable to determine minimum API level for architecture {arch}");
			}

			return minValue;
		}

		protected string EnsureDirectoryExists (string path)
		{
			if (Directory.Exists (path)) {
				return path;
			}

			throw new InvalidOperationException ($"Required directory '{path}' not found");
		}

		// This is an ugly compromise to support unified headers with minimum code duplication, because C# doesn't
		// support multiple inheritance :(
		//
		// Unified headers are supported by both clang and non-clang NDKs, so the clang+unified headers NDK class
		// (NdkToolsNoClangWithUnifiedHeaders) would have to derive from two classes - one to implement the "with clang"
		// option and another to implement the "with unified headers" option.
		protected string GetUnifiedHeadersDirPath (string androidNdkPath)
		{
			return EnsureDirectoryExists (MakeUnifiedHeadersDirPath (androidNdkPath));
		}

		protected virtual string MakeUnifiedHeadersDirPath (string androidNdkPath)
		{
			return Path.Combine (androidNdkPath, "sysroot", "usr", "include");
		}

		protected string UnifiedHeaders_GetAsmIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return Path.Combine (GetUnifiedHeadersDirPath (NdkRootDirectory), GetArchTriple (arch));
		}

		protected string UnifiedHeaders_GetPlatformIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return GetUnifiedHeadersDirPath (NdkRootDirectory);
		}

		const string platformLinux64 = "linux-x86_64";
		const string platformLinux32 = "linux-x86";
		const string platformMac64 = "darwin-x86_64";
		const string platformMac32 = "darwin-x86";
		const string platformWindows64 = "windows-x86_64";
		const string platformWindows32 = "windows-x86";

		string GetNdkHostPlatform ()
		{
			bool cannotDeterminePlatform = false;

			if (RuntimeInformation.IsOSPlatform (OSPlatform.Linux)) {
				if (HasPrebuiltDir (platformLinux64)) {
					return platformLinux64;
				}

				if (HasPrebuiltDir (platformLinux32)) {
					return platformLinux32;
				}

				cannotDeterminePlatform = true;
			}

			if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX)) {
				if (HasPrebuiltDir (platformMac64)) {
					return platformMac64;
				}

				if (HasPrebuiltDir (platformMac32)) {
					return platformMac32;
				}

				cannotDeterminePlatform = true;
			}

			if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
				if (HasPrebuiltDir (platformWindows64)) {
					return platformWindows64;
				}

				if (HasPrebuiltDir (platformWindows32)) {
					return platformWindows32;
				}

				cannotDeterminePlatform = true;
			}

			if (cannotDeterminePlatform) {
				throw new InvalidOperationException ($"Unable to determine host NDK platform");
			}

			throw new InvalidOperationException ($"Unsupported OS");

			bool HasPrebuiltDir (string name)
			{
				return Directory.Exists (Path.Combine (NdkRootDirectory, "prebuilt", name));
			}
		}

		static NdkVersion? ReadVersion (string androidNdkPath, bool logErrors = true, TaskLoggingHelper? log = null)
		{
			string sourcePropertiesPath = Path.Combine (androidNdkPath, "source.properties");
			if (!File.Exists (sourcePropertiesPath)) {
				if (log != null) {
					if (logErrors)
						log.LogCodedError ("XA5104", Properties.Resources.XA5104);
					log.LogDebugMessage ("Could not read NDK version information, '{sourcePropertiesPath}' not found.");
				}
				return null;
			}

			var splitChars = new char[] {'='};
			string? ver = null;
			foreach (string l in File.ReadAllLines (sourcePropertiesPath)) {
				string line = l.Trim ();
				if (!line.StartsWith ("Pkg.Revision", StringComparison.Ordinal)) {
					continue;
				}

				string[] parts = line.Split (splitChars, 2);
				if (parts.Length != 2) {
					if (log != null) {
						if (logErrors)
							log.LogCodedError ("XA5104", Properties.Resources.XA5104);
						log.LogDebugMessage ($"Invalid NDK version format in '{sourcePropertiesPath}'.");
					}
					return null;
				}

				ver = parts [1].Trim ();
			}

			return new NdkVersion (ver);
		}
	}
}
