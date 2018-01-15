using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools
{
	public class AndroidSdkInfo
	{
		AndroidSdkBase sdk;

		public AndroidSdkInfo (Action<TraceLevel, string> logger, string androidSdkPath = null, string androidNdkPath = null, string javaSdkPath = null)
		{
			if (logger == null)
				throw new ArgumentNullException (nameof (logger));

			sdk = CreateSdk (logger);
			sdk.Initialize (androidSdkPath, androidNdkPath, javaSdkPath);

			// shouldn't happen, in that sdk.Initialize() should throw instead
			if (string.IsNullOrEmpty (AndroidSdkPath))
				throw new InvalidOperationException ($"Could not determine Android SDK location. Please provide `{nameof (androidSdkPath)}`.");
			if (string.IsNullOrEmpty (JavaSdkPath))
				throw new InvalidOperationException ($"Could not determine Java SDK location. Please provide `{nameof (javaSdkPath)}`.");
		}

		static AndroidSdkBase CreateSdk (Action<TraceLevel, string> logger)
		{
			return OS.IsWindows
				? (AndroidSdkBase) new AndroidSdkWindows (logger)
				: (AndroidSdkBase) new AndroidSdkUnix (logger);
		}

		public IEnumerable<string> GetBuildToolsPaths (string preferredBuildToolsVersion)
		{
			if (!string.IsNullOrEmpty (preferredBuildToolsVersion)) {
				var preferredDir = Path.Combine (AndroidSdkPath, "build-tools", preferredBuildToolsVersion);
				if (Directory.Exists (preferredDir))
					return new[] { preferredDir }.Concat (GetBuildToolsPaths ().Where (p => p!= preferredDir));
			}
			return GetBuildToolsPaths ();
		}

		public IEnumerable<string> GetBuildToolsPaths ()
		{
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

		public IEnumerable<AndroidVersion> GetInstalledPlatformVersions (AndroidVersions versions)
		{
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));
			return versions.InstalledBindingVersions
				.Where (p => TryGetPlatformDirectoryFromApiLevel (p.Id, versions) != null) ;
		}

		public string GetPlatformDirectory (int apiLevel)
		{
			return GetPlatformDirectoryFromId (apiLevel.ToString ());
		}

		public string GetPlatformDirectoryFromId (string id)
		{
			return Path.Combine (AndroidSdkPath, "platforms", "android-" + id);
		}

		public string TryGetPlatformDirectoryFromApiLevel (string idOrApiLevel, AndroidVersions versions)
		{
			var id  = versions.GetIdFromApiLevel (idOrApiLevel);
			var dir = GetPlatformDirectoryFromId (id);

			if (Directory.Exists (dir))
				return dir;

			var level   = versions.GetApiLevelFromId (id);
			dir         = level.HasValue ? GetPlatformDirectory (level.Value) : null;
			if (dir != null && Directory.Exists (dir))
				return dir;

			return null;
		}

		public bool IsPlatformInstalled (int apiLevel)
		{
			return apiLevel != 0 && Directory.Exists (GetPlatformDirectory (apiLevel));
		}

		public string AndroidNdkPath {
			get { return sdk.AndroidNdkPath; }
		}

		public string AndroidSdkPath {
			get { return sdk.AndroidSdkPath; }
		}

		public string JavaSdkPath {
			get { return sdk.JavaSdkPath; }
		}

		public string AndroidNdkHostPlatform {
			get { return sdk.NdkHostPlatform; }
		}

		public static void SetPreferredAndroidNdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidNdkPath(path);
		}

		static void DefaultConsoleLogger (TraceLevel level, string message)
		{
			switch (level) {
			case TraceLevel.Error:
				Console.Error.WriteLine (message);
				break;
			default:
				Console.WriteLine (message);
				break;
			}
		}

		public static void SetPreferredAndroidSdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidSdkPath (path);
		}

		public static void SetPreferredJavaSdkPath (string path, Action<TraceLevel, string> logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredJavaSdkPath (path);
		}
	}
}
