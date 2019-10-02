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
				bool hasHostedAgentName = !string.IsNullOrEmpty (agentNameValue) && agentNameValue.ToUpperInvariant ().Contains ("AZURE PIPELINES");
				string serverTypeValue = Environment.GetEnvironmentVariable ("SYSTEM_SERVERTYPE");
				bool isHostedServerType = !string.IsNullOrEmpty (serverTypeValue) && serverTypeValue.ToUpperInvariant ().Contains ("HOSTED");
				return hasHostedAgentName || isHostedServerType;
			}
		}

		public static bool IsFIPSPolicyEnabled {
			get {
				if (!IsWindows) {
					return false;
				}

				int fipsEnabled = (int) Microsoft.Win32.Registry.GetValue (@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Lsa\FipsAlgorithmPolicy", "Enabled", 0);
				return fipsEnabled == 1;
			}
		}

		public static readonly string MacOSInstallationRoot = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";

		static VisualStudioInstance visualStudioInstance;
		public static VisualStudioInstance GetVisualStudioInstance ()
		{
			//We should cache and reuse this value, so we don't run vswhere.exe so much
			if (visualStudioInstance != null && !string.IsNullOrEmpty (visualStudioInstance.VisualStudioRootPath))
				return visualStudioInstance;

			return visualStudioInstance = MSBuildLocator.QueryLatest ();
		}

		public static string MonoAndroidFrameworkDirectory {
			get {
				if (IsWindows) {
					VisualStudioInstance vs = GetVisualStudioInstance ();
					return Path.Combine (vs.VisualStudioRootPath, "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework", "MonoAndroid");
				} else {
					return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");
				}
			}
		}

		public static string MonoAndroidToolsDirectory {
			get {
				if (IsWindows) {
					VisualStudioInstance vs = GetVisualStudioInstance ();
					return Path.Combine (vs.VisualStudioRootPath, "MSBuild", "Xamarin", "Android");
				} else {
					return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
				}
			}
		}
	}
}

