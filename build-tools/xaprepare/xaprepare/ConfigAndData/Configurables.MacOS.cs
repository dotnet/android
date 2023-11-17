using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string AdoptOpenJDKUpdate = "332";
		const string AdoptOpenJDKBuild = "b09";

		const string JetBrainsOpenJDKOperatingSystem = "osx-x64";
		const string MicrosoftOpenJDKOperatingSystem = "macOS-x64";
		const string AdoptOpenJDKOperatingSystem = "x64_mac";

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
