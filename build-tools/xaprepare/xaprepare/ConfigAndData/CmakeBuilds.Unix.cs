using System.IO;

namespace Xamarin.Android.Prepare
{
	static partial class CmakeBuilds
	{
		static CmakeBuilds ()
		{
			MxeToolchainBasePath = Path.Combine (Configurables.Paths.BuildBinDir, "mingw");
			MingwDependenciesRootDirectory = Path.Combine (Configurables.Paths.BuildBinDir, "mingw-deps");
		}
	}
}
