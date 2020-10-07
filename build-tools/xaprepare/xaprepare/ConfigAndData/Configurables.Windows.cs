using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string JetBrainsOpenJDKOperatingSystem = "windows-x64";

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".dll";
		}

		partial class Paths
		{
			static string ArchiveOSType                                           => "Darwin"; // Windows need sources from there
			public const string MonoCrossRuntimeInstallPath                       = "Windows";
			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");
			public const string NdkToolchainOSTag                                 = "windows-x86_64";
		}

		partial class Urls
		{
			public static readonly Uri DotNetInstallScript = new Uri ("https://dot.net/v1/dotnet-install.ps1");
		}
	}
}
