using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class AndroidToolchain : AppObject
	{
		public static readonly Uri AndroidUri = Configurables.Urls.AndroidToolchain_AndroidUri;

		public List<AndroidToolchainComponent> Components { get; }

		public AndroidToolchain ()
		{
			string AndroidNdkVersion       = BuildAndroidPlatforms.AndroidNdkVersion;
			string AndroidPkgRevision      = BuildAndroidPlatforms.AndroidNdkPkgRevision;
			string AndroidNdkDirectory     = GetRequiredProperty (KnownProperties.AndroidNdkDirectory);

			// Upstream manifests with version information:
			//
			//  https://dl-ssl.google.com/android/repository/repository2-1.xml
			//  https://dl-ssl.google.com/android/repository/repository2-3.xml
			//    * platform APIs
			//    * build-tools
			//    * command-line tools
			//    * sdk-tools
			//    * platform-tools
			//
			//  https://dl-ssl.google.com/android/repository/addon2-1.xml
			//    * android_m2repository_r47
			//
			//  https://dl-ssl.google.com/android/repository/sys-img/android/sys-img2-1.xml
			//  https://dl-ssl.google.com/android/repository/sys-img/google_apis/sys-img2-1.xml
			//    * system images
			//
			// Everything that lives under $(AndroidSdkDirectory) is downloaded by
			// `src/androidsdk/androidsdk.targets`. Only the NDK remains here.
			Components = new List<AndroidToolchainComponent> {
				new AndroidToolchainComponent ($"android-ndk-r{AndroidNdkVersion}-{osTag}",
					destDir: AndroidNdkDirectory,
					pkgRevision: AndroidPkgRevision,
					buildToolName: $"android-ndk-r{AndroidNdkVersion}",
					buildToolVersion: AndroidPkgRevision
				),
			};
		}

		static string GetRequiredProperty (string propertyName)
		{
			string? value = Context.Instance.Properties [propertyName];
			if (String.IsNullOrEmpty (value))
				throw new InvalidOperationException ($"Required property '{propertyName}' not defined");
			return value!;
		}
	}
}
