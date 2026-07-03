using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class AndroidSdkResolver
	{
		static string HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		static string DefaultToolchainPath = Path.Combine (HomeDirectory, "android-toolchain");

		static string GetPathFromRegistry (string valueName)
		{
			if (OperatingSystem.IsWindows ()) {
				return (string) Microsoft.Win32.Registry.GetValue ("HKEY_CURRENT_USER\\SOFTWARE\\Novell\\Mono for Android", valueName, null);
			}
			return null;
		}

		public static string GetAndroidSdkPath ()
		{
			var sdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_ROOT");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = Path.GetFullPath (Path.Combine (DefaultToolchainPath, "sdk"));

			return sdkPath;

		}

		public static string GetAndroidNdkPath ()
		{
			var ndkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_NDK_PATH");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_LATEST_HOME");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = Path.GetFullPath (Path.Combine (DefaultToolchainPath, "ndk"));

			return ndkPath;
		}

		// Cache the result, so we don't run MSBuild on every call
		static string JavaSdkPath;

		public static string GetJavaSdkPath ()
		{
			var javaSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
			if (string.IsNullOrEmpty (javaSdkPath))
				javaSdkPath = JavaSdkPath ??= Environment.GetEnvironmentVariable ("JAVA_HOME");
			if (string.IsNullOrEmpty (javaSdkPath))
				javaSdkPath = JavaSdkPath ??= RunPathsTargets ("GetJavaSdkDirectory");
			if (string.IsNullOrEmpty (javaSdkPath))
				javaSdkPath = JavaSdkPath ??= GetPathFromRegistry ("JavaSdkDirectory");
			if (string.IsNullOrEmpty (javaSdkPath))
				javaSdkPath = JavaSdkPath ??= Path.GetFullPath (Path.Combine (DefaultToolchainPath, "jdk"));
			return javaSdkPath;
		}

		static string JavaSdkVersionString;

		public static string GetJavaSdkVersionString ()
		{
			if (string.IsNullOrEmpty (JavaSdkVersionString)) {
				var javaPath = Path.Combine (GetJavaSdkPath (), "bin", "java");
				var psi = new ProcessStartInfo (javaPath, "-version") {
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					UseShellExecute = false,
				};
				var output = new StringBuilder ();
				using (var p = Process.Start (psi)) {
					p.WaitForExit ();
					output.AppendLine (p.StandardOutput.ReadToEnd ());
					output.AppendLine (p.StandardError.ReadToEnd ());
					JavaSdkVersionString = output.ToString ();
				}
			}
			return JavaSdkVersionString;
		}

		static string RunPathsTargets (string target)
		{
			var targets = Path.Combine (XABuildPaths.TopDirectory, "build-tools", "scripts", "Paths.targets");
			var dotnet = Path.Combine (TestEnvironment.DotNetPreviewDirectory, "dotnet");
			var args = $"build /nologo /v:minimal /t:{target} \"{targets}\"";
			var psi = new ProcessStartInfo (dotnet, args) {
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				WorkingDirectory = XABuildPaths.TestAssemblyOutputDirectory,
			};
			using (var p = Process.Start (psi)) {
				p.WaitForExit ();
				string path = p.StandardOutput.ReadLine ().Trim ();
				return Directory.Exists (path) ? path : null;
			}
		}

		static Version? maxInstalled;
		static object maxInstalledLock = new object ();

		/// <summary>
		/// Searches the Android SDK 'platforms' folder for the latest version that we support and consider stable.
		/// </summary>
		/// <returns>The latest Android platform version that we support and consider stable.</returns>
		public static Version GetMaxInstalledPlatform ()
		{
			lock (maxInstalledLock) {
				return GetMaxInstalledPlatformInternal ();
			}
		}

		public static bool TryParseAndroidSdkVersion (string value, [NotNullWhen (true)] out Version? version)
		{
			if (Version.TryParse (value, out version)) {
				return true;
			}
			if (int.TryParse (value, out var major)) {
				version = new Version (major, 0);
				return true;
			}
			version = null;
			return false;
		}

		static Version GetMaxInstalledPlatformInternal ()
		{
			if (maxInstalled != null)
				return maxInstalled;

			string sdkPath = GetAndroidSdkPath ();
			foreach (var dir in Directory.EnumerateDirectories (Path.Combine (sdkPath, "platforms"))) {
				string v = Path.GetFileName (dir).Replace ("android-", "");
				Console.WriteLine ($"GetMaxInstalledPlatform: Parsing {v}");
				if (!TryParseAndroidSdkVersion (v, out var version)) {
					continue;
				}
				if (version < maxInstalled || version > XABuildConfig.AndroidLatestStableApiLevel)
					continue;
				Console.WriteLine ($"GetMaxInstalledPlatform: Setting maxInstalled to {version}");
				maxInstalled = version;
			}
			return maxInstalled ?? new Version (0, 0);
		}

		/// <summary>
		/// Returns the platform directory name (e.g. "android-37.0") for the given platform version.
		/// Starting with API 37, Google ships platforms as "android-37.0" instead of "android-37",
		/// so we check for both and return whichever exists. Falls back to "{Major}" if neither exists.
		/// </summary>
		public static string GetPlatformDirectoryName (Version platform)
		{
			string sdkPath = GetAndroidSdkPath ();
			string platformsPath = Path.Combine (sdkPath, "platforms");

			// Try "{Major}.{Minor}" first (e.g. "android-37.0")
			string fullDir = Path.Combine (platformsPath, $"android-{platform}");
			if (Directory.Exists (fullDir))
				return $"android-{platform}";

			// Try "{Major}" (e.g. "android-37")
			string majorDir = Path.Combine (platformsPath, $"android-{platform.Major}");
			if (Directory.Exists (majorDir))
				return $"android-{platform.Major}";

			// Default to full version string
			return $"android-{platform}";
		}

		/// <summary>
		/// Returns the full path to android.jar for the given platform version.
		/// </summary>
		public static string GetAndroidJarPath (Version platform)
		{
			return Path.Combine (GetAndroidSdkPath (), "platforms", GetPlatformDirectoryName (platform), "android.jar");
		}
	}
}
