using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string JetBrainsOpenJDKOperatingSystem = "linux-x64";

		partial class Urls
		{
			public static readonly Uri Corretto = new Uri ($"{Corretto_BaseUri}{CorrettoUrlPathVersion}/amazon-corretto-{CorrettoDistVersion}-linux-x64.tar.gz");
		}

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
