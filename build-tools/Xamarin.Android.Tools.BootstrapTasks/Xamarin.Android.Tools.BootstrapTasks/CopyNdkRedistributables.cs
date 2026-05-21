#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	// Copies the NDK redistributable files (CRT, libc++, libunwind, libclang_rt.builtins)
	// for every supported ABI from the extracted NDK into the runtime redist directory.
	// MSBuild port of xaprepare's `Step_Android_SDK_NDK.CopyRedistributableFiles ()`.
	public class CopyNdkRedistributables : Task
	{
		static readonly string [] CRTFiles = {
			"crtbegin_so.o",
			"crtend_so.o",
			"libc.so",
			"libdl.so",
			"liblog.so",
			"libm.so",
			"libz.so",
		};

		static readonly string [] CPPAbiFiles = {
			"libc++_static.a",
			"libc++abi.a",
		};

		static readonly string [] ClangArchFiles = {
			"libunwind.a",
		};

		static readonly (string Abi, string ToolchainPrefix, string ClangArch, string Rid) [] Abis = {
			("armeabi-v7a", "arm-linux-androideabi", "arm",     "android-arm"),
			("arm64-v8a",   "aarch64-linux-android", "aarch64", "android-arm64"),
			("x86",         "i686-linux-android",    "i686",    "android-x86"),
			("x86_64",      "x86_64-linux-android",  "x86_64",  "android-x64"),
		};

		[Required]
		public string AndroidNdkDirectory { get; set; } = "";

		[Required]
		public string NdkToolchainOSTag { get; set; } = "";

		[Required]
		public string MinimumApiLevel { get; set; } = "";

		[Required]
		public string OutputDirectory { get; set; } = "";

		public override bool Execute ()
		{
			string toolchainRoot = Path.Combine (AndroidNdkDirectory, "toolchains", "llvm", "prebuilt", NdkToolchainOSTag);
			string sysrootLib    = Path.Combine (toolchainRoot, "sysroot", "usr", "lib");
			string clangRoot     = Path.Combine (toolchainRoot, "lib", "clang");
			string androidVer    = Path.Combine (toolchainRoot, "AndroidVersion.txt");

			if (!File.Exists (androidVer)) {
				Log.LogError ($"Android NDK version file not found: '{androidVer}'");
				return false;
			}

			string [] lines = File.ReadAllLines (androidVer);
			if (lines.Length < 1) {
				Log.LogError ($"Unknown format of Android NDK version file '{androidVer}'");
				return false;
			}

			string [] llvmVersion = lines [0].Split ('.');
			if (llvmVersion.Length < 3) {
				Log.LogError ($"Unknown LLVM version format for '{lines [0]}'");
				return false;
			}

			string clangLibPath = Path.Combine (clangRoot, llvmVersion [0], "lib", "linux");

			foreach (var (abi, toolchainPrefix, clangArch, rid) in Abis) {
				string abiDir       = Path.Combine (sysrootLib, toolchainPrefix);
				string crtFilesPath = Path.Combine (abiDir, MinimumApiLevel);
				string outputDir    = Path.Combine (OutputDirectory, rid);

				Directory.CreateDirectory (outputDir);

				foreach (string file in CRTFiles)
					CopyFile (abi, crtFilesPath, file, outputDir);

				foreach (string file in CPPAbiFiles)
					CopyFile (abi, abiDir, file, outputDir);

				CopyFile (abi, clangLibPath, $"libclang_rt.builtins-{clangArch}-android.a", outputDir);

				// Yay, consistency
				string archDir = string.Equals (clangArch, "i686", StringComparison.Ordinal) ? "i386" : clangArch;
				string clangArchLibPath = Path.Combine (clangLibPath, archDir);

				foreach (string file in ClangArchFiles)
					CopyFile (abi, clangArchLibPath, file, outputDir);
			}

			return !Log.HasLoggedErrors;
		}

		void CopyFile (string abi, string sourceDir, string fileName, string outputDir)
		{
			string sourceFile = Path.Combine (sourceDir, fileName);
			string destFile   = Path.Combine (outputDir, fileName);
			if (!File.Exists (sourceFile)) {
				Log.LogError ($"NDK redistributable not found for {abi}: '{sourceFile}'");
				return;
			}
			Log.LogMessage (MessageImportance.Low, $"  Copying NDK redistributable: {fileName} ({abi})");
			File.Copy (sourceFile, destFile, overwrite: true);
		}
	}
}
