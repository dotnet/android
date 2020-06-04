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
		internal static readonly HashSet<string> FrameworkAssembliesToTreatAsUserAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			"Mono.Android.Support.v13.dll",
			"Mono.Android.Support.v4.dll",
			"Xamarin.Android.NUnitLite.dll",
		};

		public static bool IsFrameworkAssembly (string assembly)
		{
			return IsFrameworkAssembly (assembly, false);
		}

		public static bool IsFrameworkAssembly (string assembly, bool checkSdkPath)
		{
			if (IsSharedRuntimeAssembly (assembly)) {
#if MSBUILD
				bool treatAsUser = FrameworkAssembliesToTreatAsUserAssemblies.Contains (Path.GetFileName (assembly));
				// Framework assemblies don't come from outside the SDK Path;
				// user assemblies do
				if (checkSdkPath && treatAsUser && TargetFrameworkDirectories != null) {
					return ExistsInFrameworkPath (assembly);
				}
#endif
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
					.Any (p => assembly.StartsWith (p));
		}
	}
}
