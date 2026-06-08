//
// AndroidPackageListExtensions.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using System.Linq;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools
{
	public static class AndroidPackageListExtensions
	{
		internal static readonly string runtimeName     = "Mono.Android.DebugRuntime";
		internal static readonly string oldRuntimeName  = "com.novell.monodroid.runtimeservice";
		internal static readonly string platformName    = "Mono.Android.Platform.ApiLevel_{0}";

		public static bool IsCurrentRuntimeInstalled (this IList<AndroidInstalledPackage> packages)
		{
			var version = MonoDroidSdk.SharedRuntimeVersion;
			return packages.Any (p => p.Name == runtimeName && p.Version == version);
		}

		public static bool IsUnknownRuntimeInstalled (this IList<AndroidInstalledPackage> packages)
		{
			return packages.Any (p => p.Name == runtimeName && p.Version == int.MaxValue);
		}

		public static IEnumerable<AndroidInstalledPackage> GetOldRuntimes (this IList<AndroidInstalledPackage> packages)
		{
			var version = MonoDroidSdk.SharedRuntimeVersion;
			return packages.Where (p => (p.Name == runtimeName && p.Version != version) || p.Name == oldRuntimeName);
		}

		public static bool IsCurrentPlatformInstalled (this IList<AndroidInstalledPackage> packages, int apiLevel)
		{
			string name = string.Format (platformName, apiLevel);
			var version = PlatformPackage.GetPlatformPackageVersion (apiLevel, ref name);

			return packages.Any (p => p.Name == name && p.Version == version);
		}

		public static bool IsUnknownPlatformInstalled (this IList<AndroidInstalledPackage> packages, int apiLevel)
		{
			string name = string.Format (platformName, apiLevel);

			return packages.Any (p => p.Name == name && p.Version == int.MaxValue);
		}

		public static bool AreCurrentRuntimeAndPlatformInstalled (this IList<AndroidInstalledPackage> packages, int apiLevel)
		{
			return packages.IsCurrentRuntimeInstalled () && packages.IsCurrentPlatformInstalled (apiLevel);
		}

		// Hopefully they don't have multiple old
		// platforms installed, but just in case...
		public static List<AndroidInstalledPackage> GetOldPlatforms (this IList<AndroidInstalledPackage> packages, int apiLevel)
		{
			string name = string.Format (platformName, apiLevel);
			int version = PlatformPackage.GetPlatformPackageVersion (apiLevel, ref name);
			
			return packages.Where (p => p.Name == name && p.Version != version).ToList ();
		}

		public static IEnumerable<AndroidInstalledPackage> GetOldRuntimesAndPlatforms (this IList<AndroidInstalledPackage> packages, int apiLevel)
		{
			return packages.GetOldRuntimes ().Concat (packages.GetOldPlatforms (apiLevel));
		}

		public static bool ContainsPackage (this IList<AndroidInstalledPackage> packages, string packageName)
		{
			return packages.Any (p => p.Name == packageName);
		}

		public static AndroidInstalledPackage GetPackage (this IList<AndroidInstalledPackage> packages, string packageName)
		{
			return packages.SingleOrDefault (x => x.Name == packageName);
		}
	}
}
