using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public partial class MonoAndroidHelper
	{
		public static string [] TargetFrameworkDirectories;

		internal static readonly string [] FrameworkEmbeddedJarLookupTargets = {
			"Mono.Android.Support.v13.dll",
			"Mono.Android.Support.v4.dll",
			"Xamarin.Android.NUnitLite.dll", // AndroidResources
		};
		internal static readonly string [] FrameworkEmbeddedNativeLibraryAssemblies = {
			"Mono.Data.Sqlite.dll",
			"Mono.Posix.dll",
		};

		public static bool IsFrameworkAssembly (string assembly)
		{
			return IsFrameworkAssembly (assembly, false);
		}

		public static bool IsFrameworkAssembly (string assembly, bool checkSdkPath)
		{
			if (IsSharedRuntimeAssembly (assembly)) {
				return true;
			}
			return TargetFrameworkDirectories == null || !checkSdkPath ? false : ExistsInFrameworkPath (assembly);
		}

		public static bool IsSharedRuntimeAssembly (string assembly)
		{
			return Array.BinarySearch (Profile.SharedRuntimeAssemblies, Path.GetFileName (assembly), StringComparer.OrdinalIgnoreCase) >= 0;
		}

		public static bool ExistsInFrameworkPath (string assembly)
		{
			return TargetFrameworkDirectories
					// TargetFrameworkDirectories will contain a "versioned" directory,
					// e.g. $prefix/lib/xamarin.android/xbuild-frameworks/MonoAndroid/v1.0.
					// Trim off the version.
					.Select (p => Path.GetDirectoryName (p.TrimEnd (Path.DirectorySeparatorChar)))
					.Any (p => assembly.StartsWith (p, StringComparison.OrdinalIgnoreCase));
		}

		static readonly char [] CustomViewMapSeparator = [';'];

		public static Dictionary<string, HashSet<string>> LoadCustomViewMapFile (string mapFile)
		{
			var map = new Dictionary<string, HashSet<string>> ();
			if (!File.Exists (mapFile))
				return map;
			foreach (var s in File.ReadLines (mapFile)) {
				var items = s.Split (CustomViewMapSeparator, count: 2);
				var key = items [0];
				var value = items [1];
				HashSet<string> set;
				if (!map.TryGetValue (key, out set))
					map.Add (key, set = new HashSet<string> ());
				set.Add (value);
			}
			return map;
		}
	}
}
