using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Build
{
	static class SymbolicLink
	{
		public static bool Create (string source, string target)
		{
			if (!Directory.Exists (source)) {
				if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
					return CreateWindowsSymLink (source, target);
				} else {
					return CreateUnixSymLink (source, target);
				}
			}

			return true;
		}

		static bool CreateWindowsSymLink (string source, string target)
		{
			//NOTE: attempt with and without the AllowUnprivilegedCreate flag, seems to fix Windows Server 2016
			if (!CreateSymbolicLink (source, target, SymbolLinkFlag.Directory | SymbolLinkFlag.AllowUnprivilegedCreate) &&
					!CreateSymbolicLink (source, target, SymbolLinkFlag.Directory)) {
				if (!Directory.Exists (source)) {
					var error = new Win32Exception ().Message;
					Console.Error.WriteLine ($"Unable to create symbolic link from `{source}` to `{target}`: {error}");
					return false;
				}
			}
			return true;
		}

		static bool CreateUnixSymLink (string source, string target)
		{
			int r = symlink (Path.GetFullPath (target), source);
			if (r != 0 && !Directory.Exists (source)) {
				perror ($"`ln -s '{source}' '{target}'` failed");
				return false;
			}
			return true;
		}

		enum SymbolLinkFlag {
			File = 0,
			Directory = 1,
			AllowUnprivilegedCreate = 2,
		}

		[DllImport ("kernel32.dll")]
		[return: MarshalAs (UnmanagedType.I1)]
		static extern bool CreateSymbolicLink (string lpSymlinkFileName, string lpTargetFileName, SymbolLinkFlag dwFlags);

		[DllImport ("libc", SetLastError=true)]
		static extern int symlink (string oldpath, string newpath);

		[DllImport ("libc")]
		static extern void perror (string s);
	}
}
