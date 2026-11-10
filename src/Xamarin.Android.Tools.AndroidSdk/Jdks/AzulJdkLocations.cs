using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class AzulJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetAzulJdks (Action<TraceLevel, string> logger, IDictionary<string, string>? environmentVariables = null)
		{
			return GetMacOSSystemJdks ("zulu-*.jdk", logger, environmentVariables: environmentVariables)
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Zulu", "zulu-*"), logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Azul Systems\Zulu", "zulu-*", null, "InstallationPath", environmentVariables: environmentVariables))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
