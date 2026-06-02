using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Defaults
		{
			public const string NativeLibraryExtension = ".dll";
		}

		partial class Paths
		{
		}

		partial class Urls
		{
			public static readonly Uri DotNetInstallScript = new Uri ("https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1");
		}
	}
}
