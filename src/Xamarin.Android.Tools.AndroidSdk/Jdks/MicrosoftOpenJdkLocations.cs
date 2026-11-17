using System.Diagnostics;

namespace Xamarin.Android.Tools; 

partial class JdkLocations {

	internal IEnumerable<JdkInfo> GetMicrosoftOpenJdks (Action<TraceLevel, string> logger)
	{
		return GetMacOSSystemJdks ("microsoft-*.jdk", logger)
			.Concat (GetMacOSUserFileSystemJdks (logger))
			.Concat (GetWindowsFileSystemJdks (Path.Combine ("Android", "openjdk", "jdk-*"), logger))
			.Concat (GetWindowsFileSystemJdks (Path.Combine ("Microsoft", "jdk-*"), logger))
			.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Microsoft\JDK", "*", @"hotspot\MSI", "Path"))
			.Concat (GetWindowsUserFileSystemJdks (logger))
			.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
	}
}
