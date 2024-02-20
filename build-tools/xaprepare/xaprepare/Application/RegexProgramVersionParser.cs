using System;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Prepare
{
	/// <summary>
	///   Parses program output looking for a version string. The regular expression must produce a single match which
	///   contains a group called <c>Version</c>. The group's value is returned verbatim as the program version.
	///
	///   Input from the program is broken up into separate lines and the regular expression is applied to each of them
	///   separately. By default (when <see cref="VersionOutputLine"/> is <c>0</c>) all lines are processed, but when
	///   <see cref="VersionOutputLine"/> is set to any other value only that line is taken into consideration. This is
	///   done to make processing less ambiguous and faster.
	/// </summary>
	class RegexProgramVersionParser : ProgramVersionParser
	{
		public const string VersionGroupName = "Version";
		public static readonly char[] LineSeparator = new [] { '\n' };

		Regex rx;

		public RegexProgramVersionParser (string programName, string versionArguments, Regex regex, uint versionOutputLine = 0, Log? log = null)
		: base (programName, versionArguments, versionOutputLine, log)
		{
			if (regex == null)
			throw new ArgumentNullException (nameof (regex));
			rx = regex;
		}

		public RegexProgramVersionParser (string programName, string versionArguments, string regex, uint versionOutputLine = 0, Log? log = null)
		: base (programName, versionArguments, versionOutputLine, log)
		{
			if (String.IsNullOrEmpty (regex))
			throw new ArgumentException ("must not be null or empty", nameof (regex));

			rx = new Regex (regex, RegexOptions.Compiled);
		}

		protected override string ParseVersion (string programOutput)
		{
			string output = programOutput.Trim ();
			if (String.IsNullOrEmpty (output)) {
				Log.WarningLine ($"Unable to parse version of {ProgramName} because version output was empty");
				return DefaultVersionString;
			}

			string ret = String.Empty;
			string[] lines = programOutput.Split (LineSeparator);
			if (VersionOutputLine > 0) {
				if (lines.Length < VersionOutputLine) {
					Log.WarningLine ($"Not enough lines in version output of {ProgramName}: version number was supposed to be found on line {VersionOutputLine} but there are only {lines.Length} lines");
					return DefaultVersionString;
				}

				if (TryMatch (rx, lines [VersionOutputLine - 1], out ret) && !String.IsNullOrEmpty (ret)) {
					return ret;
				}

				return DefaultVersionString;
			}

			foreach (string line in lines) {
				if (TryMatch (rx, line, out ret))
				break;
			}

			return ret ?? DefaultVersionString;
		}

		public static bool TryMatch (Regex regex, string line, out string version)
		{
			version = String.Empty;

			Match match = regex.Match (line);
			if (!match.Success || match.Groups.Count <= 0) {
				return false;
			}

			foreach (Group group in match.Groups) {
				if (String.Compare (group.Name, VersionGroupName, StringComparison.OrdinalIgnoreCase) == 0) {
					version = group.Value;
					return true;
				}
			}

			return false;
		}
	}
}
