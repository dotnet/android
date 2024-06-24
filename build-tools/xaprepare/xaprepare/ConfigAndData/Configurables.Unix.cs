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
			public static readonly Uri DotNetInstallScript = new Uri ("https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh");
		}
	}
}
