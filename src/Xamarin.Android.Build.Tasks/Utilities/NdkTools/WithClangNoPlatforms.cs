using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class NdkToolsWithClangNoPlatforms : NdkToolsWithClang
	{
		public NdkToolsWithClangNoPlatforms (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{
			// NDK r22 removed the arch-prefixed `ld` executable. It provides instead `ld.gold` and `ld.bfd`
			// We're going to use the former

			NdkToolNames[NdkToolKind.Linker] = "ld.gold";
		}

		public override IEnumerable<int> GetSupportedPlatforms ()
		{
			// NDK r22 and newer no longer have a single platforms dir.  The API level directories are now found in per-arch
			// subdirectories under the toolchain directory. We need to examine all of them and compose a list of unique
			// API levels (since they are repeated in each per-arch subdirectory, but not all architectures have the
			// same set of API levels)
			var apiLevels = new HashSet<int> ();
			string sysrootLibDir = GetToolchainLibDir ();
			var supportedArchitectures = new []{
				AndroidTargetArch.Arm,
				AndroidTargetArch.Arm64,
				AndroidTargetArch.X86,
				AndroidTargetArch.X86_64,
			};

			foreach (AndroidTargetArch targetArch in supportedArchitectures) {
				string archDirName = GetArchDirName (targetArch);
				if (String.IsNullOrEmpty (archDirName)) {
					Log?.LogWarning ($"NDK architecture {targetArch} unknown?");
					continue;
				}

				string archDir = Path.Combine (sysrootLibDir, archDirName);
				if (!Directory.Exists (archDir)) {
					Log?.LogWarning ($"Architecture {targetArch} toolchain directory '{archDir}' not found");
					continue;
				}

				foreach (string platform in Directory.EnumerateDirectories (archDir, "*", SearchOption.TopDirectoryOnly)) {
					string plibc = Path.Combine (platform, "libc.so");
					if (!File.Exists (plibc)) {
						continue;
					}

					string pdir = Path.GetFileName (platform);
					int api;
					if (!Int32.TryParse (pdir, out api) || apiLevels.Contains (api)) {
						continue;
					}
					apiLevels.Add (api);
				}
			}

			return apiLevels;
		}

		protected override string MakeUnifiedHeadersDirPath (string androidNdkPath)
		{
			return Path.Combine (GetSysrootDir (androidNdkPath), "usr", "include");
		}

		protected override string GetPlatformLibPath (AndroidTargetArch arch, int apiLevel)
		{
			return Path.Combine (GetToolchainLibDir (), GetArchTriple (arch), apiLevel.ToString ());
		}

		protected string GetSysrootDir (string androidNdkPath)
		{
			return Path.Combine (androidNdkPath, "toolchains", "llvm", "prebuilt", HostPlatform, "sysroot");
		}

		protected string GetToolchainLibDir ()
		{
			return Path.Combine (GetSysrootDir (NdkRootDirectory), "usr", "lib");
		}
	}
}
