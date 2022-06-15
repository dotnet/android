using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class NdkToolsWithClangNoBinutils : NdkToolsWithClangNoPlatforms
	{
		public NdkToolsWithClangNoBinutils (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{
			NdkToolNames[NdkToolKind.Linker] = "ld";
			NoBinutils = true;

			NeedClangWorkaround = IsWindows;
			if (NeedClangWorkaround) {
				//
				// NDK r23 bug:
				//
				// The llvm toolchain directory contains a selection of shell scripts (both Unix and Windows)
				// which call `clang/clang++` with different `-target` parameters depending on both the target
				// architecture and API level. For instance, the clang/clang++ compilers targetting aarch64 on API level
				// 28 will have the following Unix shell scripts present in the toolchain `bin` directory:
				//
				//   aarch64-linux-android28-clang.cmd
				//   aarch64-linux-android28-clang++.cmd
				//
				// However, the Windows version of the NDK has a bug where there these scripts don't properly deal with
				// spaces, which breaks the `BuildAotApplicationAndBundleAndÜmläüts()` unit tests, as it attempts to
				// place the NDK into a directory containing spaces.  The result is:
				//
				//   CC="C:\a\_work\1\a\TestRelease\06-13_18.07.09\temp\SDK Ümläüts\ndk\toolchains\llvm\prebuilt\windows-x86_64\bin\armv7a-linux-androideabi19-clang.CMD"
				//   AS="C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Xamarin\Android\binutils\bin\arm-linux-androideabi-as.CMD"
				//   …
				//   [CC] "C:\a\_work\1\a\TestRelease\06-13_18.07.09\temp\SDK Ümläüts\ndk\toolchains\llvm\prebuilt\windows-x86_64\bin\armv7a-linux-androideabi19-clang.CMD" ^
				//     -c -D__ANDROID_API__=19 -DANDROID -o obj\Release\bundles\armeabi-v7a\temp.o ^
				//     -I "C:\a\_work\1\a\TestRelease\06-13_18.07.09\temp\SDK Ümläüts\ndk\toolchains\llvm\prebuilt\windows-x86_64\sysroot\usr\include\arm-linux-androideabi" ^
				//     -I "C:\a\_work\1\a\TestRelease\06-13_18.07.09\temp\SDK Ümläüts\ndk\toolchains\llvm\prebuilt\windows-x86_64\sysroot\usr\include" ^
				//     obj\Release\bundles\armeabi-v7a\temp.c
				//   [cc stderr] 'C:\a\_work\1\a\TestRelease\06-13_18.07.09\temp\SDK' is not recognized as an internal or external command,
				//   [cc stderr] operable program or batch file.
				//
				// The code below tries to rectify the situation by special-casing the compiler tool handling to
				// return path to the actual .exe instead of the CMD.
				//
				NdkToolNames[NdkToolKind.CompilerC] = "clang.exe";
				NdkToolNames[NdkToolKind.CompilerCPlusPlus] = "clang++.exe";
			}
		}

		public override string GetToolPath (NdkToolKind kind, AndroidTargetArch arch, int apiLevel)
		{
			switch (kind) {
				case NdkToolKind.Assembler:
				case NdkToolKind.Linker:
				case NdkToolKind.Strip:
					return GetEmbeddedToolPath (kind, arch);

				default:
					return base.GetToolPath (kind, arch, apiLevel);
			}
		}

		public override string GetToolPath (string name, AndroidTargetArch arch, int apiLevel)
		{
			// The only triple-prefixed binaries in NDK r23+ are the compilers, and these are
			// handled by the other GetToolPath overload.

			// Some tools might not have any prefix, let's check that first
			string toolPath = MakeToolPath (name, mustExist: false);
			if (!String.IsNullOrEmpty (toolPath)) {
				return toolPath;
			}

			// Otherwise, they might be prefixed with llvm-
			return MakeToolPath ($"llvm-{name}");
		}

		string GetEmbeddedToolPath (NdkToolKind kind, AndroidTargetArch arch)
		{
			string toolName = GetToolName (kind);
			string triple = GetArchTriple (arch);
			string binutilsDir = Path.Combine (OSBinPath, "binutils", "bin");

			return GetExecutablePath (Path.Combine (binutilsDir, $"{triple}-{toolName}"), mustExist: true);
		}

		public override string GetCompilerTargetParameters (AndroidTargetArch arch, int apiLevel, bool forCPlusPlus = false)
		{
			if (!NeedClangWorkaround) {
				return base.GetCompilerTargetParameters (arch, apiLevel, forCPlusPlus);
			}

			var sb = new StringBuilder ();
			sb.Append ("--target=").Append (GetCompilerTriple (arch)).Append (apiLevel);
			sb.Append (" -fno-addrsig");

			if (arch == AndroidTargetArch.X86) {
				sb.Append (" -mstackrealign");
			}

			if (forCPlusPlus) {
				sb.Append (" -stdlib=libc++");
			}

			return sb.ToString ();
		}
	}
}
