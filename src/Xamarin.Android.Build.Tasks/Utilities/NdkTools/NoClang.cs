using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class NdkToolsNoClang : NdkTools
	{
		protected NdkToolsNoClang (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{
			NdkToolNames[NdkToolKind.CompilerC] = "gcc";
			NdkToolNames[NdkToolKind.CompilerCPlusPlus] = "g++";
		}

		public override bool ValidateNdkPlatform (Action<string> logMessage, Action<string, string> logError, AndroidTargetArch arch, bool enableLLVM)
		{
			// Check that we have a compatible NDK version for the targeted ABIs.
			if (IsNdk64BitArch (arch) && Version.Main.Major < 10) {
				logMessage (
					"The detected Android NDK version is incompatible with the targeted 64-bit architecture, " +
					"please upgrade to NDK r14 or newer.");
			}

			// NDK r10d is buggy and cannot link x86_64 ABI shared libraries because they are 32-bits.
			// See https://code.google.com/p/android/issues/detail?id=161421
			if (enableLLVM && Version.Main.Major == 10 && Version.Main.Minor == 4 && arch == AndroidTargetArch.X86_64) {
				logError ("XA3004", Properties.Resources.XA3004);
				return false;
			}

			if (enableLLVM && (Version.Main.Major < 10 || (Version.Main.Major == 10 && Version.Main.Minor < 4))) {
				logError ("XA3005", Properties.Resources.XA3005);
			}

			return true;
		}

		public override int GetMinimumApiLevelFor (AndroidTargetArch arch, AndroidRuntime runtime)
		{
			int minValue = GetApiLevel (arch, runtime);
			var platforms = GetSupportedPlatforms ().OrderBy (x => x).Where (x => x >= minValue);
			return platforms.First (x => Directory.Exists (Path.Combine (NdkRootDirectory, "platforms", $"android-{x}", $"arch-{GetPlatformArch (arch)}")));
		}

		public override string GetToolPath (NdkToolKind kind, AndroidTargetArch arch, int apiLevel)
		{
			return GetToolPath (GetToolName (kind), arch, apiLevel);
		}

		public override string GetToolPath (string name, AndroidTargetArch arch, int apiLevel)
		{
			string triple = GetArchTriple (arch);
			string toolPath = Path.Combine (NdkRootDirectory, "toolchains", GetArchDirectoryName (arch), "prebuilt", HostPlatform, "bin", $"{triple}-{name}");
			return GetExecutablePath (toolPath, mustExist: true)!;
		}

		protected string GetArchDirectoryName (AndroidTargetArch arch)
		{
			// All toolchains before clang were version 4.9
			string archDir;

			switch (arch) {
				case AndroidTargetArch.X86:
					archDir = "x86";
					break;

				case AndroidTargetArch.X86_64:
					archDir = "x86_64";
					break;

				case AndroidTargetArch.Arm:
				case AndroidTargetArch.Arm64:
					archDir = GetArchTriple (arch);
					break;

				default:
					throw new InvalidOperationException ($"Unsupported architecture {arch}");
			}

			return $"{archDir}-4.9";
		}

		public override IEnumerable<int> GetSupportedPlatforms ()
		{
			foreach (var platform in Directory.EnumerateDirectories (Path.Combine (NdkRootDirectory, "platforms"))) {
				var androidApi = Path.GetFileName (platform);
				int api = -1;
				if (int.TryParse (androidApi.Replace ("android-", String.Empty), out api)) {
					yield return api;
				}
			}
		}
	}
}
