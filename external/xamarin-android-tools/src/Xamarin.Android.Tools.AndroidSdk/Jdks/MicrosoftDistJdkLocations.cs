using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class MicrosoftDistJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetMicrosoftDistJdks (Action<TraceLevel, string> logger)
		{
			return FromPaths (GetMacOSMicrosoftDistJdkPaths (), logger, "$HOME/Library/Developer/Xamarin/jdk")
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Android", "jdk", "microsoft_dist_openjdk_*"), logger, locator: "legacy microsoft_dist_openjdk"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}

		static IEnumerable<string> GetMacOSMicrosoftDistJdkPaths ()
		{
			var jdks    = AppDomain.CurrentDomain.GetData ($"GetMacOSMicrosoftJdkPaths jdks override! {typeof (JdkInfo).AssemblyQualifiedName}")
				?.ToString ();
			if (jdks == null) {
				var home    = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				jdks        = Path.Combine (home, "Library", "Developer", "Xamarin", "jdk");
			}
			if (!Directory.Exists (jdks))
				return Enumerable.Empty <string> ();

			return Directory.EnumerateDirectories (jdks);
		}
	}
}
