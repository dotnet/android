using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal static IEnumerable<JdkInfo> GetPreferredJdks (Action<TraceLevel, string> logger, IDictionary<string, string>? environmentVariables = null)
		{
			if (OS.IsWindows) {
				var path    = GetWindowsPreferredJdkPath (environmentVariables);
				var jdk     = path == null ? null : JdkInfo.TryGetJdkInfo (path, logger, "Windows Registry", environmentVariables);
				if (jdk != null)
					yield return jdk;
			}
			foreach (var jdk in GetUnixPreferredJdks (logger, environmentVariables)) {
				yield return jdk;
			}
		}

		protected static IEnumerable<JdkInfo> FromPaths (IEnumerable<string> paths, Action<TraceLevel, string> logger, string locator, IDictionary<string, string>? environmentVariables = null)
		{
			return paths
				.Select (p => JdkInfo.TryGetJdkInfo (p, logger, locator, environmentVariables))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!);
		}
	}
}
