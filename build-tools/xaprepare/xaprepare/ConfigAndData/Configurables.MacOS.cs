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
			public const string MacOSDeploymentTarget = "10.11";
			public const string NativeLibraryExtension = ".dylib";
		}

		partial class Paths
		{
			const string LibMonoSgenBaseName = "libmonosgen-2.0";

			public const string MonoCrossRuntimeInstallPath = "Darwin";
			public const string NdkToolchainOSTag = "darwin-x86_64";
			public static readonly string UnstrippedLibMonoSgenName = $"{LibMonoSgenBaseName}.d{Defaults.NativeLibraryExtension}";
			public static readonly string StrippedLibMonoSgenName = $"{LibMonoSgenBaseName}{Defaults.NativeLibraryExtension}";
		}
	}
}
