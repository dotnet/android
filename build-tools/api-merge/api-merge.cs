using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Mono.Options;

namespace Xamarin.Android.ApiMerge {

	class Merge {
		public static int Main (string[] args)
		{
			bool show_help = false, show_version = false;
			string dest = null;
			var options = new OptionSet () {
				"Usage: api-merge -o=FILE DESCRIPTIONS+",
				"",
				"Merge API descriptions into a single file.",
				"",
				"Available Options:",
				{ "o=",
				  "Output {FILE} to create.",
				  v => dest = v },
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
			if (sources.Count == 0) {
				Console.Error.WriteLine ("api-merge: Missing DESCRIPTIONS+.");
				return 2;
			}
			ApiDescription context = null;
			int i;
			for (i = 0; i < sources.Count; i++) {
				if (!File.Exists (sources [i])) {
					Console.WriteLine ("warning: skipping file {0}...", sources [i]);
					continue;
				}
				context = new ApiDescription (sources [i]);
				break;
			}
			i++;
			for (; i < sources.Count; i++) {
				if (!File.Exists (sources [i])) {
					Console.WriteLine ("warning: skipping file {0}...", sources [i]);
					continue;
				}
				context.Merge (sources [i]);
			}
			context.Save (dest);
			return 0;
		}
	}
}
