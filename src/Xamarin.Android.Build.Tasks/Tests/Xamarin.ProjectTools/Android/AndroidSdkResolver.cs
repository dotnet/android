using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public static class AndroidSdkResolver
	{
		static string HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		static string DefaultToolchainPath = Path.Combine (HomeDirectory, "android-toolchain");
		static string AzureToolchainPathMacOS = Path.Combine (HomeDirectory, "Library", "Android");
		static string ToolchainPath = (TestEnvironment.IsMacOS && TestEnvironment.IsRunningOnCI) ? AzureToolchainPathMacOS : DefaultToolchainPath;

		public static string GetAndroidSdkPath ()
		{
			var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = Path.GetFullPath (Path.Combine (ToolchainPath, "sdk"));

			return sdkPath;

		}

		public static string GetAndroidNdkPath ()
		{
			var ndkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = Path.GetFullPath (Path.Combine (ToolchainPath, "ndk"));

			return ndkPath;
		}

	}
}
