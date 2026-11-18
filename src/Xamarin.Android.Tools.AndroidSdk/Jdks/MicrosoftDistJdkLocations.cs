using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal IEnumerable<JdkInfo> GetMicrosoftDistJdks (Action<TraceLevel, string> logger)
		{
			return FromPaths (GetMacOSMicrosoftDistJdkPaths (), logger, "$HOME/Library/Developer/Xamarin/jdk")
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Android", "jdk", "microsoft_dist_openjdk_*"), logger, locator: "legacy microsoft_dist_openjdk"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}

		IEnumerable<string> GetMacOSMicrosoftDistJdkPaths ()
		{
			if (!OS.IsMac) {
				return Array.Empty<string> ();
			}

			var jdks    = AppDomain.CurrentDomain.GetData ($"GetMacOSMicrosoftJdkPaths jdks override! {typeof (JdkInfo).AssemblyQualifiedName}")
				?.ToString ();
			if (jdks == null) {
				var home    = ProcessUtils.GetUnixHome (environmentVariables);
				if (home is not { Length: > 0 })
					return Array.Empty<string> ();
				jdks        = Path.Combine (home, "Library", "Developer", "Xamarin", "jdk");
			}
			jdks = ProcessUtils.ResolvePath (jdks, workingDirectory);
			if (jdks == null || !Directory.Exists (jdks))
				return Enumerable.Empty <string> ();

			return Directory.EnumerateDirectories (jdks);
		}
	}
}
