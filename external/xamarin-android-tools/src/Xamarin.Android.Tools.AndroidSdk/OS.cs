using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools
{
	public class OS
	{
		public readonly static bool IsWindows;
		public readonly static bool IsMac;
		public readonly static bool IsLinux;

		internal readonly static string? ProgramFilesX86;

		internal readonly static string NativeLibraryFormat = "{0}";

		static OS ()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac ();
			IsLinux = !IsWindows && !IsMac;

			if (IsWindows) {
				ProgramFilesX86 = GetProgramFilesX86 ();
			}

			if (IsWindows)
				NativeLibraryFormat = "{0}.dll";
			if (IsMac)
				NativeLibraryFormat = "lib{0}.dylib";
			if (IsLinux)
				NativeLibraryFormat = "lib{0}.so";
		}

		//From Managed.Windows.Forms/XplatUI
		static bool IsRunningOnMac ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string? os = System.Runtime.InteropServices.Marshal.PtrToStringAnsi (buf);
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
				var xdgCacheHome = Environment.GetEnvironmentVariable ("XDG_CACHE_HOME");
				if (string.IsNullOrEmpty (xdgCacheHome)) {
					var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
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

	internal static class RegistryEx
	{
		const string ADVAPI = "advapi32.dll";

		public static UIntPtr CurrentUser = (UIntPtr)0x80000001;
		public static UIntPtr LocalMachine = (UIntPtr)0x80000002;

		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegOpenKeyEx (UIntPtr hKey, string subKey, uint reserved, uint sam, out UIntPtr phkResult);

		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegQueryValueExW (UIntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
			StringBuilder lpData, ref uint lpcbData);

		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegSetValueExW (UIntPtr hKey, string lpValueName, int lpReserved,
			uint dwType, string data, uint cbData);

		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegSetValueExW (UIntPtr hKey, string lpValueName, int lpReserved,
			uint dwType, IntPtr data, uint cbData);

		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegCreateKeyEx (UIntPtr hKey, string subKey, uint reserved, string? @class, uint options,
			uint samDesired, IntPtr lpSecurityAttributes, out UIntPtr phkResult, out Disposition lpdwDisposition);

		// https://docs.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regenumkeyexw
		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegEnumKeyExW (
				UIntPtr         hKey,
				uint            dwIndex,
				[Out] char[]    lpName,
				ref uint        lpcchName,
				IntPtr          lpReserved,
				IntPtr          lpClass,
				IntPtr          lpcchClass,
				IntPtr          lpftLastWriteTime
		);

		// https://docs.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regqueryinfokeyw
		[DllImport (ADVAPI, CharSet = CharSet.Unicode, SetLastError = true)]
		static extern int RegQueryInfoKey (
				UIntPtr     hKey,
				IntPtr      lpClass,
				IntPtr      lpcchClass,
				IntPtr      lpReserved,
				out uint    lpcSubkey,
				out uint    lpcchMaxSubkeyLen,
				IntPtr      lpcchMaxClassLen,
				IntPtr      lpcValues,
				IntPtr      lpcchMaxValueNameLen,
				IntPtr      lpcbMaxValueLen,
				IntPtr      lpSecurityDescriptor,
				IntPtr      lpftLastWriteTime
		);

		[DllImport ("advapi32.dll", SetLastError = true)]
		static extern int RegCloseKey (UIntPtr hKey);

		internal static bool CheckRegistryKeyForExecutable (UIntPtr key, string subkey, string valueName, RegistryEx.Wow64 wow64, string subdir, string exe)
		{
			try {
				string key_name = string.Format (@"{0}\{1}\{2}", key == RegistryEx.CurrentUser ? "HKCU" : "HKLM", subkey, valueName);

				var path = AndroidSdkBase.NullIfEmpty (RegistryEx.GetValueString (key, subkey, valueName, wow64));

				if (path == null) {
					return false;
				}

				if (!ProcessUtils.FindExecutablesInDirectory (Path.Combine (path, subdir), exe).Any ()) {
					return false;
				}

				return true;
			} catch (Exception) {
				return false;
			}
		}

		public static IEnumerable<string> EnumerateSubkeys (UIntPtr key, string subkey, Wow64 wow64)
		{
			UIntPtr regKeyHandle;
			uint sam = (uint)Rights.Read + (uint)wow64;
			int r = RegOpenKeyEx (key, subkey, 0, sam, out regKeyHandle);
			if (r != 0) {
				yield break;
			}
			try {
				r = RegQueryInfoKey (
						hKey:                   regKeyHandle,
						lpClass:                IntPtr.Zero,
						lpcchClass:             IntPtr.Zero,
						lpReserved:             IntPtr.Zero,
						lpcSubkey:              out uint cSubkeys,
						lpcchMaxSubkeyLen:      out uint cchMaxSubkeyLen,
						lpcchMaxClassLen:       IntPtr.Zero,
						lpcValues:              IntPtr.Zero,
						lpcchMaxValueNameLen:   IntPtr.Zero,
						lpcbMaxValueLen:        IntPtr.Zero,
						lpSecurityDescriptor:   IntPtr.Zero,
						lpftLastWriteTime:      IntPtr.Zero
				);
				if (r != 0) {
					yield break;
				}
				var name    = new char [cchMaxSubkeyLen+1];
				for (uint i = 0; i < cSubkeys; ++i) {
					var nameLen = (uint) name.Length;
					r = RegEnumKeyExW (
							hKey:               regKeyHandle,
							dwIndex:            i,
							lpName:             name,
							lpcchName:          ref nameLen,
							lpReserved:         IntPtr.Zero,
							lpClass:            IntPtr.Zero,
							lpcchClass:         IntPtr.Zero,
							lpftLastWriteTime:  IntPtr.Zero
					);
					if (r != 0) {
						continue;
					}
					yield return new string (name, 0, (int) nameLen);
				}
			}
			finally {
				RegCloseKey (regKeyHandle);
			}
		}

		public static string? GetValueString (UIntPtr key, string subkey, string valueName, Wow64 wow64)
		{
			UIntPtr regKeyHandle;
			uint sam = (uint)Rights.QueryValue + (uint)wow64;
			if (RegOpenKeyEx (key, subkey, 0, sam, out regKeyHandle) != 0)
				return null;

			try {
				uint type;
				var sb = new StringBuilder (2048);
				uint cbData = (uint) sb.Capacity;
				if (RegQueryValueExW (regKeyHandle, valueName, 0, out type, sb, ref cbData) == 0) {
					return sb.ToString ();
				}
				return null;
			} finally {
				RegCloseKey (regKeyHandle);
			}
		}

		public static void SetValueString (UIntPtr key, string subkey, string valueName, string value, Wow64 wow64)
		{
			UIntPtr regKeyHandle;
			uint sam = (uint)(Rights.CreateSubKey | Rights.SetValue) + (uint)wow64;
			uint options = (uint) Options.NonVolatile;
			Disposition disposition;
			if (RegCreateKeyEx (key, subkey, 0, "", options, sam, IntPtr.Zero, out regKeyHandle, out disposition) != 0) {
				throw new Exception ("Could not open or create key");
			}

			try {
				uint type = (uint)ValueType.String;
				uint lenBytesPlusNull = ((uint)value.Length + 1) * 2;
				var result = RegSetValueExW (regKeyHandle, valueName, 0, type, value, lenBytesPlusNull);
				if (result != 0)
					throw new Exception (string.Format ("Error {0} setting registry key '{1}{2}@{3}'='{4}'",
						result, key, subkey, valueName, value));
			} finally {
				RegCloseKey (regKeyHandle);
			}
		}

		[Flags]
		enum Rights : uint
		{
			None = 0,
			QueryValue = 0x0001,
			SetValue = 0x0002,
			CreateSubKey = 0x0004,
			EnumerateSubKey = 0x0008,
			Notify          = 0x0010,
			Read            = _StandardRead | QueryValue | EnumerateSubKey | Notify,
			_StandardRead   = 0x20000,
		}

		enum Options
		{
			BackupRestore = 0x00000004,
			CreateLink = 0x00000002,
			NonVolatile = 0x00000000,
			Volatile = 0x00000001,
		}

		public enum Wow64 : uint
		{
			Key64 = 0x0100,
			Key32 = 0x0200,
		}

		enum ValueType : uint
		{
			None = 0, //REG_NONE
			String = 1, //REG_SZ
			UnexpandedString = 2, //REG_EXPAND_SZ
			Binary = 3, //REG_BINARY
			DWord = 4, //REG_DWORD
			DWordLittleEndian = 4, //REG_DWORD_LITTLE_ENDIAN
			DWordBigEndian = 5, //REG_DWORD_BIG_ENDIAN
			Link = 6, //REG_LINK
			MultiString = 7, //REG_MULTI_SZ
			ResourceList = 8, //REG_RESOURCE_LIST
			FullResourceDescriptor = 9, //REG_FULL_RESOURCE_DESCRIPTOR
			ResourceRequirementsList = 10, //REG_RESOURCE_REQUIREMENTS_LIST
			QWord = 11, //REG_QWORD
			QWordLittleEndian = 11, //REG_QWORD_LITTLE_ENDIAN
		}

		enum Disposition : uint
		{
			CreatedNewKey  = 0x00000001,
			OpenedExistingKey = 0x00000002,
		}
	}
}

