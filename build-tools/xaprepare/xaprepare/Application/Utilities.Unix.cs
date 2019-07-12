using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mono.Unix.Native;

namespace Xamarin.Android.Prepare
{
	static partial class Utilities
	{
		public static int ConsoleCursorTop    => Console.CursorTop;
		public static int ConsoleCursorLeft   => Console.CursorLeft;
		public static int ConsoleWindowHeight => Console.WindowHeight;

		public static void ConsoleSetCursorPosition (int left, int top)
		{
			// Console will throw an exception when the cursor coordinates are out of range, but we don't
			// want to check whether left/top are less than the buffer width/height, respectively, because
			// that might throw as well, so we just catch the possible exception below.
			//
			// On Unix it may happen when the windows were just being re-created (e.g. when switching
			// between monitors) and there's no reason to stop the app because of this so just catch the
			// exception, log it and move on.
			if (left < 0 || top < 0)
				return;

			try {
				Console.SetCursorPosition (left, top);
			} catch (Exception ex) {
				Log.Instance.Debug ("Exception thrown while setting console cursor position (ignored)");
				Log.Instance.Debug (ex.ToString ());
			}
		}

		/// <summary>
		///   Checks if the file exists as well as whether it's a symbolic link. If it is one, the method checks whether
		///   the file pointed to exists and returns <c>false</c> if it's not there.
		/// </summary>
		public static bool FileExists (string path)
		{
			if (!File.Exists (path))
				return false;

			if (FileIsDanglingSymlink (path)) {
				Log.Instance.WarningLine ($"File {path} is a dangling symlink. Treating as NOT existing.");
				return false;
			}

			return true;
		}

		public static IEnumerable<string> FindExecutable (string executable)
		{
			yield return executable;
		}

		/// <summary>
		///   Moves file from <paramref name="sourcePath"/> to <paramref name="destinationPath"/> the same way <see
		///   cref="System.IO.File.Move"/> does but it checks whether the file pointed to by <paramref
		///   name="sourcePath"/> is a dangling symlink and, if yes, it does not call <see cref="System.IO.File.Move"/>
		///   but instead uses <c>readlink(2)</c> and <c>symlink(2)</c> to recreate the symlink at the destination. This
		///   is to work around a bug in Mono 6 series which will throw an exception when trying to move a dangling
		///   symlink.
		/// </summary>
		public static void FileMove (string sourcePath, string destinationPath)
		{
			if (String.IsNullOrEmpty (sourcePath))
				throw new ArgumentException ("must not be null or empty", nameof (sourcePath));
			if (String.IsNullOrEmpty (destinationPath))
				throw new ArgumentException ("must not be null or empty", nameof (destinationPath));

			int ret = Syscall.lstat (sourcePath, out Stat sbuf);
			if (ret != 0 || (ret == 0 && (sbuf.st_mode & FilePermissions.S_IFLNK) != FilePermissions.S_IFLNK)) {
				// Not a symlink or an error, just call to the BCL and let it handle both situations
				File.Move (sourcePath, destinationPath);
				return;
			}

			// Source is a symlink
			ret = Syscall.stat (sourcePath, out sbuf);
			if (ret < 0)
				Log.DebugLine ($"stat on {sourcePath} returned {ret}. Errno: {Stdlib.GetLastError ()}");
			if (!FileIsDanglingSymlink (sourcePath)) {
				// let BCL handle it
				File.Move (sourcePath, destinationPath);
				return;
			}

			Log.DebugLine ($"Moving a dangling symlink from {sourcePath} to {destinationPath}");
			// We have a dangling symlink, we'll just recreate it at the destination and remove the source
			var sb = new StringBuilder (checked((int)sbuf.st_size));
			ret = Syscall.readlink (sourcePath, sb);
			if (ret < 0)
				throw new IOException ($"Failed to read a symbolic link '{sourcePath}'. {Stdlib.strerror (Stdlib.GetLastError ())}");

			string sourceLinkContents = sb.ToString ();
			Log.DebugLine ($"Source symlink {sourcePath} points to: {sourceLinkContents}");
			ret = Syscall.symlink (sourceLinkContents, destinationPath);
			if (ret < 0)
				throw new IOException ($"Failed to create a symbolic link '{destinationPath}' -> '{sourceLinkContents}'. {Stdlib.strerror (Stdlib.GetLastError ())}");
		}

		static bool FileIsDanglingSymlink (string path)
		{
			int ret = Syscall.stat (path, out Stat sbuf);
			if (ret < 0)
				Log.DebugLine ($"stat on {path} returned {ret}. Errno: {Stdlib.GetLastError ()}");
			if (ret == 0 || (ret < 0 && Stdlib.GetLastError () != Errno.ENOENT)) {
				// Either a valid symlink or an error other than ENOENT
				return false;
			}

			return true;
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
