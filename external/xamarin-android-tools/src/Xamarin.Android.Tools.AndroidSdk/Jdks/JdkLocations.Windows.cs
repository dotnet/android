using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal static string? GetWindowsPreferredJdkPath ()
		{
			var wow = RegistryEx.Wow64.Key32;
			var regKey = AndroidSdkWindows.GetMDRegistryKey ();
			if (RegistryEx.CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, AndroidSdkWindows.MDREG_JAVA_SDK, wow, "bin", "java.exe"))
				return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, AndroidSdkWindows.MDREG_JAVA_SDK, wow);
			return null;

		}

		protected static IEnumerable<JdkInfo> GetWindowsFileSystemJdks (string pattern, Action<TraceLevel, string> logger, string? locator = null)
		{
			if (!OS.IsWindows) {
				yield break;
			}

			var roots = new List<string>() {
				// "Compatibility" with existing codebases; should probably be avoided…
				Environment.ExpandEnvironmentVariables ("%ProgramW6432%"),
				Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles),
				Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86),
			};
			foreach (var root in roots) {
				if (!Directory.Exists (root))
					continue;
				IEnumerable<string> homes;
				try {
					homes = Directory.EnumerateDirectories (root, pattern);
				}
				catch (IOException) {
					continue;
				}
				foreach (var home in homes) {
					if (!File.Exists (Path.Combine (home, "bin", "java.exe")))
						continue;
					var loc = locator ?? $"{pattern} via {root}";
					var jdk = JdkInfo.TryGetJdkInfo (home, logger, loc);
					if (jdk == null)
						continue;
					yield return jdk;
				}
			}
		}

		protected static IEnumerable<JdkInfo> GetWindowsRegistryJdks (
				Action<TraceLevel, string> logger,
				string  parentKey,
				string  childKeyGlob,
				string? grandChildKey,
				string  valueName,
				string? locator = null)
		{
			if (!OS.IsWindows) {
				yield break;
			}

			var regex   = ToRegex (childKeyGlob);
			var paths   = new List<(Version version, string path)> ();
			var roots   = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wows    = new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 };
			foreach (var root in roots)
			foreach (var wow in wows) {
				foreach (var subkeyName in RegistryEx.EnumerateSubkeys (root, parentKey, wow)) {
					if (!regex.Match (subkeyName).Success) {
						continue;
					}
					var key	    = $@"{parentKey}\{subkeyName}" +
						(grandChildKey == null ? "" : "\\" + grandChildKey);
					var path    = RegistryEx.GetValueString (root, key, valueName, wow);
					if (path == null) {
						continue;
					}
					var jdk     = JdkInfo.TryGetJdkInfo (path, logger, locator ?? $"Windows Registry @ {parentKey}");
					if (jdk == null) {
						continue;
					}
					yield return jdk;
				}
			}
		}

		static Regex ToRegex (string glob)
		{
			var r   = new StringBuilder ();
			foreach (char c in glob) {
				switch (c) {
					case '*':
						r.Append (".*");
						break;
					case '?':
						r.Append (".");
						break;
					default:
						r.Append (Regex.Escape (c.ToString ()));
						break;
				}
			}
			return new Regex (r.ToString (), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		}
	}
}
