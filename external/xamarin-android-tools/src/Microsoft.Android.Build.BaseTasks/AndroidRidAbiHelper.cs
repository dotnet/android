// https://github.com/xamarin/xamarin-android/blob/34acbbae6795854cc4e9f8eb7167ab011e0266b4/src/Xamarin.Android.Build.Tasks/Utilities/MonoAndroidHelper.cs#L251

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Build.Tasks
{
	public static class AndroidRidAbiHelper
	{
		static readonly string[] ValidAbis = new[]{
			"arm64-v8a",
			"armeabi-v7a",
			"x86",
			"x86_64",
		};

		public static string GetNativeLibraryAbi (string lib)
		{
			// The topmost directory the .so file is contained within
			var dir = Path.GetFileName (Path.GetDirectoryName (lib)).ToLowerInvariant ();
			if (dir.StartsWith ("interpreter-", StringComparison.Ordinal)) {
				dir = dir.Substring (12);
			}
			if (ValidAbis.Contains (dir)) {
				return dir;
			}
			return null;
		}

		public static string GetNativeLibraryAbi (ITaskItem lib)
		{
			// If Abi is explicitly specified, simply return it.
			var lib_abi = lib.GetMetadata ("Abi");

			if (!string.IsNullOrWhiteSpace (lib_abi))
				return lib_abi;

			// Try to figure out what type of abi this is from the path
			// First, try nominal "Link" path.
			var link = lib.GetMetadata ("Link");
			if (!string.IsNullOrWhiteSpace (link)) {
				var linkdirs = link.ToLowerInvariant ().Split ('/', '\\');
				lib_abi = ValidAbis.Where (p => linkdirs.Contains (p)).FirstOrDefault ();
			}

			// Check for a RuntimeIdentifier
			var rid = lib.GetMetadata ("RuntimeIdentifier");
			if (!string.IsNullOrWhiteSpace (rid)) {
				lib_abi = RuntimeIdentifierToAbi (rid);
			}
			
			if (!string.IsNullOrWhiteSpace (lib_abi))
				return lib_abi;

			// If not resolved, use ItemSpec
			return GetNativeLibraryAbi (lib.ItemSpec);
		}

		/// <summary>
		/// Converts .NET 5 RIDs to Android ABIs or an empty string if no match.
		/// 
		/// Known RIDs:
		/// "android.21-arm64" -> "arm64-v8a"
		/// "android.21-arm"   -> "armeabi-v7a"
		/// "android.21-x86"   -> "x86"
		/// "android.21-x64"   -> "x86_64"
		/// "android-arm64"    -> "arm64-v8a"
		/// "android-arm"      -> "armeabi-v7a"
		/// "android-x86"      -> "x86"
		/// "android-x64"      -> "x86_64"
		/// </summary>
		public static string RuntimeIdentifierToAbi (string runtimeIdentifier)
		{
			if (string.IsNullOrEmpty (runtimeIdentifier) || !runtimeIdentifier.StartsWith ("android", StringComparison.OrdinalIgnoreCase)) {
				return "";
			}
			if (runtimeIdentifier.EndsWith ("-arm64", StringComparison.OrdinalIgnoreCase)) {
				return "arm64-v8a";
			}
			if (runtimeIdentifier.EndsWith ("-arm", StringComparison.OrdinalIgnoreCase)) {
				return "armeabi-v7a";
			}
			if (runtimeIdentifier.EndsWith ("-x86", StringComparison.OrdinalIgnoreCase)) {
				return "x86";
			}
			if (runtimeIdentifier.EndsWith ("-x64", StringComparison.OrdinalIgnoreCase)) {
				return "x86_64";
			}
			return "";
		}
	}
}
