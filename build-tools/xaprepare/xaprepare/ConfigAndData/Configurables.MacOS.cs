using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Urls
		{
			public static readonly Uri Corretto = new Uri ($"{Corretto_BaseUri}{CorrettoUrlPathVersion}/amazon-corretto-{CorrettoDistVersion}-macosx-x64.tar.gz");
		}

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
