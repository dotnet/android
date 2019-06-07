using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Prepare
{
	class VersionFetchers
	{
		const string StandardVersionRegex = "(?<Version>(\\d+)\\.(\\d+\\.?)(\\d+\\.)?(\\d+)?)";
		const string JavaVersionRegex = "(?<Version>(\\d+)\\.(\\d+)\\.(\\d+)_(\\d+))";

		static readonly Regex StandardVersionAtEOL = MakeRegex ($" {StandardVersionRegex}"); // $ isn't needed, we match
																							 // line by line
		static readonly Regex StandardVersionAtBOL = MakeRegex ($"^{StandardVersionRegex}");

		public readonly Dictionary<string, ProgramVersionParser> Fetchers = new Dictionary <string, ProgramVersionParser> (Context.Instance.OS.DefaultStringComparer) {
			{"7z",       "--help",    MakeRegex ($"Version {StandardVersionRegex}"),   3},
			{"ant",      "-version",  MakeRegex ($"version {StandardVersionRegex}"),   1},
			{"autoconf", "--version", StandardVersionAtEOL,                            1},
			{"automake", "--version", StandardVersionAtEOL,                            1},
			{"brew",     "--version", MakeRegex ($"^Homebrew {StandardVersionRegex}"), 1},
			{"cmake",    "--version", StandardVersionAtEOL,                            1},
			{"curl",     "--version", MakeRegex ($"^curl {StandardVersionRegex}"),     1},
			{"g++",      "--version", StandardVersionAtEOL,                            1},
			{"gcc",      "--version", StandardVersionAtEOL,                            1},
			{"git",      "--version", StandardVersionAtEOL,                            1},
			{"gmake",    "--version", StandardVersionAtEOL,                            1},
			{"java",     "-version",  MakeRegex ($" \"{JavaVersionRegex}\"$"),         1},
			{"javac",    "-version",  MakeRegex ($"{JavaVersionRegex}$"),              1},
			{"libtool",  "--version", StandardVersionAtEOL,                            1},
			{"make",     "--version", StandardVersionAtEOL,                            1},
			{"mono",     "--version", MakeRegex ($"version {StandardVersionRegex}"),   1},
			{"ninja",    "--version", MakeRegex (StandardVersionRegex),                1},
			{"sqlite3",  "--version", StandardVersionAtBOL,                            1},
		};

		static Regex MakeRegex (string regex)
		{
			return new Regex (regex, RegexOptions.Compiled | RegexOptions.Singleline);
		}
	}
}
