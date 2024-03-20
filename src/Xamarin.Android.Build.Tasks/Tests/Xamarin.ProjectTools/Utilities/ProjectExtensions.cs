using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class ProjectExtensions
	{
		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet
		/// </summary>
		[Obsolete ("SetAndroidSupportedAbis is deprecated, please use SetRuntimeIdentifiers instead.")]
		public static void SetAndroidSupportedAbis (this IShortFormProject project, params string [] abis)
		{
			project.SetRuntimeIdentifiers (abis);
		}

		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet
		/// </summary>
		/// <param name="abis">A semi-colon-delimited list of ABIs</param>
		[Obsolete ("SetAndroidSupportedAbis is deprecated, please use SetRuntimeIdentifiers instead.")]
		public static void SetAndroidSupportedAbis (this IShortFormProject project, string abis)
		{
			project.SetRuntimeIdentifiers (abis.Split (';'));
		}

		public static void SetRuntimeIdentifier (this IShortFormProject project, string androidAbi)
		{
			project.SetProperty (KnownProperties.RuntimeIdentifier, AbiUtils.AbiToRuntimeIdentifier (androidAbi));
		}

		public static void SetRuntimeIdentifiers (this IShortFormProject project, IEnumerable<string> androidAbis)
		{
			var abis = new List<string> ();
			foreach (var androidAbi in androidAbis) {
				abis.Add (AbiUtils.AbiToRuntimeIdentifier (androidAbi));
			}
			project.SetProperty (KnownProperties.RuntimeIdentifiers, string.Join (";", abis));
		}

		public static void SetRuntimeIdentifiers (this IShortFormProject project, params AndroidTargetArch[] targetArches)
		{
			if (targetArches == null || targetArches.Length == 0) {
				throw new ArgumentException ("must not be null or empty", nameof (targetArches));
			}

			project.SetProperty (KnownProperties.RuntimeIdentifiers, String.Join (";", targetArches.Select (arch => MonoAndroidHelper.ArchToRid (arch))));
		}
	}
}
