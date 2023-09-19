using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class MicrosoftOpenJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetMicrosoftOpenJdks (Action<TraceLevel, string> logger)
		{
			return GetMacOSSystemJdks ("microsoft-*.jdk", logger)
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Android", "openjdk", "jdk-*"), logger))
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Microsoft", "jdk-*"), logger))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Microsoft\JDK", "*", @"hotspot\MSI", "Path"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
