using System;

namespace Xamarin.ProjectTools
{
	public static class AbiUtils
	{
		public static string AbiToRuntimeIdentifier (string androidAbi)
		{
			if (androidAbi == "armeabi-v7a") {
				return "android-arm";
			} else if (androidAbi == "arm64-v8a") {
				return "android-arm64";
			} else if (androidAbi == "x86") {
				return "android-x86";
			} else if (androidAbi == "x86_64") {
				return "android-x64";
			}
			throw new InvalidOperationException ($"Unknown abi: {androidAbi}");
		}
	}
}
