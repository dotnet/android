using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string JetBrainsOpenJDKOperatingSystem = "linux-x64";

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".so";
		}

		partial class Paths
		{
			public const string NdkToolchainOSTag = "linux-x86_64";
		}

		partial class Urls
		{
			public static readonly Uri DotNetInstallScript = new Uri ("https://dot.net/v1/dotnet-install.sh");
		}
	}
}
