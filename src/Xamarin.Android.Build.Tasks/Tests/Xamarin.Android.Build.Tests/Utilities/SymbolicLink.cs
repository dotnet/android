using System;
using System.IO;

namespace Xamarin.Android.Build
{
	static class SymbolicLink
	{
		public static bool Create (string source, string target)
		{
			if (Directory.Exists (source))
				return true;

			Directory.CreateDirectory (Path.GetDirectoryName (source));

			// Unix symlink targets are relative to the link's directory, so resolve them against the current directory;
			// Windows previously passed the target through unchanged.
			string linkTarget = OperatingSystem.IsWindows () ? target : Path.GetFullPath (target);
			try {
				// On Windows this uses SYMBOLIC_LINK_FLAG_DIRECTORY and, on builds that support it,
				// SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE (Developer Mode).
				Directory.CreateSymbolicLink (source, linkTarget);
			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || (OperatingSystem.IsWindows () && e is ArgumentException)) {
				if (!Directory.Exists (source)) {
					Console.Error.WriteLine ($"Unable to create symbolic link from `{source}` to `{target}`: {e.Message}");
					return false;
				}
			}
			return true;
		}
	}
}
