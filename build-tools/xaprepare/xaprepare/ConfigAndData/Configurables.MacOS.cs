using System;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		static string MicrosoftOpenJDKOperatingSystem = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "macos-aarch64": "macos-x64";

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".dylib";
		}

		partial class Paths
		{
			public const string MonoCrossRuntimeInstallPath = "Darwin";
			public const string NdkToolchainOSTag = "darwin-x86_64";
		}
	}
}
