using System;
using System.IO;
using System.Runtime.InteropServices;
using Xamarin.Android.Tools.VSWhere;

namespace Xamarin.ProjectTools
{
	public static class TestEnvironment
	{
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		static bool IsDarwin ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					return os == "Darwin";
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}

		public static bool IsWindows {
			get {
				return Environment.OSVersion.Platform == PlatformID.Win32NT;
			}
		}

		public static bool IsMacOS {
			get {
				return IsDarwin ();
			}
		}

		public static bool IsLinux {
			get {
				return !IsWindows && !IsMacOS;
			}
		}

		public static bool IsRunningOnCI {
			get {
				string runningOnCiValue = Environment.GetEnvironmentVariable ("RUNNINGONCI");
				bool.TryParse (runningOnCiValue, out bool isRunningOnCi);
				return isRunningOnCi;
			}
		}

		public static bool IsRunningOnHostedAzureAgent {
			get {
				string agentNameValue = Environment.GetEnvironmentVariable ("AGENT_NAME");
				bool hasHostedAgentName = !string.IsNullOrEmpty (agentNameValue) && agentNameValue.ToUpper ().Contains ("AZURE PIPELINES");
				string serverTypeValue = Environment.GetEnvironmentVariable ("SYSTEM_SERVERTYPE");
				bool isHostedServerType = !string.IsNullOrEmpty (serverTypeValue) && serverTypeValue.ToUpper ().Contains ("HOSTED");
				return hasHostedAgentName || isHostedServerType;
			}
		}

		public static readonly string MacOSInstallationRoot = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";

		static string visualStudioDirectory;
		public static string GetVisualStudioDirectory ()
		{
			//We should cache and reuse this value, so we don't run vswhere.exe so much
			if (!string.IsNullOrEmpty (visualStudioDirectory))
				return visualStudioDirectory;

			var instance = MSBuildLocator.QueryLatest ();
			return visualStudioDirectory = instance.VisualStudioRootPath;
		}

		public static string MonoAndroidFrameworkDirectory {
			get {
				if (IsWindows) {
					string visualStudioDirectory = GetVisualStudioDirectory ();
					return Path.Combine (visualStudioDirectory, "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework", "MonoAndroid");
				} else {
					return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");
				}
			}
		}

		public static string MonoAndroidToolsDirectory {
			get {
				if (IsWindows) {
					string visualStudioDirectory = GetVisualStudioDirectory ();
					return Path.Combine (visualStudioDirectory, "MSBuild", "Xamarin", "Android");
				} else {
					return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
				}
			}
		}
	}
}

