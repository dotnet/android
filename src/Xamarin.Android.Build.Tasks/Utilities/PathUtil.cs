using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Tools
{
	public static class PathUtil
	{

		const int DevicePrefixLength = 4;
		const int UncPrefixLength = 2;
		const int UncExtendedPrefixLength = 8;

		internal const char VolumeSeparatorChar = ':';

		static bool IsWindows = Path.DirectorySeparatorChar == '\\';
		// Adapted from CoreFX sources
		public static string GetRelativePath(string relativeTo, string path, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
		{
			if (String.IsNullOrEmpty(relativeTo))
				throw new ArgumentException("must not be null or empty", nameof(relativeTo));

			if (String.IsNullOrEmpty(path))
				throw new ArgumentException("must not be null or empty", nameof(path));

			relativeTo = Path.GetFullPath(relativeTo);
			path = Path.GetFullPath(path);

			// Need to check if the roots are different- if they are we need to return the "to" path.
			if (!AreRootsEqual(relativeTo, path, comparisonType))
				return path;

			int commonLength = GetCommonPathLength(relativeTo, path, ignoreCase: comparisonType == StringComparison.OrdinalIgnoreCase);

			// If there is nothing in common they can't share the same root, return the "to" path as is.
			if (commonLength == 0)
				return path;

			// Trailing separators aren't significant for comparison
			int relativeToLength = relativeTo.Length;
			if (EndsInDirectorySeparator(relativeTo))
				relativeToLength--;

			bool pathEndsInSeparator = EndsInDirectorySeparator(path);
			int pathLength = path.Length;
			if (pathEndsInSeparator)
				pathLength--;

			// If we have effectively the same path, return "."
			if (relativeToLength == pathLength && commonLength >= relativeToLength)
				return ".";

			// We have the same root, we need to calculate the difference now using the
			// common Length and Segment count past the length.
			//
			// Some examples:
			//
			//  C:\Foo C:\Bar L3, S1 -> ..\Bar
			//  C:\Foo C:\Foo\Bar L6, S0 -> Bar
			//  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
			//  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar

			var sb = new StringBuilder(Math.Max(relativeTo.Length, path.Length));

			// Add parent segments for segments past the common on the "from" path
			if (commonLength < relativeToLength)
			{
				sb.Append("..");

				for (int i = commonLength + 1; i < relativeToLength; i++)
				{
					if (IsDirectorySeparator(relativeTo[i]))
					{
						sb.Append(Path.DirectorySeparatorChar);
						sb.Append("..");
					}
				}
			}
			else if (IsDirectorySeparator(path[commonLength]))
			{
				// No parent segments and we need to eat the initial separator
				//  (C:\Foo C:\Foo\Bar case)
				commonLength++;
			}

			// Now add the rest of the "to" path, adding back the trailing separator
			int differenceLength = pathLength - commonLength;
			if (pathEndsInSeparator)
				differenceLength++;

			if (differenceLength > 0)
			{
				if (sb.Length > 0)
				{
					sb.Append(Path.DirectorySeparatorChar);
				}

				sb.Append(path, commonLength, differenceLength);
			}

			return sb.ToString();
		}

		// Adapted from CoreFX sources
		static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
		{
			int firstRootLength = GetRootLength(first);
			int secondRootLength = GetRootLength(second);

			return firstRootLength == secondRootLength
				&& String.Compare(
					strA: first,
					indexA: 0,
					strB: second,
					indexB: 0,
					length: firstRootLength,
					comparisonType: comparisonType) == 0;
		}

		// Adapted from CoreFX sources
		static int GetCommonPathLength(string first, string second, bool ignoreCase)
		{
			int commonChars = EqualStartingCharacterCount(first, second, ignoreCase: ignoreCase);

			// If nothing matches
			if (commonChars == 0)
				return commonChars;

			// Or we're a full string and equal length or match to a separator
			if (commonChars == first.Length && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
				return commonChars;

			if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
				return commonChars;

			// It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
			while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
				commonChars--;

			return commonChars;
		}

		// Adapted from CoreFX sources
		static unsafe int EqualStartingCharacterCount(string first, string second, bool ignoreCase)
		{
			if (String.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
				return 0;

			int commonChars = 0;
			fixed (char* f = first)
			{
				fixed (char* s = second)
				{
					char* l = f;
					char* r = s;
					char* leftEnd = l + first.Length;
					char* rightEnd = r + second.Length;

					while (l != leftEnd && r != rightEnd && (*l == *r || (ignoreCase && char.ToUpperInvariant((*l)) == char.ToUpperInvariant((*r)))))
					{
						commonChars++;
						l++;
						r++;
					}
				}
			}

			return commonChars;
		}

		// Adapted from CoreFX sources
		static bool EndsInDirectorySeparator(string path) => path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);

		static bool IsDirectorySeparator(char c)
		{
			if (!IsWindows)
				return c == Path.DirectorySeparatorChar;
			return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
		}

		// Adapted from CoreFX sources
		static int GetRootLength(string path)
		{
			if (!IsWindows)
				return path.Length > 0 && IsDirectorySeparator(path[0]) ? 1 : 0;
			return GetRootLength_ForWindows(path);
		}

		internal static bool IsValidDriveChar(char value)
		{
			return ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'));
		}

		// Adapted from CoreFX sources
		static bool IsExtended(string path)
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
		static bool IsDevice(string path)
		{
			// If the path begins with any two separators is will be recognized and normalized and prepped with
			// "\??\" for internal usage correctly. "\??\" is recognized and handled, "/??/" is not.
			return IsExtended(path) ||
				(
					path.Length >= DevicePrefixLength
					&& IsDirectorySeparator(path[0])
					&& IsDirectorySeparator(path[1])
					&& (path[2] == '.' || path[2] == '?')
					&& IsDirectorySeparator(path[3])
				);
		}

		// Adapted from CoreFX sources
		static bool IsDeviceUNC(string path)
		{
			return path.Length >= UncExtendedPrefixLength
				&& IsDevice(path)
				&& IsDirectorySeparator(path[7])
				&& path[4] == 'U'
				&& path[5] == 'N'
				&& path[6] == 'C';
		}

		// Adapted from CoreFX sources
		static int GetRootLength_ForWindows(string path)
		{
			int pathLength = path.Length;
			int i = 0;

			bool deviceSyntax = IsDevice(path);
			bool deviceUnc = deviceSyntax && IsDeviceUNC(path);

			if ((!deviceSyntax || deviceUnc) && pathLength > 0 && IsDirectorySeparator(path[0]))
			{
				// UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")
				if (deviceUnc || (pathLength > 1 && IsDirectorySeparator(path[1])))
				{
					// UNC (\\?\UNC\ or \\), scan past server\share

					// Start past the prefix ("\\" or "\\?\UNC\")
					i = deviceUnc ? UncExtendedPrefixLength : UncPrefixLength;

					// Skip two separators at most
					int n = 2;
					while (i < pathLength && (!IsDirectorySeparator(path[i]) || --n > 0))
						i++;
				}
				else
				{
					// Current drive rooted (e.g. "\foo")
					i = 1;
				}
			}
			else if (deviceSyntax)
			{
				// Device path (e.g. "\\?\.", "\\.\")
				// Skip any characters following the prefix that aren't a separator
				i = DevicePrefixLength;
				while (i < pathLength && !IsDirectorySeparator(path[i]))
					i++;

				// If there is another separator take it, as long as we have had at least one
				// non-separator after the prefix (e.g. don't take "\\?\\", but take "\\?\a\")
				if (i < pathLength && i > DevicePrefixLength && IsDirectorySeparator(path[i]))
					i++;
			}
			else if (pathLength >= 2 && path[1] == VolumeSeparatorChar && IsValidDriveChar(path[0]))
			{
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