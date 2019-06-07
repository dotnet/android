using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Defaults
		{
			public const string DefaultCompiler = "cc";
		}

		partial class Paths
		{
			static string BundleOSType                  => Context.Instance.OS.Type;

			public static string BCLTestsSourceDir      => GetCachedPath (ref bclTestsSourceDir, () => Path.Combine (MonoProfileDir, "tests"));
			public static string BCLAssembliesSourceDir => MonoProfileDir;

			public static readonly string MonoRuntimeHostMingwNativeLibraryPrefix = Path.Combine ("..", "bin");
		}
	}
}
