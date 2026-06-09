using System;
using System.IO;
using System.Runtime.InteropServices;

using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK
{
	public class AndroidSDKContext
	{
		static readonly object instanceCreationLock = new object ();

		public static readonly AndroidSDKContext Instance;

		public AndroidSDKPlatform Platform { get; set; }

		public string HostArch { get; set; }

		public string BaseTemporaryDirectory { get; set; }

		public string UserName {
			get {
				if (CommonUtilities.Helpers == null)
					throw new InvalidOperationException ("Internal error: CommonUtilities.Helpers must be initialized first");
				if (String.IsNullOrEmpty (CommonUtilities.Helpers.UserName))
					throw new InvalidOperationException ("Internal error: CommonUtilities.Helpers.UserName must have a valid value");
				return CommonUtilities.Helpers.UserName;
			}
		}

		// Just a convenience to make initialization more straightforward
		public string ProductName {
			get { return CommonUtilities.ProductName; }
			set { CommonUtilities.ProductName = value; }
		}

		static AndroidSDKContext ()
		{
			lock (instanceCreationLock) {
				if (Instance != null)
					return;

				Instance = new AndroidSDKContext {
					BaseTemporaryDirectory = Path.Combine(Path.GetTempPath(), "Xamarin"),
					Platform = DeterminePlatform(),
					HostArch = DetermineHostArch()
				};
			}
		}

		static AndroidSDKPlatform DeterminePlatform ()
		{
			switch (Environment.OSVersion.Platform) {
				case PlatformID.MacOSX:
					return AndroidSDKPlatform.Mac;

				// We only care about distinction between OSX and Linux, since that's the only 
				// difference that matters in the Android SDK repository context
				case PlatformID.Unix:
					return IsRunningOnMac () ? AndroidSDKPlatform.Mac : AndroidSDKPlatform.Linux;

				default:
					return AndroidSDKPlatform.Windows; // another simplification that'll do
			}
		}

		static string DetermineHostArch()
		{
			switch (RuntimeInformation.OSArchitecture)
			{
				case Architecture.X86:
					return "x86"; 
				case Architecture.X64:
					return "x64";
				case Architecture.Arm64:
					return "aarch64";
				default:
					return "";
			}
		}

		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		//From Managed.Windows.Forms/XplatUI
		static bool IsRunningOnMac ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
				// ignore
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}
	}
}
