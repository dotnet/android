using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Extensions to make it easier to select components from a full component set.
	/// </summary>
	public static class PublicExtensions
	{
		/// <summary>
		/// Checks whether any components in the passed set are outdated.
		/// </summary>
		/// <returns><c>true</c>, if outdated components were found, <c>false</c> otherwise.</returns>
		/// <param name="components">Components.</param>
		public static bool AnyOutdated (this IList<IAndroidComponent> components)
		{
			return components?.Any (c => c != null && c.NeedsUpdate) ?? false;
		}

		/// <summary>
		/// Checks whether any components in the set aren't preset on the system
		/// </summary>
		/// <returns><c>true</c>, if there are any missing components, <c>false</c> otherwise.</returns>
		/// <param name="components">Components.</param>
		public static bool AnyNotInstalled (this IList<IAndroidComponent> components)
		{
			return components?.Any (c => c != null && !c.Present) ?? false;
		}

		/// <summary>
		/// Returns a list of all outdated or missing components from the passed set/
		/// </summary>
		/// <returns>The outdated or missing components.</returns>
		/// <param name="components">Components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllOutdatedOrMissing (this IList<IAndroidComponent> components, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			var ret = new List <IAndroidComponent> ();
			ret.AddRange (components.AllOutdated (shouldInclude));
			ret.AddRange (components.AllNotInstalled (shouldInclude: shouldInclude));

			return ret;
		}

		/// <summary>
		/// Gets all outdated components from the passed set. The returned list contains outdated components from all
		/// channels.
		/// </summary>
		/// <returns>List of all the outdated components</returns>
		/// <param name="components">Aource list of components</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllOutdated (this IList<IAndroidComponent> components, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components?.Where (c => c != null && c.Present && c.NeedsUpdate && (shouldInclude == null || shouldInclude (c)))?.ToList ();
		}

		/// <summary>
		/// Gets all installed components from the passed set. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of all installed components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="includeOutdated">If set to <c>true</c> include outdated components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllInstalled (this IList<IAndroidComponent> components, bool includeOutdated = true, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components?.Where (c => c != null && c.Present && (includeOutdated || !c.NeedsUpdate) && (shouldInclude == null || shouldInclude(c)))?.ToList ();
		}

		/// <summary>
		/// Gets all components that aren't installed from the passed set. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of or components that aren't installed</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="includeObsolete">If set to <c>true</c> include obsolete components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllNotInstalled (this IList<IAndroidComponent> components, bool includeObsolete = false, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components?.Where (c => c != null && !c.Present && (includeObsolete || !c.Obsolete) && (shouldInclude == null || shouldInclude(c)))?.ToList ();
		}

		/// <summary>
		/// Gets all the components of the specified type from the passed set. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of components of the specified type</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="type">Component type</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllOfType (this IList<IAndroidComponent> components, AndroidComponentType type, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return GetAllOfType (components, type, shouldInclude)?.ToList ();
		}

		/// <summary>
		/// Gets all the platform components from the passed set.
		/// </summary>
		/// <returns>List of platform components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllPlatforms (this IList<IAndroidComponent> components, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return GetAllOfType (components, AndroidComponentType.Platform, shouldInclude)?.ToList ();
		}

		/// <summary>
		/// Gets all the system image components from the passed set. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of system image components.</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="forABI">Select only components that use this ABI</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllSystemImages (this IList<IAndroidComponent> components, AndroidSystemImageAbi forABI = AndroidSystemImageAbi.Any, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return GetAllOfType (components, AndroidComponentType.SystemImage, shouldInclude)?.Where (c => forABI == AndroidSystemImageAbi.Any || (c.Info as AndroidComponentInfoSystemImage).Abi == forABI)?.ToList ();
		}

		/// <summary>
		/// Gets all the components that use the specified API level (platform). The returned list contains components from all channels
		/// </summary>
		/// <returns>List of components using the specified API level</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="apiLevel">API level to match.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllApiLevel (this IList<IAndroidComponent> components, string apiLevel = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return GetAllApiLevel (components, apiLevel, shouldInclude)?.ToList ();
		}

		/// <summary>
		/// Gets all the components with a matching path. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of components with a matching path</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="path">Path.</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		/// <param name="matchPathStartOnly">If <c>true</c> it will check whether the component path starts with the specified one.</param>
		public static IList<IAndroidComponent> AllWithPath (this IList<IAndroidComponent>components, string path, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null, bool matchPathStartOnly = false)
		{
			if (String.IsNullOrEmpty (path))
				return null;
			
			return components?.Where (c => c != null && 
			                          !String.IsNullOrEmpty (c.Path) &&
			                          PathMatches (c) &&
			                          (minimumRevision == null || c.Revision >= minimumRevision) && 
			                          (shouldInclude == null || shouldInclude (c)))?.ToList ();

			bool PathMatches (IAndroidComponent c)
			{
				if (c == null || c.Path == null)
					return false;

				if (matchPathStartOnly)
					return c.Path.StartsWith (path, StringComparison.Ordinal);
				return String.Compare (c.Path, path, StringComparison.Ordinal) == 0;
			}
		}

		/// <summary>
		/// Get all of the Build Tools components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the Build Tools components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllBuildTools (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.BuildTools, minimumRevision, shouldInclude, true);
		}

		/// <summary>
		/// Get all of the Tools components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the Tools components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllTools (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.Tools, minimumRevision, shouldInclude, true);
		}

		/// <summary>
		/// Get all of the Platform Tools components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the Platform Tools components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllPlatformTools(this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.PlatformTools, minimumRevision, shouldInclude);
		}

		/// <summary>
		/// Get all of the Emulator components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the Emulator components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllEmulators (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.Emulator, minimumRevision, shouldInclude);
		}

		/// <summary>
		/// Get all of the CMake components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the CMake components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllCMake (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.CMake, minimumRevision, shouldInclude, true);
		}

		/// <summary>
		/// Get all of the LLDB components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the LLDB components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllLLDB (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.LLDB, minimumRevision, shouldInclude, true);
		}

		/// <summary>
		/// Get all of the NDK components. The returned list contains components from all channels
		/// </summary>
		/// <returns>List of the Emulator components</returns>
		/// <param name="components">Source list of components</param>
		/// <param name="minimumRevision">Minimum revision of the components.</param>
		/// <param name="shouldInclude">An optional delegate which decides whether or not to include the component passed to it</param>
		public static IList<IAndroidComponent> AllNDK (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return components.AllWithPath (Constants.ComponentPaths.NDK, minimumRevision, shouldInclude);
		}

		public static IList<IAndroidComponent> AllAddons (this IList<IAndroidComponent> components, AndroidRevision minimumRevision = null, Func<IAndroidComponent, bool> shouldInclude = null)
		{
			return GetAllOfType (components, AndroidComponentType.Addon, shouldInclude)?.ToList ();
		}

		static IEnumerable<IAndroidComponent> GetAllApiLevel (IList<IAndroidComponent> components, string apiLevel, Func<IAndroidComponent, bool> shouldInclude)
		{
			bool haveApiLevel = !String.IsNullOrEmpty (apiLevel);
			return components?.OfType<IAndroidApiLevel> ()?.
				              Where (c => c != null && (!haveApiLevel || String.Compare(c.ApiLevel, apiLevel, StringComparison.OrdinalIgnoreCase) == 0))?.
				              Select (c => c as IAndroidComponent).
				              Where (c => shouldInclude == null || shouldInclude(c));
		}

		static IEnumerable<IAndroidComponent> GetAllOfType (IList<IAndroidComponent> components, AndroidComponentType type, Func<IAndroidComponent, bool> shouldInclude)
		{
			return components?.Where (c => c != null && c.ComponentType == type && (shouldInclude == null || shouldInclude(c)));
		}
	}
}
