using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public static class NdkUtil
	{
		// We need it to work fine during our tests which are executed at the same time in various threads.
		[ThreadStatic]
		static bool usingClangNDK;

		public static bool UsingClangNDK => usingClangNDK;

		public static bool Init (string ndkPath)
		{
			return Init (null, ndkPath); // For tests which don't have access to a TaskLoggingHelper
		}

		public static bool Init (TaskLoggingHelper log, string ndkPath)
		{
			Version ndkVersion;
			bool hasNdkVersion = GetNdkToolchainRelease (ndkPath ?? "", out ndkVersion);

			if (!hasNdkVersion) {
				log?.LogCodedError ("XA5101",
						"Could not locate the Android NDK. Please make sure the Android NDK is installed in the Android SDK Manager, " +
						"or if using a custom NDK path, please ensure the $(AndroidNdkDirectory) MSBuild property is set to the custom path.");
				return false;
			}

			usingClangNDK = ndkVersion.Major >= 19;

			return true;
		}

		public static bool ValidateNdkPlatform (TaskLoggingHelper log, string ndkPath, AndroidTargetArch arch, bool enableLLVM)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.ValidateNdkPlatform (log, ndkPath, arch, enableLLVM);

			// Check that we have a compatible NDK version for the targeted ABIs.
			Version ndkVersion;
			bool hasNdkVersion = GetNdkToolchainRelease (ndkPath, out ndkVersion);

			if (hasNdkVersion && ndkVersion.Major < 19) {
				log.LogMessage (MessageImportance.High,
						"The detected Android NDK version is incompatible with this version of Xamarin.Android, " +
						"please upgrade to NDK r19 or newer.");
			}

			return true;
		}

		public static string GetNdkToolPrefix (string androidNdkPath, AndroidTargetArch arch, int apiLevel)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkToolPrefix (androidNdkPath, arch);

			var path = GetNdkTool (androidNdkPath, arch, "as", apiLevel);
			if (path != null)
				path = path.Substring (0, path.LastIndexOf ("-") + 1);
			return path;
		}

		// See the "NDK r19 bug" comment in the "GetNdkTool" method below for explanation of the issue
		// this method fixes.
		public static string GetCompilerTargetParameters (string androidNdkPath, AndroidTargetArch arch, int apiLevel, bool forCPlusPlus = false)
		{
			if (!UsingClangNDK || !OS.IsWindows)
				return String.Empty;

			string targetPrefix;
			string otherParams = String.Empty;
			switch (arch) {
				case AndroidTargetArch.Arm:
					targetPrefix = "--target=armv7a-linux-androideabi";
					break;

				case AndroidTargetArch.Arm64:
					targetPrefix = "--target=aarch64-linux-android";
					break;

				case AndroidTargetArch.X86:
					targetPrefix = "--target=i686-linux-android";
					otherParams = "-mstackrealign";
					break;

				case AndroidTargetArch.X86_64:
					targetPrefix = "--target=x86_64-linux-android";
					break;

				default:
					throw new InvalidOperationException ($"Unsupported target architecture {arch}");
			}

			string stdlib = String.Empty;
			if (forCPlusPlus)
				stdlib = "-stdlib=libc++";

			return $"{targetPrefix}{apiLevel} {otherParams} -fno-addrsig {stdlib}";
		}

		public static string GetNdkTool (string androidNdkPath, AndroidTargetArch arch, string tool, int apiLevel)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkTool (androidNdkPath, arch, tool);

			string toolchainDir = Path.Combine (androidNdkPath, "toolchains", "llvm", "prebuilt", MonoAndroidHelper.AndroidSdk.AndroidNdkHostPlatform);
			string toolName;
			bool forCompiler = false;

			if (String.Compare (tool, "gcc", StringComparison.Ordinal) == 0 ||
				String.Compare (tool, "clang", StringComparison.Ordinal) == 0) {
				forCompiler = true;
				toolName = "clang";
			} else if (String.Compare (tool, "g++", StringComparison.Ordinal) == 0 ||
					String.Compare (tool, "clang++", StringComparison.Ordinal) == 0) {
				forCompiler = true;
				toolName = "clang++";
			} else
				toolName = tool;

			//
			// NDK r19 bug.
			//
			// The llvm toolchain directory contains a selection of shell scripts (both Unix and Windows)
			// which call `clang/clang++` with different `-target` parameters depending on both the target
			// architecture and API level. For instance, the clang/clang++ compilers targetting aarch64 on API level
			// 28 will have the following Unix shell scripts present in the toolchain `bin` directory:
			//
			//   aarch64-linux-android28-clang
			//   aarch64-linux-android28-clang++
			//
			// However, the Windows version of the NDK has a bug where there is only one  Windows
			// counterpart to the above Unix scripts:
			//
			//   aarch64-linux-android28-clang.cmd
			//
			// This script, despite its name suggesting that it calls `clang.exe` in fact calls
			// `clang++.exe` which breaks compilation of some C programs (including the code generated by
			// Mono's mkbundle utility) because `clang++` treats the input as C++. There is no corresponding
			// `aarch64-linux-android28-clang++.cmd` and so invocation of `clang.exe` becomes harder and,
			// most certainly, non-standard as far as cross-platform NDK compatibility is concerned.
			//
			// The code below tries to rectify the situation by special-casing the compiler tool handling to
			// return path to the actual .exe instead of the CMD. Unfortunately, the caller of this code
			// will need to provide the correct parameters for the compilers.
			//
			string toolchainPrefix;
			if (forCompiler) {
				if (!OS.IsWindows)
					toolchainPrefix = $"{GetNdkToolchainPrefix (arch, true)}{apiLevel}";
				else
					toolchainPrefix = String.Empty;
			} else
				toolchainPrefix = GetNdkToolchainPrefix (arch, false);

			string extension = OS.IsWindows ? ".exe" : String.Empty;
			if (forCompiler && OS.IsWindows)
				toolName = $"{toolName}{extension}";
			else
				toolName = $"{toolchainPrefix}-{toolName}{extension}";

			string binDir = Path.Combine (toolchainDir, "bin");
			string toolExe  = MonoAndroidHelper.GetExecutablePath (binDir, toolName);
			string toolPath  = Path.Combine (binDir, toolExe);
			if (File.Exists (toolPath))
				return toolPath;

			Diagnostic.Error (5101,
					$"Toolchain utility '{toolName}' for target {arch} was not found. Tried in path: \"{toolchainDir}\"");
			return null;
		}

		static string GetUnifiedHeadersPath (string androidNdkPath)
		{
			return Path.Combine (androidNdkPath, "sysroot", "usr", "include");
		}

		public static string GetArchDirName (AndroidTargetArch arch)
		{
			switch (arch) {
				case AndroidTargetArch.Arm:
					return "arm-linux-androideabi";

				case AndroidTargetArch.Arm64:
					return "aarch64-linux-android";

				case AndroidTargetArch.X86:
					return "i686-linux-android";

				case AndroidTargetArch.X86_64:
					return "x86_64-linux-android";

				default:
					throw new InvalidOperationException ($"Unsupported architecture {arch}");
			}
		}

		public static string GetNdkAsmIncludePath (string androidNdkPath, AndroidTargetArch arch, int apiLevel)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkAsmIncludePath (androidNdkPath, arch, apiLevel);

			string path = GetUnifiedHeadersPath (androidNdkPath);
			string archDir = GetArchDirName (arch);

			return Path.Combine (path, archDir);
		}

		public static string GetNdkPlatformIncludePath (string androidNdkPath, AndroidTargetArch arch, int apiLevel)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkPlatformIncludePath (androidNdkPath, arch, apiLevel);

			string path = GetUnifiedHeadersPath (androidNdkPath);
			if (Directory.Exists (path))
				return path;

			throw new InvalidOperationException ($"Android include path not found. Tried: {path}");
		}

		public static string GetNdkPlatformLibPath (string androidNdkPath, AndroidTargetArch arch, int apiLevel)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkPlatformLibPath (androidNdkPath, arch, apiLevel);

			string lib = arch == AndroidTargetArch.X86_64 ? "lib64" : "lib";
			string path = Path.Combine (androidNdkPath, "platforms", $"android-{apiLevel}", $"arch-{GetPlatformArch (arch)}", "usr", lib);
			if (!Directory.Exists (path))
				throw new InvalidOperationException ($"Platform library directory for target {arch} and API Level {apiLevel} was not found. Expected path is \"{path}\"");
			return path;
		}

		static string GetPlatformArch (AndroidTargetArch arch)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetPlatformArch (arch);

			switch (arch) {
			case AndroidTargetArch.Arm:
				return "arm";
			case AndroidTargetArch.Arm64:
				return "arm64";
			case AndroidTargetArch.X86:
				return "x86";
			case AndroidTargetArch.X86_64:
				return "x86_64";
			}
			return null;
		}

		static string GetNdkToolchainPrefix (AndroidTargetArch arch, bool forCompiler)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetNdkToolchainPrefix (arch);

			switch (arch) {
			case AndroidTargetArch.Arm:
				return forCompiler ? "armv7a-linux-androideabi" : "arm-linux-androideabi";
			case AndroidTargetArch.Arm64:
				return "aarch64-linux-android";
			case AndroidTargetArch.X86:
				return "i686-linux-android";
			case AndroidTargetArch.X86_64:
				return "x86_64-linux-android";
			default:
				// return empty. Since this method returns the "prefix", the resulting
				// tool path just becomes the tool name i.e. "gcc" becomes "gcc".
				// This should work for any custom arbitrary platform.
				return String.Empty;
			}
		}

		public static bool GetNdkToolchainRelease (string androidNdkPath, out NdkUtilOld.NdkVersion ndkVersion)
		{
			return NdkUtilOld.GetNdkToolchainRelease (androidNdkPath, out ndkVersion);
		}

		public static bool GetNdkToolchainRelease (string androidNdkPath, out Version ndkVersion)
		{
			ndkVersion = new Version ();
			string sourcePropertiesPath = Path.Combine (androidNdkPath, "source.properties");
			if (!File.Exists (sourcePropertiesPath)) {
				return false;
			}

			foreach (string l in File.ReadAllLines (sourcePropertiesPath)) {
				string line = l.Trim ();
				if (!line.StartsWith ("Pkg.Revision", StringComparison.Ordinal))
					continue;
				string[] parts = line.Split (new char[] {'='}, 2);
				if (parts.Length != 2)
					return false;

				if (Version.TryParse (parts [1].Trim (), out ndkVersion))
					return true;
				break;
			}

			return false;
		}

		public static bool IsNdk64BitArch (AndroidTargetArch arch)
		{
			return arch == AndroidTargetArch.Arm64 || arch == AndroidTargetArch.X86_64;
		}

		public static IEnumerable<int> GetSupportedPlatforms (string androidNdkPath)
		{
			foreach (var platform in Directory.EnumerateDirectories (Path.Combine (androidNdkPath, "platforms"))) {
				var androidApi = Path.GetFileName (platform);
				int api = -1;
				if (int.TryParse (androidApi.Replace ("android-", String.Empty), out api)) {
					yield return api;
				}
			}
		}

		public static int GetMinimumApiLevelFor (AndroidTargetArch arch, string androidNdkPath)
		{
			if (!UsingClangNDK)
				return NdkUtilOld.GetMinimumApiLevelFor (arch, androidNdkPath);

			int minValue = 0;
			string archName = GetPlatformArch (arch);
			if (!XABuildConfig.ArchAPILevels.TryGetValue (archName, out minValue))
				throw new InvalidOperationException ($"Unable to determine minimum API level for architecture {arch}");

			return minValue;
		}
	}
}
