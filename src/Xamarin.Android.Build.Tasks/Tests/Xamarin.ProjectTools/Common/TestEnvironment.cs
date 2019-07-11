using System;
using System.Runtime.InteropServices;

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

		public static string MonoAndroidToolsDirectory {
			get {
				return IsWindows ? @"$(MSBuildExtensionsPath)\Xamarin\Android" : "/Library/Frameworks/Mono.framework/External/xbuild/Xamarin/Android";
			}
		}

	}
}

