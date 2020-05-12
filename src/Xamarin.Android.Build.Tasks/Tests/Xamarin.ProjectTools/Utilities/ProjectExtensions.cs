using System;

namespace Xamarin.ProjectTools
{
	public static class ProjectExtensions
	{
		public static void SetRuntimeIdentifier (this IShortFormProject project, string androidAbi)
		{
			if (androidAbi == "armeabi-v7a") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android.21-arm");
			} else if (androidAbi == "arm64-v8a") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android.21-arm64");
			} else if (androidAbi == "x86") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android.21-x86");
			} else if (androidAbi == "x86_64") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android.21-x64");
			}
		}
	}
}
