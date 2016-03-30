using System;
using System.Globalization;
using Mono.Options;
using Mono.CSharp;

namespace Xamarin.Android.Tools.LogcatParse {

	public class Program {

		public static int Main (string[] args)
		{
			bool help = false;
			int? pid = null;
			var options = new OptionSet () {
				"logcat-parse: Parse `adb logcat` output to analyze GREF information.",
				"",
				"usage: logcat-parse [-p PID] FILE",
				{ "p|pid=",
				  "The {PID} to filter GREF output.",
				  v => pid = int.Parse (v, CultureInfo.InvariantCulture)
				},
				{ "h|?|help",
				  "Show this message and exit.",
				  v => help = v != null },
			};
			var files = options.Parse (args);
			if (help) {
				options.WriteOptionDescriptions (Console.Out);
				return 0;
			}

			Console.WriteLine ("// `adb logcat` GREF parsing utility");
			Console.WriteLine ("//");
			Console.WriteLine ("// Use `Grefs.Parse(stream)` to parse a file containing `adb logcat` output.");
			Console.WriteLine ("// Grefs.AllocatedPeers contains all exposed Java.Lang.Object instances.");
			Console.WriteLine ("// Grefs.AlivePeers contains those still alive by the end of parsing.");

			var settings = new CompilerSettings () {
				Unsafe = true
			};

			var printer = new ConsoleReportPrinter ();

			var eval = new Evaluator (new CompilerContext (settings, printer));
			eval.ReferenceAssembly (typeof(Program).Assembly);
			eval.Run ("using Xamarin.Android.Tools.LogcatParse;");
			if (files.Count > 0) {
				if (files.Count > 1)
					Console.Error.WriteLine ("logcat-parse: More than one file is unsupported. Loading: {0}", files [0]);
				Console.WriteLine ("var grefs = Grefs.Parse(\"{0}\"{1});", files [0],
						pid.HasValue ? ", " + pid.Value : "");
				eval.Run ("Grefs grefs;");
				eval.Run (
						"using (var __source = new System.IO.StreamReader(\"" +
						files [0] +
						"\")) grefs = Grefs.Parse(__source" +
						(pid.HasValue ? (", " + pid.ToString ()) : "") + ");");
			}

			eval.InteractiveBaseClass       = typeof (Mono.InteractiveBaseShell);
			eval.DescribeTypeExpressions    = true;
			eval.WaitOnTask                 = true;

			var shell   = new Mono.CSharpShell (eval);
			return shell.Run (new string[0]);
		}
	}
}
