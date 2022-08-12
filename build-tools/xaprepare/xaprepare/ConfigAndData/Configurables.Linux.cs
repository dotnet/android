using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string AdoptOpenJDKUpdate = "345";
		const string AdoptOpenJDKBuild = "b01";

		const string JetBrainsOpenJDKOperatingSystem = "linux-x64";
		const string MicrosoftOpenJDKOperatingSystem = "linux-x64";
		const string AdoptOpenJDKOperatingSystem = "x64_linux";

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".so";
		}

		partial class Paths
		{
			public const string NdkToolchainOSTag = "linux-x86_64";
		}
	}
}
