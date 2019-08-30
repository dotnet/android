using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		const string CorrettoDistVersion = "8.222.10.1";
		const string CorrettoUrlPathVersion = CorrettoDistVersion;

		partial class Defaults
		{
			public const string DefaultCompiler = "cc";
			public static readonly Version CorrettoVersion = Version.Parse (CorrettoDistVersion);
		}

		partial class Paths
		{
			static string BundleOSType                  => Context.Instance.OS.Type;
			static string ArchiveOSType                 => Context.Instance.OS.Type;

			public static string BCLTestsSourceDir      => GetCachedPath (ref bclTestsSourceDir, () => Path.Combine (MonoProfileDir, "tests"));
			public static string BCLAssembliesSourceDir => MonoProfileDir;
			public static string HostRuntimeDir         => GetCachedPath (ref hostRuntimeDir, ()   => Path.Combine (XAInstallPrefix, "xbuild", "Xamarin", "Android", "lib", $"host-{ctx.OS.Type}"));

			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");

			static string hostRuntimeDir;
		}
	}
}
