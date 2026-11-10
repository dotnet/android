using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class EclipseAdoptiumJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetEclipseAdoptiumJdks (Action<TraceLevel, string> logger, IDictionary<string, string>? environmentVariables = null)
		{
			return GetMacOSSystemJdks ("temurin-*.jdk", logger, environmentVariables: environmentVariables)
				.Concat (GetMacOSSystemJdks ("adoptopenjdk-*.jdk", logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("AdoptOpenJDK", "jdk-*"), logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\AdoptOpenJDK\JDK", "*", @"hotspot\MSI", "Path", environmentVariables: environmentVariables))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Eclipse Foundation", "jdk-*"), logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Eclipse Foundation\JDK", "*", @"hotspot\MSI", "Path", environmentVariables: environmentVariables))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
