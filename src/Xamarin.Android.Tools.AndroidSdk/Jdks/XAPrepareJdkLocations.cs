using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal IEnumerable<JdkInfo> GetXAPrepareJdks (Action<TraceLevel, string> logger)
		{
			return FromPaths (GetXAPrepareJdkPaths (), logger, "android-toolchain");
		}

		IEnumerable<string> GetXAPrepareJdkPaths ()
		{
			var home = OS.IsWindows ? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile) : ProcessUtils.GetUnixHome (environmentVariables);
			if (home is not { Length: > 0 })
				return Array.Empty<string> ();
			var androidToolchainDir = Path.Combine (home, "android-toolchain");
			if (!Directory.Exists (androidToolchainDir)) {
				return Array.Empty<string> ();
			}
			return Directory.EnumerateDirectories (androidToolchainDir, "jdk*");
		}
	}
}
