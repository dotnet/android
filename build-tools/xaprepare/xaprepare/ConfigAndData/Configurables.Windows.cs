using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string CorrettoDistVersion = "8.222.10.3";
		const string CorrettoUrlPathVersion = "8.222.10.1";

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
			static string BundleOSType                                            => "Darwin"; // Windows doesn't build the bundle
			static string ArchiveOSType                                           => "Darwin"; // Windows need sources from there

			// Windows doesn't build the bundle so we need to look in the XA framework dir as installed
			public static string BCLAssembliesSourceDir                           => InstallBCLFrameworkDir;

			// Likewise here, there's no "source" dir for test assemblies - we need to look at the destination dir
			public static string BCLTestsSourceDir                                => BCLTestsDestDir;

			public const string MonoCrossRuntimeInstallPath                       = "Windows";
			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");
			public const string NdkToolchainOSTag                                 = "windows";
		}
	}
}
