using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Urls
		{
			public static readonly Uri Corretto = new Uri ("https://d3pxv6yz143wms.cloudfront.net/8.212.04.2/amazon-corretto-8.212.04.2-macosx-x64.tar.gz");

			// 6.0: https://download.mono-project.com/archive/6.0.0/macos-10-universal/MonoFramework-MDK-6.0.0.241.macos10.xamarin.universal.pkg
			//      Currently breaks the build, thus we use the latest stable below
			public static readonly Uri MonoPackage = new Uri ("https://download.mono-project.com/archive/5.20.1/macos-10-universal/MonoFramework-MDK-5.20.1.19.macos10.xamarin.universal.pkg");
		}

		partial class Defaults
		{
			public const string MacOSDeploymentTarget = "10.11";
			public const string NativeLibraryExtension = ".dylib";
		}

		partial class Paths
		{
			public const string MonoCrossRuntimeInstallPath = "Darwin";
			public const string NdkToolchainOSTag = "darwin-x86_64";
		}
	}
}
