using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class EclipseAdoptiumJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetEclipseAdoptiumJdks (Action<TraceLevel, string> logger)
		{
			return GetMacOSSystemJdks ("temurin-*.jdk", logger)
				.Concat (GetMacOSSystemJdks ("adoptopenjdk-*.jdk", logger))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("AdoptOpenJDK", "jdk-*"), logger))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\AdoptOpenJDK\JDK", "*", @"hotspot\MSI", "Path"))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Eclipse Foundation", "jdk-*"), logger))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Eclipse Foundation\JDK", "*", @"hotspot\MSI", "Path"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
