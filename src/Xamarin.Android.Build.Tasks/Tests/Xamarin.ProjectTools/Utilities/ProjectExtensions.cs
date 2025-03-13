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
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet. If <param name="removeConflictingProperties" />
		/// is `true` (the default), remove potentially set properties that may break the build with `RuntimeIdentifiers` present. Currently, it
		/// means removing the `RuntimeIdentifier`
		/// </summary>
		[Obsolete ("SetAndroidSupportedAbis is deprecated, please use SetRuntimeIdentifiers instead.")]
		public static void SetAndroidSupportedAbis (this IShortFormProject project, bool removeConflictingProperties, params string [] abis)
		{
			project.SetRuntimeIdentifiers (abis);
		}

		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet.
		/// Remove potentially set properties that may break the build
		/// with `RuntimeIdentifiers` present. Currently, it means removing the `RuntimeIdentifier` property.
		/// </summary>
		[Obsolete ("SetAndroidSupportedAbis is deprecated, please use SetRuntimeIdentifiers instead.")]
		public static void SetAndroidSupportedAbis (this IShortFormProject project, params string [] abis)
		{
			project.SetAndroidSupportedAbis (removeConflictingProperties: true, abis);
		}

		/// <summary>
		/// Sets $(AndroidSupportedAbis) or $(RuntimeIdentifiers) depending if running under dotnet. If <param name="removeConflictingProperties" />
		/// is `true` (the default), remove potentially set properties that may break the build with `RuntimeIdentifiers` present. Currently, it
		/// means removing the `RuntimeIdentifier`
		/// </summary>
		/// <param name="abis">A semi-colon-delimited list of ABIs</param>
		[Obsolete ("SetAndroidSupportedAbis is deprecated, please use SetRuntimeIdentifiers instead.")]
		public static void SetAndroidSupportedAbis (this IShortFormProject project, string abis, bool removeConflictingProperties = true)
		{
			project.SetRuntimeIdentifiers (abis.Split (';'), removeConflictingProperties);
		}

		/// <summary>
		/// Set the `$(RuntimeIdentifier)` property to the specified value. If <param name="removeConflictingProperties" /> is `true` (the default), remove
		/// potentially set properties that may break the build with `RuntimeIdentifier` present. Currently, it means removing the `RuntimeIdentifiers`
		/// property.
		/// </summary>
		public static void SetRuntimeIdentifier (this IShortFormProject project, string androidAbi, bool removeConflictingProperties = true)
		{
			project.SetProperty (KnownProperties.RuntimeIdentifier, AbiUtils.AbiToRuntimeIdentifier (androidAbi));
			if (removeConflictingProperties) {
				project.RemoveProperty (KnownProperties.RuntimeIdentifiers);
			}
		}

		/// <summary>
		/// Set the `$(RuntimeIdentifiers)` property to the specified value. If <param name="removeConflictingProperties" /> is `true` (the default), remove
		/// potentially set properties that may break the build with `RuntimeIdentifiers` present. Currently, it means removing the `RuntimeIdentifier`
		/// property.
		/// </summary>
		public static void SetRuntimeIdentifiers (this IShortFormProject project, IEnumerable<string> androidAbis, bool removeConflictingProperties = true)
		{
			var abis = new List<string> ();
			foreach (var androidAbi in androidAbis) {
				abis.Add (AbiUtils.AbiToRuntimeIdentifier (androidAbi));
			}
			project.SetProperty (KnownProperties.RuntimeIdentifiers, string.Join (";", abis));

			if (removeConflictingProperties) {
				project.RemoveProperty (KnownProperties.RuntimeIdentifier);
			}
		}

		/// <summary>
		/// Set the `$(RuntimeIdentifiers)` property to the specified value. If <param name="removeConflictingProperties" /> is `true`, remove
		/// potentially set properties that may break the build with `RuntimeIdentifiers` present. Currently, it means removing the `RuntimeIdentifier`
		/// property.
		/// </summary>
		public static void SetRuntimeIdentifiers (this IShortFormProject project, bool removeConflictingProperties, params AndroidTargetArch[] targetArches)
		{
			if (targetArches == null || targetArches.Length == 0) {
				throw new ArgumentException ("must not be null or empty", nameof (targetArches));
			}

			project.SetProperty (KnownProperties.RuntimeIdentifiers, String.Join (";", targetArches.Select (arch => MonoAndroidHelper.ArchToRid (arch))));
		}

		/// <summary>
		/// Set the `$(RuntimeIdentifiers)` property to the specified value. Remove potentially set properties that may break the build
		/// with `RuntimeIdentifiers` present. Currently, it means removing the `RuntimeIdentifier` property.
		/// </summary>
		public static void SetRuntimeIdentifiers (this IShortFormProject project, params AndroidTargetArch[] targetArches)
		{
			project.SetRuntimeIdentifiers (removeConflictingProperties: true, targetArches);
		}

		public static HashSet<string> GetRuntimeIdentifiers (this XamarinProject project)
		{
			var ridsPropValue = project.GetProperty (KnownProperties.RuntimeIdentifiers);

			if (string.IsNullOrEmpty (ridsPropValue)) {
				return new HashSet<string> () { "android-arm64", "android-x64", };
			}

			return ridsPropValue.Split (';').ToHashSet (StringComparer.OrdinalIgnoreCase);
		}

		public static HashSet<string> GetRuntimeIdentifiersAsAbis (this XamarinProject project)
		{
			return project.GetRuntimeIdentifiers ().Select(r => MonoAndroidHelper.RidToAbi (r)).ToHashSet (StringComparer.OrdinalIgnoreCase);
		}
	}
}
