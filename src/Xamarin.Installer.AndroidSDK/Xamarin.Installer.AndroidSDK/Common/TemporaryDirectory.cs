using System;
using System.IO;
using Xamarin.Installer.AndroidSDK.Manager;

#if !WINDOWS
using Mono.Unix.Native;
#endif

namespace Xamarin.Installer.AndroidSDK.Common
{
	static class TemporaryDirectory
	{
		public static string Create ()
		{
#if !WINDOWS
			if (!Platform.IsWindows) {
				for (int attempt = 0; attempt < 10; attempt++) {
					string path = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
					if (Syscall.mkdir (path, FilePermissions.S_IRWXU) == 0)
						return path;
					var error = Stdlib.GetLastError ();
					if (error == Errno.EEXIST)
						continue;
					throw new IOException ($"Failed to create temporary directory '{path}': {Stdlib.strerror (error)}");
				}
				throw new IOException ("Failed to find a unique temporary directory.");
			}
#endif
			string windowsPath = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (windowsPath);
			return windowsPath;
		}
	}
}
