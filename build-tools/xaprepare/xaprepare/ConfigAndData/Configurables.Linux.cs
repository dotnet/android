using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string CorrettoDistVersion = "8.232.09.1";
		const string CorrettoUrlPathVersion = CorrettoDistVersion;

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
