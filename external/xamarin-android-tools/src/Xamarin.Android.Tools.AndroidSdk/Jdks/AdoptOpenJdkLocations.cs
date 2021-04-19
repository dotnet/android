using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class AdoptOpenJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetAdoptOpenJdks (Action<TraceLevel, string> logger)
		{
			return GetMacOSSystemJdks ("adoptopenjdk-*.jdk", logger)
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("AdoptOpenJDK", "jdk-*"), logger))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\AdoptOpenJDK\JDK", "*", @"hotspot\MSI", "Path"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
