using System;
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
				rename (sourceFile, destFile);
			}
		}

		[DllImport ("libc", SetLastError=true)]
		static extern int rename (string old, string @new);
	}
}

