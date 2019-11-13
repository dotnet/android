using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	static partial class Utilities
	{
		const int DevicePrefixLength = 4;
		const int UncPrefixLength = 2;
		const int UncExtendedPrefixLength = 8;

		internal const char VolumeSeparatorChar = ':';

		public static int ConsoleCursorTop    => SafeConsoleAccess (() => Console.CursorTop);
		public static int ConsoleCursorLeft   => SafeConsoleAccess (() => Console.CursorLeft);
		public static int ConsoleWindowHeight => SafeConsoleAccess (() => Console.WindowHeight);

		public static void ConsoleSetCursorPosition (int left, int top)
		{
			if (left < 0 || top < 0)
				return;

			SafeConsoleAccess (() => {
					Console.SetCursorPosition (left, top);
					return 0;
				}
			);
		}

		static int SafeConsoleAccess (Func<int> code)
		{
			// Accessing the console may throw an exception of Windows (e.g. when xaprepare runs from within msbuild)
			try {
				return code ();
			} catch (IOException) {
				// Ignore
			}

			return 0;
		}

		public static void FileMove (string sourcePath, string destinationPath)
		{
			File.Move (sourcePath, destinationPath);
		}

		public static bool FileExists (string filePath)
		{
			return File.Exists (filePath);
		}

		public static IEnumerable<string> FindExecutable (string executable)
		{
			var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

			if (pathExts != null) {
				foreach (var ext in pathExts)
					yield return Path.ChangeExtension (executable, ext);
			}
			yield return executable;
		}

		// Adapted from CoreFX sources
		internal static bool IsValidDriveChar(char value)
		{
			return ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'));
		}

		// Adapted from CoreFX sources
		static bool IsDirectorySeparator (char c)
		{
			return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
		}

		// Adapted from CoreFX sources
		static bool IsExtended (string path)
		{
			// While paths like "//?/C:/" will work, they're treated the same as "\\.\" paths.
			// Skipping of normalization will *only* occur if back slashes ('\') are used.
			return path.Length >= DevicePrefixLength
				&& path[0] == '\\'
				&& (path[1] == '\\' || path[1] == '?')
				&& path[2] == '?'
				&& path[3] == '\\';
		}

		// Adapted from CoreFX sources
		static bool IsDevice (string path)
		{
			// If the path begins with any two separators is will be recognized and normalized and prepped with
			// "\??\" for internal usage correctly. "\??\" is recognized and handled, "/??/" is not.
			return IsExtended (path) ||
				(
					path.Length >= DevicePrefixLength
					&& IsDirectorySeparator (path[0])
					&& IsDirectorySeparator (path[1])
					&& (path[2] == '.' || path[2] == '?')
					&& IsDirectorySeparator (path[3])
				);
		}

		// Adapted from CoreFX sources
		static bool IsDeviceUNC (string path)
		{
			return path.Length >= UncExtendedPrefixLength
				&& IsDevice (path)
				&& IsDirectorySeparator (path[7])
				&& path[4] == 'U'
				&& path[5] == 'N'
				&& path[6] == 'C';
		}

		// Adapted from CoreFX sources
		static int GetRootLength (string path)
		{
			int pathLength = path.Length;
			int i = 0;

			bool deviceSyntax = IsDevice (path);
			bool deviceUnc = deviceSyntax && IsDeviceUNC(path);

			if ((!deviceSyntax || deviceUnc) && pathLength > 0 && IsDirectorySeparator (path[0])) {
				// UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")
				if (deviceUnc || (pathLength > 1 && IsDirectorySeparator (path[1]))) {
					// UNC (\\?\UNC\ or \\), scan past server\share

					// Start past the prefix ("\\" or "\\?\UNC\")
					i = deviceUnc ? UncExtendedPrefixLength : UncPrefixLength;

					// Skip two separators at most
					int n = 2;
					while (i < pathLength && (!IsDirectorySeparator (path[i]) || --n > 0))
						i++;
				} else {
					// Current drive rooted (e.g. "\foo")
					i = 1;
				}
			} else if (deviceSyntax) {
				// Device path (e.g. "\\?\.", "\\.\")
				// Skip any characters following the prefix that aren't a separator
				i = DevicePrefixLength;
				while (i < pathLength && !IsDirectorySeparator(path[i]))
					i++;

				// If there is another separator take it, as long as we have had at least one
				// non-separator after the prefix (e.g. don't take "\\?\\", but take "\\?\a\")
				if (i < pathLength && i > DevicePrefixLength && IsDirectorySeparator(path[i]))
					i++;
			} else if (pathLength >= 2 && path[1] == VolumeSeparatorChar && IsValidDriveChar (path[0])) {
				// Valid drive specified path ("C:", "D:", etc.)
				i = 2;

				// If the colon is followed by a directory separator, move past it (e.g "C:\")
				if (pathLength > 2 && IsDirectorySeparator(path[2]))
					i++;
			}

			return i;
		}
	}
}
