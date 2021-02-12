using System.Collections.Generic;

namespace Xamarin.ProjectTools
{
	public static class ProjectExtensions
	{
		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet
		/// </summary>
		public static void SetAndroidSupportedAbis (this IShortFormProject project, params string [] abis)
		{
			if (Builder.UseDotNet || project is XASdkProject) {
				project.SetRuntimeIdentifiers (abis);
			} else {
				project.SetAndroidSupportedAbis (string.Join (";", abis));
			}
		}

		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet
		/// </summary>
		/// <param name="abis">A semi-colon-delimited list of ABIs</param>
		public static void SetAndroidSupportedAbis (this IShortFormProject project, string abis)
		{
			if (Builder.UseDotNet || project is XASdkProject) {
				project.SetRuntimeIdentifiers (abis.Split (';'));
			} else {
				project.SetProperty (KnownProperties.AndroidSupportedAbis, abis);
			}
		}

		public static void SetRuntimeIdentifier (this IShortFormProject project, string androidAbi)
		{
			if (androidAbi == "armeabi-v7a") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm");
			} else if (androidAbi == "arm64-v8a") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			} else if (androidAbi == "x86") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android-x86");
			} else if (androidAbi == "x86_64") {
				project.SetProperty (KnownProperties.RuntimeIdentifier, "android-x64");
			}
		}

		public static void SetRuntimeIdentifiers (this IShortFormProject project, string [] androidAbis)
		{
			var abis = new List<string> ();
			foreach (var androidAbi in androidAbis) {
				if (androidAbi == "armeabi-v7a") {
					abis.Add ("android-arm");
				} else if (androidAbi == "arm64-v8a") {
					abis.Add ("android-arm64");
				} else if (androidAbi == "x86") {
					abis.Add ("android-x86");
				} else if (androidAbi == "x86_64") {
					abis.Add ("android-x64");
				}
			}
			project.SetProperty (KnownProperties.RuntimeIdentifiers, string.Join (";", abis));
		}
	}
}
