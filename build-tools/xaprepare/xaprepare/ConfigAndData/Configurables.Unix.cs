using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string MicrosoftOpenJDKFileExtension   = "tar.gz";
		const string AdoptOpenJDKArchiveExtension = "tar.gz";

		partial class Defaults
		{
			public const string DefaultCompiler = "cc";
		}

		partial class Urls
		{
			// This is the "public" url that we really should be using, but it keeps failing with:
			// AuthenticationException: The remote certificate is invalid because of errors in the certificate chain: RevocationStatusUnknown
			// For now we'll grab it directly from GitHub
			// public static readonly Uri DotNetInstallScript = new Uri ("https://dot.net/v1/dotnet-install.sh");
			public static readonly Uri DotNetInstallScript = new Uri ("https://raw.githubusercontent.com/dotnet/install-scripts/refs/heads/main/src/dotnet-install.sh");
		}
	}
}
