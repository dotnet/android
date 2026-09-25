using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	// Source-linked by both test harnesses; neither uses MSBuild to acquire these inputs.
	internal static class GuestReadinessTestSources
	{
		internal static bool Enabled => IsEnabled (Environment.OSVersion.Platform, Environment.GetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION"));

		internal static bool IsEnabled (PlatformID platform, string optIn)
		{
			if (platform != PlatformID.Win32NT || string.IsNullOrEmpty (optIn))
				return false;
			if (optIn != "1")
				throw new InvalidOperationException ("ANDROID_GUEST_READINESS_TEST_ACQUISITION must be absent or exactly 1.");
			return true;
		}

		internal static string GetNuGetConfig ()
		{
			var path = Environment.GetEnvironmentVariable ("RESTORECONFIGFILE");
			if (string.IsNullOrWhiteSpace (path) || !Path.IsPathFullyQualified (path) || !File.Exists (path))
				throw new InvalidOperationException ("Diagnostic test acquisition requires an existing absolute RESTORECONFIGFILE.");
			return Path.GetFullPath (path);
		}

		internal static string GetDownloadUrl (string url)
		{
			const string central = "https://repo1.maven.org/maven2/";
			const string mirror = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public-maven/maven/v1/";
			if (!Enabled || !url.StartsWith (central, StringComparison.Ordinal))
				return url;
			var suffix = url.Substring (central.Length);
			if (suffix.Length == 0)
				return url;
			foreach (var segment in suffix.Split ('/')) {
				if (segment.Length == 0 || segment == "." || segment == "..")
					return url;
				foreach (var c in segment) {
					if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
							!(c >= '0' && c <= '9') && c != '-' && c != '_' && c != '.')
						return url;
				}
			}
			return mirror + suffix;
		}
	}
}
