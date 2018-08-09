using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// ValidateJavaVersion's job is to shell out to java and javac to detect their version
	/// </summary>
	public class ValidateJavaVersion : Task
	{
		public string TargetFrameworkVersion { get; set; }

		public string AndroidSdkBuildToolsVersion { get; set; }

		public string JavaSdkPath { get; set; }

		public string JavaToolExe { get; set; }

		public string JavacToolExe { get; set; }

		public string LatestSupportedJavaVersion { get; set; }

		public string MinimumSupportedJavaVersion { get; set; }

		[Output]
		public string MinimumRequiredJdkVersion { get; set; }

		[Output]
		public string JdkVersion { get; set; }

		public override bool Execute ()
		{
			if (!ValidateJava (TargetFrameworkVersion, AndroidSdkBuildToolsVersion))
				return false;

			Log.LogDebugMessage ($"{nameof (ValidateJavaVersion)} Outputs:");
			Log.LogDebugMessage ($"  {nameof (JdkVersion)}: {JdkVersion}");
			Log.LogDebugMessage ($"  {nameof (MinimumRequiredJdkVersion)}: {MinimumRequiredJdkVersion}");

			return !Log.HasLoggedErrors;
		}

		// `java -version` will produce values such as:
		//  java version "9.0.4"
		//  java version "1.8.0_77"
		static readonly Regex JavaVersionRegex = new Regex (@"version ""(?<version>[\d\.]+)(_d+)?[^""]*""");

		// `javac -version` will produce values such as:
		//  javac 9.0.4
		//  javac 1.8.0_77
		static readonly Regex JavacVersionRegex = new Regex (@"(?<version>[\d\.]+)(_d+)?");

		bool ValidateJava (string targetFrameworkVersion, string buildToolsVersion)
		{
			var java = JavaToolExe ?? (OS.IsWindows ? "java.exe" : "java");
			var javac = JavacToolExe ?? (OS.IsWindows ? "javac.exe" : "javac");

			return ValidateJava (java, JavaVersionRegex, targetFrameworkVersion, buildToolsVersion) &&
				ValidateJava (javac, JavacVersionRegex, targetFrameworkVersion, buildToolsVersion);
		}

		bool ValidateJava (string javaExe, Regex versionRegex, string targetFrameworkVersion, string buildToolsVersion)
		{
			Version requiredJavaForFrameworkVersion = GetJavaVersionForFramework (targetFrameworkVersion);
			Version requiredJavaForBuildTools = GetJavaVersionForBuildTools (buildToolsVersion);

			Version required = requiredJavaForFrameworkVersion > requiredJavaForBuildTools ? requiredJavaForFrameworkVersion : requiredJavaForBuildTools;

			MinimumRequiredJdkVersion = required.ToString ();

			try {
				var versionNumber = GetVersionFromTool (javaExe, versionRegex);
				if (versionNumber != null) {
					Log.LogMessage (MessageImportance.Normal, $"Found Java SDK version {versionNumber}.");
					if (versionNumber < requiredJavaForFrameworkVersion) {
						Log.LogCodedError ("XA0031", $"Java SDK {requiredJavaForFrameworkVersion} or above is required when targeting FrameworkVersion {targetFrameworkVersion}.");
					}
					if (versionNumber < requiredJavaForBuildTools) {
						Log.LogCodedError ("XA0032", $"Java SDK {requiredJavaForBuildTools} or above is required when using build-tools {buildToolsVersion}.");
					}
					if (versionNumber > Version.Parse (LatestSupportedJavaVersion)) {
						Log.LogCodedError ("XA0030", $"Building with JDK Version `{versionNumber}` is not supported. Please install JDK version `{LatestSupportedJavaVersion}`. See https://aka.ms/xamarin/jdk9-errors");
					}
				}
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				Log.LogCodedWarning ("XA0034", $"Failed to get the Java SDK version. Please ensure you have Java {required} or above installed.");
				return false;
			}

			return !Log.HasLoggedErrors;
		}

		Version GetVersionFromTool (string javaExe, Regex versionRegex)
		{
			var javaTool = Path.Combine (JavaSdkPath, "bin", javaExe);
			var key = new Tuple<string, string> (nameof (ValidateJavaVersion), javaTool);
			var cached = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.AppDomain) as Version;
			if (cached != null) {
				Log.LogDebugMessage ($"Using cached value for `{javaTool} -version`: {cached}");
				JdkVersion = cached.ToString ();
				return cached;
			}

			var sb = new StringBuilder ();
			MonoAndroidHelper.RunProcess (javaTool, "-version", (s, e) => {
				if (!string.IsNullOrEmpty (e.Data))
					sb.AppendLine (e.Data);
			}, (s, e) => {
				if (!string.IsNullOrEmpty (e.Data))
					sb.AppendLine (e.Data);
			});
			var versionInfo = sb.ToString ();
			var versionNumberMatch = versionRegex.Match (versionInfo);
			Version versionNumber;
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value, out versionNumber)) {
				BuildEngine4.RegisterTaskObject (key, versionNumber, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: false);
				JdkVersion = versionNumberMatch.Groups ["version"].Value;
				return versionNumber;
			} else {
				Log.LogCodedWarning ("XA0033", $"Failed to get the Java SDK version as it does not appear to contain a valid version number. `{javaExe} -version` returned: ```{versionInfo}```");
				return null;
			}
		}

		Version GetJavaVersionForFramework (string targetFrameworkVersion)
		{
			var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (targetFrameworkVersion);
			if (apiLevel >= 24)
				return new Version (1, 8);
			else if (apiLevel == 23)
				return new Version (1, 7);
			else
				return new Version (1, 6);
		}

		Version GetJavaVersionForBuildTools (string buildToolsVersion)
		{
			Version buildTools;
			if (!Version.TryParse (buildToolsVersion, out buildTools)) {
				return Version.Parse (LatestSupportedJavaVersion);
			}
			if (buildTools >= new Version (24, 0, 1))
				return new Version (1, 8);
			return Version.Parse (MinimumSupportedJavaVersion);
		}
	}
}
