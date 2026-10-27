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

		public AndroidSdkInfo (Action<TraceLevel, string>? logger = null, string? androidSdkPath = null, string? androidNdkPath = null, string? javaSdkPath = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			sdk = CreateSdk (logger);
			sdk.Initialize (androidSdkPath, androidNdkPath, javaSdkPath);

			// shouldn't happen, in that sdk.Initialize() should throw instead
			if (string.IsNullOrEmpty (AndroidSdkPath))
				throw new InvalidOperationException ($"Could not determine Android SDK location. Please provide `{nameof (androidSdkPath)}`.");
			if (string.IsNullOrEmpty (JavaSdkPath))
				throw new InvalidOperationException ($"Could not determine Java SDK location. Please provide `{nameof (javaSdkPath)}`.");
		}

		/// <summary>
		/// Discovers installed SDK and JDK paths without requiring both to be present, for installers
		/// that must work before the SDK or JDK has been installed.
		/// </summary>
		public static void DiscoverInstallationPaths (out string? androidSdkPath, out string[] allAndroidSdkPaths, out string? javaSdkPath,
			string? preferredAndroidSdkPath = null, string? preferredJavaSdkPath = null, Action<TraceLevel, string>? logger = null)
		{
			DiscoverInstallationPaths (CreateSdk (logger ?? DefaultConsoleLogger), out androidSdkPath, out allAndroidSdkPaths,
				out javaSdkPath, preferredAndroidSdkPath, preferredJavaSdkPath);
		}

		internal static void DiscoverInstallationPaths (AndroidSdkBase sdk, out string? androidSdkPath, out string[] allAndroidSdkPaths,
			out string? javaSdkPath, string? preferredAndroidSdkPath = null, string? preferredJavaSdkPath = null)
		{
			sdk.Initialize (preferredAndroidSdkPath, javaSdkPath: preferredJavaSdkPath);
			androidSdkPath = sdk.AndroidSdkPath;
			allAndroidSdkPaths = sdk.AllAndroidSdks;
			javaSdkPath = sdk.JavaSdkPath;
		}

		static AndroidSdkBase CreateSdk (Action<TraceLevel, string> logger)
		{
			return OS.IsWindows
				? (AndroidSdkBase) new AndroidSdkWindows (logger)
				: (AndroidSdkBase) new AndroidSdkUnix (logger);
		}

		public IEnumerable<string> GetBuildToolsPaths (string preferredBuildToolsVersion)
			=> GetBuildToolsPaths (AndroidSdkPath, preferredBuildToolsVersion);

		public static IEnumerable<string> GetBuildToolsPaths (string androidSdkPath, string preferredBuildToolsVersion)
		{
			if (androidSdkPath == null)
				throw new ArgumentNullException (nameof (androidSdkPath));

			if (!string.IsNullOrEmpty (preferredBuildToolsVersion)) {
				var preferredDir = Path.Combine (androidSdkPath, "build-tools", preferredBuildToolsVersion);
				if (Directory.Exists (preferredDir))
					return new[] { preferredDir }.Concat (EnumerateBuildToolsPaths (androidSdkPath).Where (p => p!= preferredDir));
			}
			return EnumerateBuildToolsPaths (androidSdkPath);
		}

		public IEnumerable<string> GetBuildToolsPaths ()
			=> GetBuildToolsPaths (AndroidSdkPath, "");

		static IEnumerable<string> EnumerateBuildToolsPaths (string androidSdkPath)
		{
			var buildTools  = Path.Combine (androidSdkPath, "build-tools");
			if (Directory.Exists (buildTools)) {
				var sorted = SortedSubdirectoriesByVersion (buildTools);

				foreach (var d in sorted)
					yield return d;

				var preview = Directory.EnumerateDirectories (buildTools)
					.Where(x => TryParseVersion (Path.GetFileName (x)) == null)
					.Select(x => x);

				foreach (var d in preview)
					yield return d;
			}
			var ptPath  = Path.Combine (androidSdkPath, "platform-tools");
			if (Directory.Exists (ptPath))
				yield return ptPath;
		}

		static IEnumerable<string> SortedSubdirectoriesByVersion (string dir)
		{
			return from p in Directory.EnumerateDirectories (dir)
					let version = TryParseVersion (Path.GetFileName (p))
						where version != null
					orderby version descending
					select p;
		}

		static Version? TryParseVersion (string v)
		{
			if (Version.TryParse (v, out var version))
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

		public string? TryGetPlatformDirectoryFromApiLevel (string idOrApiLevel, AndroidVersions versions)
			=> TryGetPlatformDirectoryFromApiLevel (AndroidSdkPath, idOrApiLevel, versions);

		public static string? TryGetPlatformDirectoryFromApiLevel (string androidSdkPath, string idOrApiLevel, AndroidVersions versions)
		{
			if (androidSdkPath == null)
				throw new ArgumentNullException (nameof (androidSdkPath));
			if (versions == null)
				throw new ArgumentNullException (nameof (versions));

			var id  = versions.GetIdFromApiLevel (idOrApiLevel);
			if (id == null)
				return null;

			string? dir = Path.Combine (androidSdkPath, "platforms", "android-" + id);

			if (Directory.Exists (dir))
				return dir;

			// Only fall back to major API level if we weren't explicitly requesting a minor version.
			// For example, if "36.1" was requested but android-36.1 doesn't exist, don't fall back to android-36
			// because the minor version may have APIs that the major version doesn't have.
			// See: https://github.com/dotnet/android/issues/10720
			if (Version.TryParse (id, out var version) && version.Minor != 0) {
				return null;
			}

			var level   = versions.GetApiLevelFromId (id);
			dir         = level.HasValue ? Path.Combine (androidSdkPath, "platforms", "android-" + level.Value) : null;
			if (dir != null && Directory.Exists (dir))
				return dir;

			// Starting with API 37, Google's SDK Manager installs platforms to
			// "android-37.0" instead of "android-37". Try the "{major}.0" fallback.
			// See: https://github.com/dotnet/android-tools/issues/319
			if (int.TryParse (id, out _)) {
				dir = Path.Combine (androidSdkPath, "platforms", "android-" + id + ".0");
				if (Directory.Exists (dir))
					return dir;
			}

			return null;
		}

		public bool IsPlatformInstalled (int apiLevel)
		{
			return apiLevel != 0 &&
				(Directory.Exists (GetPlatformDirectory (apiLevel)) ||
				 Directory.Exists (GetPlatformDirectoryFromId (apiLevel + ".0")));
		}

		public string? AndroidNdkPath {
			get { return sdk.AndroidNdkPath; }
		}

		public string AndroidSdkPath {
			get {
				var path = sdk.AndroidSdkPath;
				if (path == null)
					throw new InvalidOperationException ("Could not determine Android SDK location. Please provide `androidSdkPath`.");
				return path;
			}
		}

		public string [] AllAndroidSdkPaths {
			get {
				return sdk.AllAndroidSdks ?? new string [0];
			}
		}

		public string JavaSdkPath {
			get {
				var path = sdk.JavaSdkPath;
				if (path == null)
					throw new InvalidOperationException ("Could not determine Java SDK location. Please provide `javaSdkPath`.");
				return path;
			}
		}

		public string AndroidNdkHostPlatform {
			get { return sdk.NdkHostPlatform; }
		}

		public static void SetPreferredAndroidNdkPath (string path, Action<TraceLevel, string>? logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidNdkPath(path);
		}

		internal static void DefaultConsoleLogger (TraceLevel level, string message)
		{
			switch (level) {
			case TraceLevel.Error:
				Console.Error.WriteLine (message);
				break;
			default:
				Console.WriteLine ($"[{level}] {message}");
				break;
			}
		}

		public static void SetPreferredAndroidSdkPath (string path, Action<TraceLevel, string>? logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredAndroidSdkPath (path);
		}

		public static void SetPreferredJavaSdkPath (string path, Action<TraceLevel, string>? logger = null)
		{
			logger  = logger ?? DefaultConsoleLogger;

			var sdk = CreateSdk (logger);
			sdk.SetPreferredJavaSdkPath (path);
		}

		public static void DetectAndSetPreferredJavaSdkPathToLatest (Action<TraceLevel, string>? logger = null)
		{
			if (OS.IsWindows)
				throw new NotImplementedException ("Windows is not supported at this time.");

			logger          = logger ?? DefaultConsoleLogger;

			var latestJdk   = JdkInfo.GetSupportedJdkInfos (logger).FirstOrDefault ();
			if (latestJdk == null)
				throw new NotSupportedException ("No Microsoft OpenJDK could be found.  Please re-run the Visual Studio installer or manually specify the JDK path in settings.");

			var sdk = CreateSdk (logger);
			sdk.SetPreferredJavaSdkPath (latestJdk.HomePath);
		}

		public string? TryGetCommandLineToolsPath ()
		{
			return GetCommandLineToolsPaths ("latest").FirstOrDefault ();
		}

		public IEnumerable<string> GetCommandLineToolsPaths (string preferredCommandLineToolsVersion)
			=> GetCommandLineToolsPaths (AndroidSdkPath, preferredCommandLineToolsVersion);

		public static IEnumerable<string> GetCommandLineToolsPaths (string androidSdkPath, string preferredCommandLineToolsVersion)
		{
			if (androidSdkPath == null)
				throw new ArgumentNullException (nameof (androidSdkPath));

			if (!string.IsNullOrEmpty (preferredCommandLineToolsVersion)) {
				var preferredDir = Path.Combine (androidSdkPath, "cmdline-tools", preferredCommandLineToolsVersion);
				if (Directory.Exists (preferredDir))
					return new[] { preferredDir }.Concat (EnumerateCommandLineToolsPaths (androidSdkPath).Where (p => p != preferredDir));
			}
			return EnumerateCommandLineToolsPaths (androidSdkPath);
		}

		public IEnumerable<string> GetCommandLineToolsPaths ()
			=> GetCommandLineToolsPaths (AndroidSdkPath, "");

		static IEnumerable<string> EnumerateCommandLineToolsPaths (string androidSdkPath)
		{
			var cmdlineToolsDir = Path.Combine (androidSdkPath, "cmdline-tools");
			if (Directory.Exists (cmdlineToolsDir)) {
				var latestDir = Path.Combine (cmdlineToolsDir, "latest");
				if (Directory.Exists (latestDir))
					yield return latestDir;
				foreach (var d in SortedSubdirectoriesByVersion (cmdlineToolsDir)) {
					var version = Path.GetFileName (d);
					if (version == "latest")
						continue;
					yield return d;
				}
			}
			var toolsDir = Path.Combine (androidSdkPath, "tools");
			if (Directory.Exists (toolsDir)) {
				yield return toolsDir;
			}
		}
	}
}
