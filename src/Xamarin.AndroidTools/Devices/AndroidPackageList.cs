// 
// AndroidPackageList.cs
//  
// Authors:
//       Jonathan Pobst <jpobst@xamarin.com>
// 
// Copyright 2011 Xamarin Inc. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools
{
	[Obsolete ("Use AndroidPackageListExtensions")]
	public class AndroidPackageList
	{
		string runtimeName = "Mono.Android.DebugRuntime";
		string oldRuntimeName = "com.novell.monodroid.runtimeservice";
		string platformName = "Mono.Android.Platform.ApiLevel_{0}";

		public List<AndroidInstalledPackage> Packages { get; private set; }

		public AndroidPackageList (List<AndroidInstalledPackage> packages)
		{
			Packages = packages;
		}

		public bool IsCurrentRuntimeInstalled (int runtimeVersion)
		{
			return Packages.Any (p => p.Name == runtimeName && p.Version == runtimeVersion);
		}

		public bool IsUnknownRuntimeInstalled ()
		{
			return Packages.Any (p => p.Name == runtimeName && p.Version == int.MaxValue);
		}

		public List<AndroidInstalledPackage> GetOldRuntimes (int runtimeVersion)
		{
			return Packages.Where (p => (p.Name == runtimeName && p.Version != runtimeVersion) || p.Name == oldRuntimeName).ToList ();
		}

		public bool IsCurrentPlatformInstalled (string apiLevel, int runtimeVersion)
		{
			string name = string.Format (platformName, apiLevel);

			return Packages.Any (p => p.Name == name && p.Version == runtimeVersion);
		}

		public bool IsUnknownPlatformInstalled (string apiLevel)
		{
			string name = string.Format (platformName, apiLevel);

			return Packages.Any (p => p.Name == name && p.Version == int.MaxValue);
		}

		public bool AreCurrentRuntimeAndPlatformInstalled (string apiLevel, int runtimeVersion)
		{
			return IsCurrentRuntimeInstalled (runtimeVersion) && IsCurrentPlatformInstalled (apiLevel, runtimeVersion);
		}

		// Hopefully they don't have multiple old
		// platforms installed, but just in case...
		public List<AndroidInstalledPackage> GetOldPlatforms (string apiLevel, int runtimeVersion)
		{
			string name = string.Format (platformName, apiLevel);

			return Packages.Where (p => p.Name == name && p.Version != runtimeVersion).ToList ();
		}

		public List<AndroidInstalledPackage> GetOldRuntimesAndPlatforms (string apiLevel, int runtimeVersion)
		{
			var runtimes = GetOldRuntimes (runtimeVersion);

			runtimes.AddRange (GetOldPlatforms (apiLevel, runtimeVersion));

			return runtimes;
		}

		public bool ContainsPackage (string packageName)
		{
			return Packages.Any (p => p.Name == packageName);
		}

		public AndroidInstalledPackage GetPackage (string packageName)
		{
			return Packages.Where (x => x.Name == packageName).SingleOrDefault ();
		}
	}
}