using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class OracleJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetOracleJdks (Action<TraceLevel, string> logger)
		{
			if (!OS.IsWindows) {
				yield break;
			}
			foreach (var path in GetOracleJdkPaths ()) {
				var jdk = JdkInfo.TryGetJdkInfo (path, logger, "Oracle Registry");
				if (jdk == null) {
					continue;
				}
				yield return jdk;
			}
		}

		static IEnumerable<string> GetOracleJdkPaths ()
		{
			string subkey = @"SOFTWARE\JavaSoft\Java Development Kit";

			foreach (var wow64 in new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 }) {
				string key_name = string.Format (@"{0}\{1}\{2}", "HKLM", subkey, "CurrentVersion");
				var currentVersion = RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey, "CurrentVersion", wow64);

				if (!string.IsNullOrEmpty (currentVersion)) {

					if (RegistryEx.CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64, "bin", "java.exe"))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64) ?? "";
				}
			}
		}
	}
}
