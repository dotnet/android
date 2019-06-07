using System;
using System.Collections.Generic;
using System.IO;

using Mono.Unix.Native;

namespace Xamarin.Android.Prepare
{
	static partial class Utilities
	{
		public static bool FileExists (string path)
		{
			if (!File.Exists (path))
				return false;

			Stat sbuf;
			int ret = Syscall.stat (path, out sbuf);
			if (ret < 0) {
				if (Stdlib.GetLastError () == Errno.ENOENT)
					Log.Instance.WarningLine ($"File {path} is a dangling symlink. Treating as NOT existing.");

				return false;
			}

			return true;
		}

		public static IEnumerable<string> FindExecutable (string executable)
		{
			yield return executable;
		}

		// Adapted from CoreFX sources
		static bool IsDirectorySeparator (char c)
		{
			return c == Path.DirectorySeparatorChar;
		}

		// Adapted from CoreFX sources
		static int GetRootLength(string path)
		{
			return path.Length > 0 && IsDirectorySeparator (path[0]) ? 1 : 0;
		}
	}
}
