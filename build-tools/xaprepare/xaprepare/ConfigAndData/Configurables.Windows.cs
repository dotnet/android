using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string CorrettoDistVersion = "8.232.09.1";
		const string CorrettoUrlPathVersion = CorrettoDistVersion;

		partial class Urls
		{
			public static Uri Corretto => GetWindowsCorrettoUrl ();

			public static readonly Uri Corretto64 = new Uri ($"{Corretto_BaseUri}{CorrettoUrlPathVersion}/amazon-corretto-{CorrettoDistVersion}-windows-x64-jdk.zip");
			public static readonly Uri Corretto32 = new Uri ($"{Corretto_BaseUri}{CorrettoUrlPathVersion}/amazon-corretto-{CorrettoDistVersion}-windows-x86-jdk.zip");

			static Uri GetWindowsCorrettoUrl ()
			{
				if (Context.Instance.OS == null)
					return Corretto64;

				return Context.Instance.OS.Is64Bit ? Corretto64 : Corretto32;
			}
		}

		partial class Defaults
		{
			public const string NativeLibraryExtension = ".dll";
			public static readonly Version CorrettoVersion = Version.Parse (CorrettoDistVersion);
		}

		partial class Paths
		{
			static string ArchiveOSType                                           => "Darwin"; // Windows need sources from there
			public const string MonoCrossRuntimeInstallPath                       = "Windows";
			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");
			public const string NdkToolchainOSTag                                 = "windows-x86_64";
		}
	}
}
