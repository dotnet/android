using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		static IEnumerable<JdkInfo> GetUnixPreferredJdks (Action<TraceLevel, string> logger)
		{
			return GetUnixConfiguredJdkPaths (logger)
				.Select (p => JdkInfo.TryGetJdkInfo (p, logger, "monodroid-config.xml"))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!)
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}

		static IEnumerable<string> GetUnixConfiguredJdkPaths (Action<TraceLevel, string> logger)
		{
			var config = AndroidSdkUnix.GetUnixConfigFile (logger);

			foreach (var java_sdk in config.Elements ("java-sdk")) {
				var path    = (string?) java_sdk.Attribute ("path");
				if (path != null && !string.IsNullOrEmpty (path)) {
					yield return path;
				}
			}
		}

		const   string  MacOSJavaVirtualMachinesRoot    = "/Library/Java/JavaVirtualMachines";

		protected static IEnumerable<JdkInfo> GetMacOSUserFileSystemJdks (Action<TraceLevel, string> logger)
		{
			if (!OS.IsMac) {
				return Array.Empty<JdkInfo> ();
			}

			// Search ~/Library/Android/*jdk*/ (matches microsoft-21.jdk, jdk-21, etc.)
			var libraryAndroidRoot = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Android");
			if (!Directory.Exists (libraryAndroidRoot)) {
				return Array.Empty<JdkInfo> ();
			}

			IEnumerable<string> dirs;
			try {
				dirs = Directory.EnumerateDirectories (libraryAndroidRoot, "*jdk*");
			}
			catch (IOException) {
				return Array.Empty<JdkInfo> ();
			}

			var toHome = Path.Combine ("Contents", "Home");
			var paths = new List<string> ();
			foreach (var dir in dirs) {
				// Check for macOS .jdk bundle structure (Contents/Home)
				var bundleHome = Path.Combine (dir, toHome);
				if (Directory.Exists (bundleHome)) {
					paths.Add (bundleHome);
				} else {
					// Flat JDK structure - let TryGetJdkInfo validate
					paths.Add (dir);
				}
			}

			return FromPaths (paths, logger, "~/Library/Android/*jdk*/");
		}

		protected static IEnumerable<JdkInfo> GetMacOSSystemJdks (string pattern, Action<TraceLevel, string> logger, string? locator = null)
		{
			if (!OS.IsMac) {
				return Array.Empty<JdkInfo>();
			}
			locator = locator ?? Path.Combine (MacOSJavaVirtualMachinesRoot, pattern);
			return FromPaths (GetMacOSSystemJvmPaths (pattern), logger, locator);
		}

		static IEnumerable<string> GetMacOSSystemJvmPaths (string pattern)
		{
			var root    = MacOSJavaVirtualMachinesRoot;
			var toHome  = Path.Combine ("Contents", "Home");
			var jdks    = AppDomain.CurrentDomain.GetData ($"GetMacOSMicrosoftJdkPaths jdks override! {typeof (JdkInfo).AssemblyQualifiedName}")
				?.ToString ();
			if (jdks != null) {
				root    = jdks;
				toHome  = "";
				pattern = "*";
			}
			if (!Directory.Exists (root)) {
				yield break;
			}
			foreach (var dir in Directory.EnumerateDirectories (root, pattern)) {
				var home = Path.Combine (dir, toHome);
				if (!Directory.Exists (home))
					continue;
				yield return home;
			}
		}
	}
}
