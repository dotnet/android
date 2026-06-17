//
// Platform.cs
//
// Author:
//       Vsevolod Kukol <sevoku@microsoft.com>
//
//  Copyright (c) 2017, Microsoft, Inc
//

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	public static class Platform
	{
		public readonly static bool IsWindows;
		public readonly static bool IsMac;
		public readonly static bool IsLinux;
		public static Version OSVersion { get; private set; }

		static Platform ()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac ();
			IsLinux = !IsMac && !IsWindows;
			OSVersion = Environment.OSVersion.Version;
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
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}
	}
}
