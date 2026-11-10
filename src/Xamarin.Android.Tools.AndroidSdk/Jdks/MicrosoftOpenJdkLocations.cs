using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class MicrosoftOpenJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetMicrosoftOpenJdks (Action<TraceLevel, string> logger, IDictionary<string, string>? environmentVariables = null)
		{
			return GetMacOSSystemJdks ("microsoft-*.jdk", logger, environmentVariables: environmentVariables)
				.Concat (GetMacOSUserFileSystemJdks (logger, environmentVariables))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Android", "openjdk", "jdk-*"), logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Microsoft", "jdk-*"), logger, environmentVariables: environmentVariables))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Microsoft\JDK", "*", @"hotspot\MSI", "Path", environmentVariables: environmentVariables))
				.Concat (GetWindowsUserFileSystemJdks (logger, environmentVariables))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
