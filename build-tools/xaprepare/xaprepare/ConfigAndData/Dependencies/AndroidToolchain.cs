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
			string AndroidCmakeVersion     = GetRequiredProperty (KnownProperties.AndroidCmakeVersion);
			string AndroidCmakeVersionPath = GetRequiredProperty (KnownProperties.AndroidCmakeVersionPath);
			string EmulatorVersion         = GetRequiredProperty (KnownProperties.EmulatorVersion);
			string EmulatorPkgRevision     = GetRequiredProperty (KnownProperties.EmulatorPkgRevision);
			string XABuildToolsFolder      = GetRequiredProperty (KnownProperties.XABuildToolsFolder);
			string XABuildToolsVersion     = GetRequiredProperty (KnownProperties.XABuildToolsVersion);
			string XAPlatformToolsVersion  = GetRequiredProperty (KnownProperties.XAPlatformToolsVersion);

			Components = new List<AndroidToolchainComponent> {
				new AndroidPlatformComponent ("android-2.3.3_r02", apiLevel: "10", pkgRevision: "2"),
				new AndroidPlatformComponent ("android-15_r05",    apiLevel: "15", pkgRevision: "5"),
				new AndroidPlatformComponent ("android-16_r05",    apiLevel: "16", pkgRevision: "5"),
				new AndroidPlatformComponent ("android-17_r03",    apiLevel: "17", pkgRevision: "3"),
				new AndroidPlatformComponent ("android-18_r03",    apiLevel: "18", pkgRevision: "3"),
				new AndroidPlatformComponent ("android-19_r04",    apiLevel: "19", pkgRevision: "4"),
				new AndroidPlatformComponent ("android-20_r02",    apiLevel: "20", pkgRevision: "2"),
				new AndroidPlatformComponent ("android-21_r02",    apiLevel: "21", pkgRevision: "2"),
				new AndroidPlatformComponent ("android-22_r02",    apiLevel: "22", pkgRevision: "2"),
				new AndroidPlatformComponent ("platform-23_r03",   apiLevel: "23", pkgRevision: "3"),
				new AndroidPlatformComponent ("platform-24_r02",   apiLevel: "24", pkgRevision: "3"), // Local package revision is actually .3
				new AndroidPlatformComponent ("platform-25_r03",   apiLevel: "25", pkgRevision: "3"),
				new AndroidPlatformComponent ("platform-26_r02",   apiLevel: "26", pkgRevision: "2"),
				new AndroidPlatformComponent ("platform-27_r03",   apiLevel: "27", pkgRevision: "3"),
				new AndroidPlatformComponent ("platform-28_r04",   apiLevel: "28", pkgRevision: "4"),
				new AndroidPlatformComponent ("platform-29_r01",   apiLevel: "29", pkgRevision: "1"),

				new AndroidToolchainComponent ("docs-24_r01",                                       destDir: "docs", pkgRevision: "1"),
				new AndroidToolchainComponent ("android_m2repository_r47",                          destDir: Path.Combine ("extras", "android", "m2repository"), pkgRevision: "47.0.0"),
				new AndroidToolchainComponent ("x86-29_r06",                                        destDir: Path.Combine ("system-images", "android-29", "default", "x86"), relativeUrl: new Uri ("sys-img/android/", UriKind.Relative), pkgRevision: "6"),
				new AndroidToolchainComponent ($"android-ndk-r{AndroidNdkVersion}-{osTag}-x86_64",  destDir: AndroidNdkDirectory, pkgRevision: AndroidPkgRevision),
				new AndroidToolchainComponent ($"build-tools_r{XABuildToolsVersion}-{altOsTag}",    destDir: Path.Combine ("build-tools", XABuildToolsFolder), isMultiVersion: true),
				new AndroidToolchainComponent ($"platform-tools_r{XAPlatformToolsVersion}-{osTag}", destDir: "platform-tools", pkgRevision: XAPlatformToolsVersion),
				new AndroidToolchainComponent ($"sdk-tools-{osTag}-4333796",                        destDir: "tools", pkgRevision: "26.1.1"),
				new AndroidToolchainComponent ($"emulator-{osTag}-{EmulatorVersion}",               destDir: "emulator", pkgRevision: EmulatorPkgRevision),
				new AndroidToolchainComponent ($"cmake-{AndroidCmakeVersion}-{osTag}-x86_64",       destDir: Path.Combine ("cmake", AndroidCmakeVersionPath), isMultiVersion: true, noSubdirectory: true, pkgRevision: "3.10.2"),
			};
		}

		static string GetRequiredProperty (string propertyName)
		{
			string value = Context.Instance.Properties [propertyName];
			if (String.IsNullOrEmpty (value))
				throw new InvalidOperationException ($"Required property '{propertyName}' not defined");
			return value;
		}
	}
}
