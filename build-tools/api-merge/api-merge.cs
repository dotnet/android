using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Mono.Options;

namespace Xamarin.Android.ApiMerge {

	class Merge {
		public static int Main (string[] args)
		{
			bool show_help = false, show_version = false;
			string dest = null;
			string glob = null;
			string lastDescription = null;
			var options = new OptionSet () {
				"Usage: api-merge -o=FILE DESCRIPTIONS+",
				"",
				"Merge API descriptions into a single API description.",
				"",
				"Options:",
				{ "o=",
				  "Output {FILE} to create.",
				  v => dest = v },
				{ "s|sort-glob=",
				  "{GLOB} pattern for sorting DESCRIPTIONS.",
				  v => glob = v },
				{ "last-description=",
				  "Last {DESCRIPTION} to process. Any later descriptions are ignored.",
				  v => lastDescription = v },
				{ "version",
				  "Output version information and exit.",
				  v => show_version = v != null },
				{ "h|?|help",
				  "Show this help message and exit.",
				  v => show_help = v != null },
			};
			List<string> sources;
			try {
				sources = options.Parse (args);
			}
			catch (Exception e) {
				Console.Error.WriteLine ("api-merge: {0}", e);
				return 1;
			}
			if (show_version) {
				Console.WriteLine ("api-merge {0}", "<<Not generated at this time>>");
				return 0;
			}
			if (show_help) {
				options.WriteOptionDescriptions (Console.Out);
				return 0;
			}
			for (int i = sources.Count - 1; i >= 0; i--) {
				if (!File.Exists (sources [i])) {
					Console.WriteLine ("warning: skipping file {0}...", sources [i]);
					sources.RemoveAt (i);
				}
			}
			if (sources.Count == 0) {
				Console.Error.WriteLine ("api-merge: Missing DESCRIPTIONS+.");
				return 2;
			}
			SortSources (sources, glob);
			ApiDescription context = new ApiDescription (sources [0]);
			for (int i = 1; i < sources.Count; i++) {
				if (sources [i-1] == lastDescription)
					break;
				context.Merge (sources [i]);
			}
			context.Save (dest);
			return 0;
		}

		static void SortSources (List<string> sources, string globPattern)
		{
			if (globPattern == null)
				return;

			var regex = GlobToRegex (globPattern);
			Comparison<string> c = (x, y) => {
				var mx = regex.Match (x);
				var my = regex.Match (y);
				if (!(mx.Success && my.Success))
					return string.Compare (x, y, StringComparison.OrdinalIgnoreCase);
				for (int i = 1; i < mx.Groups.Count; i++) {
					var vx = mx.Groups [i].Value;
					var vy = my.Groups [i].Value;
					int nx, ny;
					if (int.TryParse (vx, out nx) && int.TryParse (vy, out ny)) {
						if (nx == ny)
							continue;
						return nx.CompareTo (ny);
					}
					var r = string.Compare (vx, vy, StringComparison.OrdinalIgnoreCase);
					if (r != 0) {
						return r;
					}
				}
				return 0;
			};
			sources.Sort (c);
		}

		static Regex GlobToRegex (string globPattern)
		{
			var regexPattern = new StringBuilder ("^")
				.Append (globPattern)
				.Append ("$");
			for (int i = regexPattern.Length - 1; i >= 0; --i) {
				switch (regexPattern [i]) {
				case '.':
				case '(':
				case ')':
					regexPattern.Insert (i, "\\");
					break;
				case '*':
					regexPattern.Insert (i + 1, ')');
					regexPattern.Insert (i,     "(.");
					break;
				case '?':
					regexPattern.Insert (i + 1, ')');
					regexPattern.Insert (i,     '(');
					break;
				}
			}
			return new Regex (regexPattern.ToString ());
		}
	}
}
