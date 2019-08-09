using System;

namespace Xamarin.Android.Prepare
{
	partial class Configurables
	{
		partial class Urls
		{
			public static readonly Uri Corretto = new Uri ("https://d3pxv6yz143wms.cloudfront.net/8.212.04.2/amazon-corretto-8.212.04.2-macosx-x64.tar.gz");
			public static readonly Uri MonoPackage = new Uri ("https://xamjenkinsartifact.azureedge.net/build-package-osx-mono/2019-06/121/761220d914e28b3ebd67e5642cf8c200110f62ec/MonoFramework-MDK-6.4.0.137.macos10.xamarin.universal.pkg");
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
