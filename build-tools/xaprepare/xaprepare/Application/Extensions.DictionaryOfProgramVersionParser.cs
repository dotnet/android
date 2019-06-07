using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Prepare
{
	static class DictionaryOfProgramVersionParser_Extensions
	{
		public static void Add (this Dictionary<string, ProgramVersionParser> dict, string programName, string versionArguments, Regex regex, uint versionOutputLine = 0, Log log = null)
		{
			if (dict == null)
				throw new ArgumentNullException (nameof (dict));

			if (dict.ContainsKey (programName)) {
				Log.Instance.WarningLine ($"Entry for {programName} version matcher already defined. Ignoring the new entry ({regex})");
				return;
			}

			dict [programName] = new RegexProgramVersionParser (programName, versionArguments, regex, versionOutputLine, log);
		}
	}
}
