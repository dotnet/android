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
			if (Builder.UseDotNet) {
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
			if (Builder.UseDotNet) {
				project.SetRuntimeIdentifiers (abis.Split (';'));
			} else {
				project.SetProperty (KnownProperties.AndroidSupportedAbis, abis);
			}
		}

		public static void SetRuntimeIdentifier (this IShortFormProject project, string androidAbi)
		{
			project.SetProperty (KnownProperties.RuntimeIdentifier, AbiUtils.AbiToRuntimeIdentifier (androidAbi));
		}

		public static void SetRuntimeIdentifiers (this IShortFormProject project, string [] androidAbis)
		{
			var abis = new List<string> ();
			foreach (var androidAbi in androidAbis) {
				abis.Add (AbiUtils.AbiToRuntimeIdentifier (androidAbi));
			}
			project.SetProperty (KnownProperties.RuntimeIdentifiers, string.Join (";", abis));
		}
	}
}
