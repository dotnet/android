using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string AdoptOpenJDKUpdate = "345";
		const string AdoptOpenJDKBuild = "b01";

		const string JetBrainsOpenJDKOperatingSystem = "windows-x64";
		const string MicrosoftOpenJDKOperatingSystem = "windows-x64";
		const string MicrosoftOpenJDKFileExtension   = "zip";
		const string AdoptOpenJDKOperatingSystem     = "x64_windows";
		const string AdoptOpenJDKArchiveExtension    = "zip";

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".dll";
		}

		partial class Paths
		{
			public const string NdkToolchainOSTag                                 = "windows-x86_64";
		}

		partial class Urls
		{
			public static readonly Uri DotNetInstallScript = new Uri ("https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1");
		}
	}
}
