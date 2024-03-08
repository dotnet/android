using System;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Prepare;

class SevenZipVersionParser : ProgramVersionParser
{
	const string VersionArgument = "--help";
	readonly Regex fallbackRegex;
	readonly Regex modernRegex;

	public SevenZipVersionParser (string programName, Regex fallbackRegex, Log? log = null)
		: base (programName, VersionArgument, 0, log)
	{
		this.fallbackRegex = fallbackRegex;
		modernRegex = VersionFetchers.MakeRegex (@"^7-Zip (\(a\) ){0,1}(?<Version>[\d]+\.[\d]+)");
	}

	protected override string ParseVersion (string programOutput)
	{
		string output = programOutput.Trim ();
		if (String.IsNullOrEmpty (output)) {
			Log.WarningLine ($"Unable to parse version of {ProgramName} because version output was empty");
			return DefaultVersionString;
		}

		string ret = String.Empty;
		string[] lines = programOutput.Split (RegexProgramVersionParser.LineSeparator);

		// First try to find the official 7zip release version
		foreach (string l in lines) {
			string line = l.Trim ();

			if (line.Length == 0) {
				continue;
			}

			if (line.StartsWith ("7-Zip", StringComparison.OrdinalIgnoreCase)) {
				// Strings of the form:
				//   7-Zip 23.01 (x64) : Copyright (c) 1999-2023 Igor Pavlov : 2023-06-20
				//   7-Zip (a) 23.01 (x64) : Copyright (c) 1999-2023 Igor Pavlov : 2023-06-20
				//   7-Zip (a) 18.01 (x64) : Copyright (c) 1999-2018 Igor Pavlov : 2018-01-28
				//   7-Zip (a) 18.01 (x86) : Copyright (c) 1999-2018 Igor Pavlov : 2018-01-28
				if (RegexProgramVersionParser.TryMatch (modernRegex, line, out ret)) {
					return ret;
				}
			}

			// Since we know we're dealing with `--help` option output, we can short-circuit things
			if (line.StartsWith ("Usage:", StringComparison.OrdinalIgnoreCase)) {
				break;
			}
		}

		// Modern version wasn't found, try again with the fallback one
		foreach (string l in lines) {
			string line = l.Trim ();

			if (line.Length == 0) {
				continue;
			}

			if (RegexProgramVersionParser.TryMatch (fallbackRegex, line, out ret)) {
				return ret;
			}
		}

		return DefaultVersionString;
	}
}
