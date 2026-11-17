using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	partial class JdkLocations {

		internal IEnumerable<JdkInfo> GetOracleJdks (Action<TraceLevel, string> logger)
		{
			if (!OS.IsWindows) {
				yield break;
			}
			foreach (var path in GetOracleJdkPaths ()) {
				var jdk = JdkInfo.TryGetJdkInfo (path, logger, "Oracle Registry", environmentVariables, workingDirectory);
				if (jdk == null) {
					continue;
				}
				yield return jdk;
			}
		}

		IEnumerable<string> GetOracleJdkPaths ()
		{
			string subkey = @"SOFTWARE\JavaSoft\Java Development Kit";

			foreach (var wow64 in new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 }) {
				var currentVersion = RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey, "CurrentVersion", wow64);

				if (!string.IsNullOrEmpty (currentVersion)) {

					if (RegistryEx.CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64, "bin", "java.exe", environmentVariables, workingDirectory))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64) ?? "";
				}
			}
		}
	}
}
