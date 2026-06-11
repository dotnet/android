// 
// OS.cs
//  
// Author:
//       Andreia Gaita <andreia@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace Xamarin.AndroidTools
{
	public static class OS
	{
		public readonly static bool IsWindows;
		public readonly static bool IsMac;

		internal readonly static string ProgramFilesX86;
		
		static OS ()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac ();

			if (IsWindows) {
				ProgramFilesX86 = GetProgramFilesX86 ();
			}
		}
		
		//From Managed.Windows.Forms/XplatUI
		static bool IsRunningOnMac ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string os = System.Runtime.InteropServices.Marshal.PtrToStringAnsi (buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					System.Runtime.InteropServices.Marshal.FreeHGlobal (buf);
			}
			return false;
		}
		
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		static string GetProgramFilesX86 ()
		{
			//SpecialFolder.ProgramFilesX86 is broken on 32-bit WinXP
			if (IntPtr.Size == 8) {
				return Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
			} else {
				return Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles);
			}
		}

		internal static string GetXamarinAndroidCacheDir ()
		{
			if (IsMac) {
				var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				return Path.Combine (home, "Library", "Caches", "Xamarin.Android");
			} else if (IsWindows) {
				var localAppData = Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData);
				return Path.Combine (localAppData, "Xamarin.Android", "Cache");
			} else {
				var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				var xdgCacheHome = Environment.GetEnvironmentVariable ("XDG_CACHE_HOME");
				if (string.IsNullOrEmpty (xdgCacheHome)) {
					xdgCacheHome = Path.Combine (home, ".cache");
				}
				return Path.Combine (xdgCacheHome, "Xamarin.Android");
			}
		}
	}
	
	public static class KernelEx {
		[DllImport ("kernel32.dll", CharSet = CharSet.Auto)]
		static extern int GetLongPathName (
			[MarshalAs (UnmanagedType.LPTStr)] string path,
			[MarshalAs (UnmanagedType.LPTStr)] StringBuilder longPath,
			int longPathLength
		);

		public static string GetLongPathName (string path)
		{
			StringBuilder sb = new StringBuilder (255);
			GetLongPathName (path, sb, sb.Capacity);
			return sb.ToString ();
		}

		[DllImport ("kernel32.dll", CharSet = CharSet.Auto)]
		static extern int GetShortPathName (
			[MarshalAs (UnmanagedType.LPTStr)] string path,
			[MarshalAs (UnmanagedType.LPTStr)] StringBuilder shortPath,
			int shortPathLength
		);

		public static string GetShortPathName (string path)
		{
			StringBuilder sb = new StringBuilder (255);
			GetShortPathName (path, sb, sb.Capacity);
			return sb.ToString ();
		}
	}
}
