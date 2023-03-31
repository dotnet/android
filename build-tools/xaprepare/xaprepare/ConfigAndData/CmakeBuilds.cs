using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	//
	// Templates for various cmake builds (sqlite and monodroid currently).
	// Generator code is in ../Application/GeneratedMonodroidCmakeFiles.cs
	//
	// This is all a bit convoluted, but the single set of CMake defines below needs to serve the purpose of building
	// several different flavors our our runtime (52 in total, right now) and is used to generate both MSBuild and Unix
	// bash code.
	//
	static partial class CmakeBuilds
	{
		public sealed class RuntimeCommand
		{
			public string Configuration = String.Empty;
			public string BuildType = String.Empty;
			public string Suffix = String.Empty;
			public string MSBuildApiLevel = String.Empty;
			public List<string>? ExtraOptions = null;
			public bool IsDotNet = false;
			public bool IsHost = false;
		};

		const string msbuildApiLevelLegacy = "%(AndroidSupportedTargetJitAbi.ApiLevel)";
		const string msbuildApiLevel = "%(AndroidSupportedTargetJitAbi.ApiLevelNET)";

		// These two are configured in CmakeBuilds.Unix.cs, Windows doesn't use them
		public static readonly string MxeToolchainBasePath = String.Empty;
		public static readonly string MingwDependenciesRootDirectory = String.Empty;

		public static readonly List<string> CommonFlags = new List<string> {
			"-GNinja",
			"-DCMAKE_MAKE_PROGRAM=\"@NinjaPath@\"",
			"-DXA_BUILD_CONFIGURATION=@XA_BUILD_CONFIGURATION@",
			"-DXA_LIB_TOP_DIR=\"@XA_LIB_TOP_DIR@\"",
			"-DLIBUNWIND_SOURCE_DIR=\"@LibUnwindSourceFullPath@\"",
			"-DLIBUNWIND_HEADERS_DIR=\"@LibUnwindGeneratedHeadersFullPath@\"",
		};

		public static readonly List<string> AndroidFlags = new List<string> {
			"-DANDROID_STL=\"none\"",
			"-DANDROID_CPP_FEATURES=\"no-rtti no-exceptions\"",
			"-DANDROID_TOOLCHAIN=clang",
			"-DCMAKE_TOOLCHAIN_FILE=\"@AndroidNdkDirectory@@AndroidToolchainPath@\"",
			"-DANDROID_NDK=@AndroidNdkDirectory@",
			"-DLIBUNWIND_SOURCE_DIR=\"@LibUnwindSourceFullPath@\"",
			"-DLIBUNWIND_HEADERS_DIR=\"@LibUnwindGeneratedHeadersFullPath@\"",
		};

		public static readonly List<string> MonodroidCommonDefines = new List<string> {
			"-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
			"-DMONO_PATH=\"@MonoSourceFullPath@\"",
		};

		public static readonly List<string> MonodroidMxeCommonFlags = new List<string> {
			"-DMINGW_DEPENDENCIES_ROOT_DIR=\"@MingwDependenciesRootDirectory@\"",
		};

		public static readonly List<string> MonodroidMxeCommonFlagsBitness = new List<string> {
			"-DCMAKE_TOOLCHAIN_FILE=\"@MxeToolchainBasePath@-@BITNESS@.cmake\"",
			"-DMINGW_TARGET_@BITNESS@=1",
		};

		public static readonly List<string> ConfigureHostRuntimeCommandsCommonFlags = new List<string> {
			"@CmakeHostFlags@",
			"-DCONFIGURATION=@CONFIGURATION@",
			"-DCMAKE_BUILD_TYPE=@BUILD_TYPE@",
			"-DJDK_INCLUDE=\"@JdkIncludePath@\"",
			"-DCMAKE_ARCHIVE_OUTPUT_DIRECTORY=\"@OUTPUT_DIRECTORY@\"",
			"-DCMAKE_LIBRARY_OUTPUT_DIRECTORY=\"@OUTPUT_DIRECTORY@\"",
			"-DCMAKE_RUNTIME_OUTPUT_DIRECTORY=\"@OUTPUT_DIRECTORY@\"",
			"\"@SOURCE_DIRECTORY@\"",
		};

		public static readonly List<string> ConfigureAndroidRuntimeCommandsCommonFlags = new List<string> {
			"@CmakeAndroidFlags@",
			"-DCONFIGURATION=@CONFIGURATION@",
			"-DCMAKE_BUILD_TYPE=@BUILD_TYPE@",
			"-DANDROID_NATIVE_API_LEVEL=@NATIVE_API_LEVEL@",
			"-DANDROID_PLATFORM=android-@NATIVE_API_LEVEL@",
			"-DANDROID_ABI=@ABI@",
			"-DANDROID_RID=@RID@",
			"-DCMAKE_ARCHIVE_OUTPUT_DIRECTORY=\"@OUTPUT_DIRECTORY@\"",
			"-DCMAKE_LIBRARY_OUTPUT_DIRECTORY=\"@OUTPUT_DIRECTORY@\"",
			"\"@SOURCE_DIRECTORY@\"",
		};

		public static readonly List<string> AsanExtraOptions = new List<string> {
			"-DENABLE_CLANG_ASAN=ON",
			"-DANDROID_STL=\"c++_static\"",
		};

		public static readonly List<string> UbsanExtraOptions = new List<string> {
			"-DENABLE_CLANG_UBSAN=ON",
			"-DANDROID_STL=\"c++_static\"",
			"-DANDROID_CPP_FEATURES=\"rtti exceptions\"",
		};

		const string enableNet = "-DENABLE_NET=ON";
		public static readonly List<string> NetExtraOptions = new List<string> {
			enableNet,
		};

		public static readonly List<string> NetAsanExtraOptions = new List<string> (AsanExtraOptions) {
			enableNet,
		};

		public static readonly List<string> NetUbsanExtraOptions = new List<string> (UbsanExtraOptions) {
			enableNet,
		};

		public static readonly List<RuntimeCommand> AndroidRuntimeCommands = new List<RuntimeCommand> {
			// Debug builds
			new RuntimeCommand {
				Suffix = "Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevelLegacy,
			},

			new RuntimeCommand {
				Suffix = "asan-Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevelLegacy,
				ExtraOptions = AsanExtraOptions,
			},

			new RuntimeCommand {
				Suffix = "ubsan-Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevelLegacy,
				ExtraOptions = UbsanExtraOptions,
			},

			new RuntimeCommand {
				Suffix = "Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetExtraOptions,
				IsDotNet = true,
			},

			new RuntimeCommand {
				Suffix = "asan-Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetAsanExtraOptions,
				IsDotNet = true,
			},

			new RuntimeCommand {
				Suffix = "ubsan-Debug",
				Configuration = "Release",
				BuildType = "Debug",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetUbsanExtraOptions,
				IsDotNet = true,
			},

			// Release builds

			new RuntimeCommand {
				Suffix = "Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevelLegacy,
			},

			new RuntimeCommand {
				Suffix = "asan-Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevelLegacy,
				ExtraOptions = AsanExtraOptions,
			},

			new RuntimeCommand {
				Suffix = "ubsan-Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevelLegacy,
				ExtraOptions = UbsanExtraOptions,
			},

			new RuntimeCommand {
				Suffix = "Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetExtraOptions,
				IsDotNet = true,
			},

			new RuntimeCommand {
				Suffix = "asan-Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetAsanExtraOptions,
				IsDotNet = true,
			},

			new RuntimeCommand {
				Suffix = "ubsan-Release",
				Configuration = "Debug",
				BuildType = "Release",
				MSBuildApiLevel = msbuildApiLevel,
				ExtraOptions = NetUbsanExtraOptions,
				IsDotNet = true,
			},
		};

		public static readonly List<RuntimeCommand> HostRuntimeCommands = new List<RuntimeCommand> {
			new RuntimeCommand {
				Suffix = "Debug",
				Configuration = "Release",
				BuildType = "Debug",
				IsHost = true,
			},

			new RuntimeCommand {
				Suffix = "Release",
				Configuration = "Debug",
				BuildType = "Release",
				IsHost = true,
			},
		};
	}
}
