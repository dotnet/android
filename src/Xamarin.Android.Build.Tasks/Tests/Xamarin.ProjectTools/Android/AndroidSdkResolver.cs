using System;
using System.Diagnostics;
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
			if (TestEnvironment.IsWindows) {
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
				javaSdkPath = JavaSdkPath ??= Environment.GetEnvironmentVariable ("JI_JAVA_HOME");
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

		static int? maxInstalled;
		static object maxInstalledLock = new object ();

		/// <summary>
		/// Searches the Android SDK 'platforms' folder for the latest version that we support and consider stable.
		/// </summary>
		/// <returns>The latest Android platform version that we support and consider stable.</returns>
		public static int GetMaxInstalledPlatform ()
		{
			lock (maxInstalledLock) {
				return GetMaxInstalledPlatformInternal ();
			}
		}

		static int GetMaxInstalledPlatformInternal ()
		{
			if (maxInstalled != null)
				return maxInstalled.Value;

			string sdkPath = GetAndroidSdkPath ();
			foreach (var dir in Directory.EnumerateDirectories (Path.Combine (sdkPath, "platforms"))) {
				int version;
				string v = Path.GetFileName (dir).Replace ("android-", "");
				Console.WriteLine ($"GetMaxInstalledPlatform: Parsing {v}");
				if (!int.TryParse (v, out version))
					continue;
				if (version < maxInstalled || version > XABuildConfig.AndroidLatestStableApiLevel)
					continue;
				Console.WriteLine ($"GetMaxInstalledPlatform: Setting maxInstalled to {version}");
				maxInstalled = version;
			}
			return maxInstalled ?? 0;
		}
	}
}
