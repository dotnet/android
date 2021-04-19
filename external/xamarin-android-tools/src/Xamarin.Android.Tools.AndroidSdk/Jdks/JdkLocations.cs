using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal static IEnumerable<JdkInfo> GetPreferredJdks (Action<TraceLevel, string> logger)
		{
			if (OS.IsWindows) {
				var path    = GetWindowsPreferredJdkPath ();
				var jdk     = path == null ? null : JdkInfo.TryGetJdkInfo (path, logger, "Windows Registry");
				if (jdk != null)
					yield return jdk;
			}
			foreach (var jdk in GetUnixPreferredJdks (logger)) {
				yield return jdk;
			}
		}

		protected static IEnumerable<JdkInfo> FromPaths (IEnumerable<string> paths, Action<TraceLevel, string> logger, string locator)
		{
			return paths
				.Select (p => JdkInfo.TryGetJdkInfo (p, logger, locator))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!);
		}
	}
}
