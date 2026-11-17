using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

public partial class BuildTest : BaseTest
{
	static readonly object [] DotNetBuildSource = new object [] {
		new object [] {
			/* runtimeIdentifiers */ "android-arm64",
			/* isRelease */          true,
			/* usesAssemblyStore */  true,
			/* runtime */            AndroidRuntime.CoreCLR,
		},
		new object [] {
			/* runtimeIdentifiers */ "android-arm64",
			/* isRelease */          false,
			/* usesAssemblyStore */  true,
			/* runtime */            AndroidRuntime.CoreCLR,
		},
		new object [] {
			/* runtimeIdentifiers */ "android-arm64",
			/* isRelease */          true,
			/* usesAssemblyStore */  false,
			/* runtime */            AndroidRuntime.NativeAOT,
		},
	};

}
