#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Tools
{
	class FileUtil
	{
		public static string GetTempFilenameForWrite (string fileName)
		{
			return Path.GetDirectoryName (fileName) + Path.DirectorySeparatorChar + ".#" + Path.GetFileName (fileName);
		}

		//From MonoDevelop.Core.FileService
		public static void SystemRename (string sourceFile, string destFile)
		{
			//FIXME: use the atomic System.IO.File.Replace on NTFS
			if (OS.IsWindows) {
				string? wtmp = null;
				if (File.Exists (destFile)) {
					do {
						wtmp = Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ());
					} while (File.Exists (wtmp));

					File.Move (destFile, wtmp);
				}
				try {
					File.Move (sourceFile, destFile);
				}
				catch {
					try {
						if (wtmp != null)
							File.Move (wtmp, destFile);
					}
					catch {
						wtmp = null;
					}
					throw;
				}
				finally {
					if (wtmp != null) {
						try {
							File.Delete (wtmp);
						}
						catch { }
					}
				}
			}
			else {
				if (rename (sourceFile, destFile) != 0) {
					int errno = Marshal.GetLastWin32Error ();
					var error = new Win32Exception (errno);
					throw new IOException ($"Could not rename '{sourceFile}' to '{destFile}' (errno {errno}): {error.Message}", error);
				}
			}
		}

		// SAFETY: The platform C ABI matches this import; the marshaller supplies NUL-terminated
		// path strings valid for the synchronous call, and libc retains neither pointer on success or failure.
		[DllImport ("libc", SetLastError=true)]
		static safe extern int rename (string old, string @new);
	}
}
