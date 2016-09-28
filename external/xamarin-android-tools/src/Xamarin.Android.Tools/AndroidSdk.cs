using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tools
{
	public class AndroidSdk
	{
		private static AndroidSdkBase sdk;

		public static void Refresh (string androidSdkPath = null, string androidNdkPath = null, string javaSdkPath = null)
		{
			if (OS.IsWindows)
				sdk = new AndroidSdkWindows ();
			else
				sdk = new AndroidSdkUnix ();

			try {
				sdk.Initialize (androidSdkPath ?? sdk.PreferedAndroidSdkPath, androidNdkPath ?? sdk.PreferedAndroidNdkPath,
					javaSdkPath ?? sdk.PreferedJavaSdkPath);
				if (IsInstalled) {
					var levels = GetInstalledPlatformVersions ().Select (l => l.ApiLevel.ToString ()).ToArray ();
					string levelList;
					if (levels == null || levels.Length == 0)
						levelList = "(none)";
					else
						levelList = string.Join (", ", levels);
					AndroidLogger.LogInfo (null, "Found Android SDK. API levels: {0}", levelList);
				} else {
					AndroidLogger.LogInfo (null, "Did not find Android SDK");
				}
			} catch (Exception ex) {
				AndroidLogger.LogError ("Error finding Android/Java SDKs", ex);
			}
		}

		public static IEnumerable<string> GetBuildToolsPaths (string preferredBuildToolsVersion)
		{
			if (!string.IsNullOrEmpty (preferredBuildToolsVersion)) {
				var preferredDir = Path.Combine (AndroidSdkPath, "build-tools", preferredBuildToolsVersion);
				if (Directory.Exists (preferredDir))
					return new[] { preferredDir }.Concat (GetBuildToolsPaths ().Where (p => p!= preferredDir));
			}
			return GetBuildToolsPaths ();
		}

		public static IEnumerable<string> GetBuildToolsPaths ()
		{
			ValidatePath (AndroidSdkPath);

			var buildTools  = Path.Combine (AndroidSdkPath, "build-tools");
			if (Directory.Exists (buildTools)) {
				var preview = Directory.EnumerateDirectories (buildTools)
					.Where(x => TryParseVersion (Path.GetFileName (x)) == null)
					.Select(x => x);

				foreach (var d in preview)
					yield return d;

				var sorted = from p in Directory.EnumerateDirectories (buildTools)
					let version = TryParseVersion (Path.GetFileName (p))
						where version != null
					orderby version descending
					select p;

				foreach (var d in sorted)
					yield return d;
			}
			var ptPath  = Path.Combine (AndroidSdkPath, "platform-tools");
			if (Directory.Exists (ptPath))
				yield return ptPath;
		}

		static Version TryParseVersion (string v)
		{
			Version version;
			if (Version.TryParse (v, out version))
				return version;
			return null;
		}

		// it was useful when android-21 was android-L, or android-23 was android-MNC.
		// We will use this when similar preview release is out.
		static string ToApiName (int apiLevel)
		{
			return apiLevel.ToString ();
		}

		static string ValidatePath (string path)
		{
			if (String.IsNullOrEmpty (path))
				throw new InvalidOperationException ("This property is not valid when the SDK is not installed");
			return path;
		}

		public static string GetPlatformDirectory (int apiLevel)
		{
			return Path.Combine (AndroidSdkPath, "platforms", "android-" + ToApiName (apiLevel));
		}

		public static string GetPlatformDirectory (string osVersion)
		{
			var level = AndroidVersion.TryOSVersionToApiLevel (osVersion);
			if (level == 0)
				return null;
			return GetPlatformDirectory (level);
		}

		public static bool IsPlatformInstalled (int apiLevel)
		{
			return apiLevel != 0 && Directory.Exists (GetPlatformDirectory (apiLevel));
		}

		public static IEnumerable<AndroidVersion> GetInstalledPlatformVersions ()
		{
			var knownAndInstalledSdkLevels = AndroidVersion.KnownVersions.Where (v => IsPlatformInstalled (v.ApiLevel));

			return knownAndInstalledSdkLevels.Where (version => {
				var apiLevel = MonoDroidSdk.GetApiLevelForFrameworkVersion (version.OSVersion);
				return MonoDroidSdk.IsSupportedFrameworkLevel (apiLevel);
			});
		}

		public static bool IsInstalled {
			get {
				return !string.IsNullOrEmpty (AndroidSdkPath) && !string.IsNullOrEmpty (JavaSdkPath);
			}
		}

		public static string AndroidNdkPath {
			get { return sdk.AndroidNdkPath; }
		}

		public static string AndroidSdkPath {
			get { return sdk.AndroidSdkPath; }
		}

		public static string JavaSdkPath {
			get { return sdk.JavaSdkPath; }
		}

		public static string AndroidNdkHostPlatform {
			get { return sdk.NdkHostPlatform; }
		}
	}
}

