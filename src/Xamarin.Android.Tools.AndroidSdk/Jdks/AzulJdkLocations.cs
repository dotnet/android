using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools {

	class AzulJdkLocations : JdkLocations {

		internal static IEnumerable<JdkInfo> GetAzulJdks (Action<TraceLevel, string> logger)
		{
			return GetMacOSSystemJdks ("zulu-*.jdk", logger)
				.Concat (GetWindowsFileSystemJdks (Path.Combine ("Zulu", "zulu-*"), logger))
				.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Azul Systems\Zulu", "zulu-*", null, "InstallationPath"))
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}
	}
}
