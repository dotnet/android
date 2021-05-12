using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xamarin.ProjectTools
{
	public static class AndroidSdkResolver
	{
		static string HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		static string DefaultToolchainPath = Path.Combine (HomeDirectory, "android-toolchain");
		static string AzureToolchainPathMacOS = Path.Combine (HomeDirectory, "Library", "Android");
		static string ToolchainPath = (TestEnvironment.IsMacOS && TestEnvironment.IsRunningOnCI) ? AzureToolchainPathMacOS : DefaultToolchainPath;

		static string GetPathFromRegistry (string valueName)
		{
			if (TestEnvironment.IsWindows) {
				return (string) Microsoft.Win32.Registry.GetValue ("HKEY_CURRENT_USER\\SOFTWARE\\Novell\\Mono for Android", valueName, null);
			}
			return null;
		}

		public static string GetAndroidSdkPath ()
		{
			var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = GetPathFromRegistry ("AndroidSdkDirectory");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = Path.GetFullPath (Path.Combine (ToolchainPath, "sdk"));

			return sdkPath;

		}

		public static string GetAndroidNdkPath ()
		{
			var ndkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = GetPathFromRegistry ("AndroidNdkDirectory");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = Path.GetFullPath (Path.Combine (ToolchainPath, "ndk"));

			return ndkPath;
		}

		// Cache the result, so we don't run MSBuild on every call
		static string JavaSdkPath;

		public static string GetJavaSdkPath ()
		{
			if (string.IsNullOrEmpty (JavaSdkPath))
				JavaSdkPath = RunPathsTargets ("GetJavaSdkDirectory");
			if (string.IsNullOrEmpty (JavaSdkPath))
				JavaSdkPath = Environment.GetEnvironmentVariable ("JI_JAVA_HOME");
			if (string.IsNullOrEmpty (JavaSdkPath))
				JavaSdkPath = Environment.GetEnvironmentVariable ("JAVA_HOME");
			if (string.IsNullOrEmpty (JavaSdkPath))
				JavaSdkPath = GetPathFromRegistry ("JavaSdkDirectory");
			if (string.IsNullOrEmpty (JavaSdkPath))
				JavaSdkPath = Path.GetFullPath (Path.Combine (ToolchainPath, "jdk"));
			return JavaSdkPath;
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

		// Cache the result, so we don't run MSBuild on every call
		static string DotNetPreviewPath;
		public static string GetDotNetPreviewPath ()
		{
			if (string.IsNullOrEmpty (DotNetPreviewPath))
				DotNetPreviewPath = RunPathsTargets ("GetDotNetPreviewPath");
			return DotNetPreviewPath;
		}

		static string RunPathsTargets (string target)
		{
			var targets = Path.Combine (XABuildPaths.TopDirectory, "build-tools", "scripts", "Paths.targets");
			var msbuild = TestEnvironment.IsWindows ? TestEnvironment.GetVisualStudioInstance ().MSBuildPath : "msbuild";
			var args = $"/nologo /v:minimal /t:{target} \"{targets}\"";
			var psi = new ProcessStartInfo (msbuild, args) {
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
			};
			using (var p = Process.Start (psi)) {
				p.WaitForExit ();
				string path = p.StandardOutput.ReadToEnd ().Trim ();
				return Directory.Exists (path) ? path : null;
			}
		}

		static int? maxInstalled;

		public static int GetMaxInstalledPlatform ()
		{
			if (maxInstalled != null)
				return maxInstalled.Value;

			string sdkPath = GetAndroidSdkPath ();
			foreach (var dir in Directory.EnumerateDirectories (Path.Combine (sdkPath, "platforms"))) {
				int version;
				string v = Path.GetFileName (dir).Replace ("android-", "");
				if (!int.TryParse (v, out version))
					continue;
				if (version < maxInstalled)
					continue;
				maxInstalled = version;
			}
			return maxInstalled ?? 0;
		}
	}
}
