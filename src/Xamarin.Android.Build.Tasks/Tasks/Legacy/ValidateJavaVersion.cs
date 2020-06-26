using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tasks.Legacy
{
	/// <summary>
	/// ValidateJavaVersion's job is to shell out to java and javac to detect their version
	/// </summary>
	public class ValidateJavaVersion : Xamarin.Android.Tasks.ValidateJavaVersion
	{
		public string AndroidSdkBuildToolsVersion { get; set; }

		public string TargetFrameworkVersion { get; set; }

		protected override bool ValidateJava (string javaExe, Regex versionRegex)
		{
			Version requiredJavaForFrameworkVersion = GetJavaVersionForFramework ();
			Version requiredJavaForBuildTools = GetJavaVersionForBuildTools ();
			Version required = requiredJavaForFrameworkVersion > requiredJavaForBuildTools ? requiredJavaForFrameworkVersion : requiredJavaForBuildTools;

			MinimumRequiredJdkVersion = required.ToString ();

			try {
				var versionNumber = GetVersionFromTool (javaExe, versionRegex);
				if (versionNumber != null) {
					Log.LogMessage (MessageImportance.Normal, $"Found Java SDK version {versionNumber}.");
					if (versionNumber < requiredJavaForFrameworkVersion) {
						Log.LogCodedError ("XA0031", Properties.Resources.XA0031, requiredJavaForFrameworkVersion, $"$(TargetFrameworkVersion) {TargetFrameworkVersion}");
					}
					if (versionNumber < requiredJavaForBuildTools) {
						Log.LogCodedError ("XA0032", Properties.Resources.XA0032, requiredJavaForBuildTools, AndroidSdkBuildToolsVersion);
					}
					CheckJavaGreaterThanLatestSupported (versionNumber);
				}
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				Log.LogCodedWarning ("XA0034", Properties.Resources.XA0034, required);
				return false;
			}

			return !Log.HasLoggedErrors;
		}

		Version GetJavaVersionForFramework ()
		{
			var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			if (apiLevel >= 30) {
				// At present, it *looks like* API-R works with Build-tools r29, but
				// historically API-X requires Build-tools rX, so if/when API-30
				// requires Build-tools r30, it will require JDK11.
				// return new Version (11, 0);
				return new Version (1, 8);
			}
			if (apiLevel >= 24)
				return new Version (1, 8);
			else if (apiLevel == 23)
				return new Version (1, 7);
			else
				return new Version (1, 6);
		}

		Version GetJavaVersionForBuildTools ()
		{
			string buildToolsVersionString = AndroidSdkBuildToolsVersion;
			if (buildToolsVersionString != null) {
				int index = buildToolsVersionString.IndexOf ('-');
				if (index != -1)
					buildToolsVersionString = buildToolsVersionString.Substring (0, index);
			}
			Version buildTools;
			if (!Version.TryParse (buildToolsVersionString, out buildTools)) {
				return Version.Parse (LatestSupportedJavaVersion);
			}
			if (buildTools >= new Version (30, 0, 0))
				return new Version (11, 0);
			if (buildTools >= new Version (24, 0, 1))
				return new Version (1, 8);
			return Version.Parse (MinimumSupportedJavaVersion);
		}
	}
}
