using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class VSAndroidJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetVSAndroidJdks (Action<TraceLevel, string> logger)
		{
			if (!OS.IsWindows) {
				yield break;
			}
			var root        = RegistryEx.LocalMachine;
			var wows        = new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 };
			var subKey      = @"SOFTWARE\Microsoft\VisualStudio\Android";
			var valueName   = "JavaHome";

			foreach (var wow in wows) {
				if (!RegistryEx.CheckRegistryKeyForExecutable (root, subKey, valueName, wow, "bin", "java.exe")) {
					continue;
				}
				var path    = RegistryEx.GetValueString (root, subKey, valueName, wow) ?? "";
				if (string.IsNullOrEmpty (path)) {
					continue;
				}
				var jdk     = JdkInfo.TryGetJdkInfo (path, logger, subKey);
				if (jdk == null) {
					continue;
				}
				yield return jdk;
			}
		}
	}
}
