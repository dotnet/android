using System.Diagnostics;

namespace Xamarin.Android.Tools; 

partial class JdkLocations {

	internal IEnumerable<JdkInfo> GetAzulJdks (Action<TraceLevel, string> logger)
	{
		return GetMacOSSystemJdks ("zulu-*.jdk", logger)
			.Concat (GetWindowsFileSystemJdks (Path.Combine ("Zulu", "zulu-*"), logger))
			.Concat (GetWindowsRegistryJdks (logger, @"SOFTWARE\Azul Systems\Zulu", "zulu-*", null, "InstallationPath"))
			.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
	}
}
