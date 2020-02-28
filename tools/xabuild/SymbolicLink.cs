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
				Directory.CreateDirectory (Path.GetDirectoryName (source));
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

		public static string GetRealPath (string path)
		{
			if (string.IsNullOrEmpty (path))
				return null;

			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				return GetWindowsRealPath (path);
			} else {
				return GetUnixRealPath (path);
			}
		}

		static string GetWindowsRealPath (string path)
		{
			const FileAttributes    FILE_FLAG_BACKUP_SEMANTICS  = (FileAttributes) 0x02000000;
			const FileAccess        GENERIC_READ                = unchecked((FileAccess) 0x80000000);
			IntPtr handle = CreateFileW (lpFileName: path,
					dwDesiredAccess:        GENERIC_READ,
					dwShareMode:            FileShare.Read,
					lpSecurityAttributes:   IntPtr.Zero,
					dwCreationDisposition:  FileMode.Open,
					dwFlagsAndAttributes:   FILE_FLAG_BACKUP_SEMANTICS,
					hTemplateFile:          IntPtr.Zero);
			if (handle == INVALID_FILE_HANDLE)
				return null;
			IntPtr finalPathBuf = IntPtr.Zero;
			try  {
				const FinalPathFlags flags = FinalPathFlags.FILE_NAME_OPENED;
				uint len = GetFinalPathNameByHandleW (handle, IntPtr.Zero, 0, flags);
				if (len == 0)
					return null;
				len = checked(len + 1);
				finalPathBuf    = Marshal.AllocHGlobal (checked ((int) (sizeof (char)*(len))));
				uint checkLen   = GetFinalPathNameByHandleW (handle, finalPathBuf, len, flags);
				if (checkLen == 0 || checkLen > len) {
					Console.Error.WriteLine ($"GetFinalPathNameByHandleW: expected {len}, got {checkLen}. Last Error: {Marshal.GetLastWin32Error()}");
					return null;
				}
				const string LocalUncPathPrefix = @"\\?\";
				string finalPath  = Marshal.PtrToStringUni (finalPathBuf);
				if (finalPath?.StartsWith (LocalUncPathPrefix, StringComparison.Ordinal) ?? false)
					finalPath = finalPath.Substring (LocalUncPathPrefix.Length);
				return finalPath;
			}
			finally {
				Marshal.FreeHGlobal (finalPathBuf);
				CloseHandle (handle);
			}
		}

		static string GetUnixRealPath (string path)
		{
			IntPtr buf = realpath (path, IntPtr.Zero);
			try {
				if (buf == IntPtr.Zero)
					return null;
				return Marshal.PtrToStringAnsi (buf);
			}
			finally {
				free (buf);
			}
		}

		public static bool IsPathSymlink (string path)
		{
			// Sometimes case for disk drives was different on Windows.
			return !string.Equals(Path.GetFullPath (path), GetRealPath (path), StringComparison.OrdinalIgnoreCase);
		}

		[DllImport ("kernel32.dll")]
		[return: MarshalAs (UnmanagedType.I1)]
		static extern bool CreateSymbolicLink (string lpSymlinkFileName, string lpTargetFileName, SymbolLinkFlag dwFlags);

		[DllImport ("libc", SetLastError=true)]
		static extern int symlink (string oldpath, string newpath);

		[DllImport ("libc")]
		static extern void perror (string s);

		[DllImport ("libc")]
		static extern IntPtr realpath (string file_name, IntPtr resolved_name);

		[DllImport ("libc")]
		static extern void free (IntPtr p);

		static readonly IntPtr INVALID_FILE_HANDLE = new IntPtr (-1);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateFileW(
			[MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
			[MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
			[MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
			IntPtr lpSecurityAttributes,
			[MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
			[MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		static extern uint GetFinalPathNameByHandleW(
			IntPtr hFile,
			IntPtr lpszFilePath,
			uint cchFilePath,
			FinalPathFlags dwFlags);
	}

	[Flags]
	enum FinalPathFlags : uint {
		VOLUME_NAME_DOS      = 0x0,
		FILE_NAME_NORMALIZED = 0x0,
		VOLUME_NAME_GUID     = 0x1,
		VOLUME_NAME_NT       = 0x2,
		VOLUME_NAME_NONE     = 0x4,
		FILE_NAME_OPENED     = 0x8,
	}
}
