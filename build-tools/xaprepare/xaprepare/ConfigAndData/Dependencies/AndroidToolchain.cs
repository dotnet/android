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
			string AndroidNdkDirectory     = GetRequiredProperty (KnownProperties.AndroidNdkDirectory);
			string AndroidCmakeVersion     = GetRequiredProperty (KnownProperties.AndroidCmakeVersion);
			string AndroidCmakeVersionPath = GetRequiredProperty (KnownProperties.AndroidCmakeVersionPath);
			string EmulatorVersion         = GetRequiredProperty (KnownProperties.EmulatorVersion);
			string XABuildToolsFolder      = GetRequiredProperty (KnownProperties.XABuildToolsFolder);
			string XABuildToolsVersion     = GetRequiredProperty (KnownProperties.XABuildToolsVersion);
			string XAPlatformToolsVersion  = GetRequiredProperty (KnownProperties.XAPlatformToolsVersion);

			Components = new List<AndroidToolchainComponent> {
				new AndroidPlatformComponent ("android-2.3.3_r02-linux", "10"),
				new AndroidPlatformComponent ("android-15_r03",          "15"),
				new AndroidPlatformComponent ("android-16_r04",          "16"),
				new AndroidPlatformComponent ("android-17_r02",          "17"),
				new AndroidPlatformComponent ("android-18_r02",          "18"),
				new AndroidPlatformComponent ("android-19_r03",          "19"),
				new AndroidPlatformComponent ("android-20_r02",          "20"),
				new AndroidPlatformComponent ("android-21_r02",          "21"),
				new AndroidPlatformComponent ("android-22_r02",          "22"),
				new AndroidPlatformComponent ("platform-23_r03",         "23"),
				new AndroidPlatformComponent ("platform-24_r02",         "24"),
				new AndroidPlatformComponent ("platform-25_r03",         "25"),
				new AndroidPlatformComponent ("platform-26_r02",         "26"),
				new AndroidPlatformComponent ("platform-27_r03",         "27"),
				new AndroidPlatformComponent ("platform-28_r04",         "28"),
				new AndroidPlatformComponent ("platform-Q_r03",          "Q"),

				new AndroidToolchainComponent ("docs-24_r01",                                       destDir: "docs"),
				new AndroidToolchainComponent ("android_m2repository_r16",                          destDir: Path.Combine ("extras", "android", "m2repository")),
				new AndroidToolchainComponent ("x86-28_r04",                                        destDir: Path.Combine ("system-images", "android-28", "default", "x86"), relativeUrl: new Uri ("sys-img/android/", UriKind.Relative)),
				new AndroidToolchainComponent ($"android-ndk-r{AndroidNdkVersion}-{osTag}-x86_64",  destDir: AndroidNdkDirectory, expectedPkgRevision: "19.2.5345600"),
				new AndroidToolchainComponent ($"build-tools_r{XABuildToolsVersion}-{altOsTag}",    destDir: Path.Combine ("build-tools", XABuildToolsFolder), isMultiVersion: true),
				new AndroidToolchainComponent ($"platform-tools_r{XAPlatformToolsVersion}-{osTag}", destDir: "platform-tools"),
				new AndroidToolchainComponent ($"sdk-tools-{osTag}-4333796",                        destDir: "tools"),
				new AndroidToolchainComponent ($"emulator-{osTag}-{EmulatorVersion}",               destDir: "emulator"),
				new AndroidToolchainComponent ($"cmake-{AndroidCmakeVersion}-{osTag}-x86_64",       destDir: Path.Combine ("cmake", AndroidCmakeVersionPath), isMultiVersion: true, noSubdirectory: true, expectedPkgRevision: "3.10.2"),
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
