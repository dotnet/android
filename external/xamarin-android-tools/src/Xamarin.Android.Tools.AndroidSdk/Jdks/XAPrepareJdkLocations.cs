using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class XAPrepareJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetXAPrepareJdks (Action<TraceLevel, string> logger)
		{
			return FromPaths (GetXAPrepareJdkPaths (), logger, "android-toolchain");
		}

		static IEnumerable<string> GetXAPrepareJdkPaths ()
		{
			var androidToolchainDir = Path.Combine (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "android-toolchain");
			if (!Directory.Exists (androidToolchainDir)) {
				return Array.Empty<string> ();
			}
			return Directory.EnumerateDirectories (androidToolchainDir, "jdk*");
		}
	}
}
