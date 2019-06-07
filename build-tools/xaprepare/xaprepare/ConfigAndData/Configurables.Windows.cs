using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Urls
		{
			public static Uri Corretto => GetWindowsCorrettoUrl ();

			public static readonly Uri Corretto64 = new Uri ("https://d3pxv6yz143wms.cloudfront.net/8.212.04.2/amazon-corretto-8.212.04.2-windows-x64-jdk.zip");
			public static readonly Uri Corretto32 = new Uri ("https://d3pxv6yz143wms.cloudfront.net/8.212.04.2/amazon-corretto-8.212.04.2-windows-x86-jdk.zip");

			/// <summary>
			///   Base URL for the Ant tool download. Used in <see cref ="AndroidToolchain"/>
			/// </summary>
			public static readonly Uri AntBaseUri = new Uri ("https://archive.apache.org/dist/ant/binaries/");

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
		}

		partial class Paths
		{
			static string BundleOSType                                            => "Darwin"; // Windows doesn't build the bundle

			// Windows doesn't build the bundle so we need to look in the XA framework dir as installed
			public static string BCLAssembliesSourceDir                           => InstallBCLFrameworkDir;

			// Likewise here, there's no "source" dir for test assemblies - we need to look at the destination dir
			public static string BCLTestsSourceDir                                => BCLTestsDestDir;

			public const string MonoCrossRuntimeInstallPath                       = "Windows";
			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");
			public const string NdkToolchainOSTag                                 = "windows";
			public const string AntArchiveName                                    = "apache-ant-1.10.6-bin.zip";
			public static string AntArchivePath                                   => GetCachedPath (ref antArchivePath, () => Path.Combine (ctx.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory), AntArchiveName));
			public static string AntInstallDir                                    => GetCachedPath (ref antInstallDir, ()  => Path.Combine (ctx.Properties.GetRequiredValue (KnownProperties.AndroidToolchainDirectory), "ant"));
		}

		static string antArchivePath;
		static string antInstallDir;
	}
}
