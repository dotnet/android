using Mono.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using static System.Console;

namespace jittimes {
	class MainClass {
		static bool Verbose;
		static readonly string Name = "jit-times";
		static readonly List<Regex> methodNameRegexes = new List<Regex> ();

		enum SortKind {
			Unsorted,
			Self,
			Total,
			Method,
		};

		static SortKind sortKind = SortKind.Self;

		static string ProcessArguments (string [] args)
		{
			var help = false;
			var options = new OptionSet {
				$"Usage: {Name}.exe OPTIONS* <methods-file>",
				"",
				"Processes JIT methods file from XA app with debug.mono.log=timing enabled",
				"",
				"Copyright 2019 Microsoft Corporation",
				"",
				"Options:",
				{ "h|help|?",
					"Show this message and exit",
				  v => help = v != null },
				{ "m|method=",
					"Process only methods whose names match {TYPE-REGEX}.",
				  v => methodNameRegexes.Add (new Regex (v)) },
				{ "s|sort-self-times",
					"Sort by self times. (this is default ordering)",
				  v => sortKind = SortKind.Self },
				{ "t|sort-total-times",
					"Sort by total times.",
				  v => sortKind = SortKind.Total },
				{ "n|sort-methods",
					"Sort by method names.",
				  v => sortKind = SortKind.Method },
				{ "u|unsorted",
					"Show unsorted results.",
				  v => sortKind = SortKind.Unsorted },
				{ "v|verbose",
				  "Output information about progress during the run of the tool",
				  v => Verbose = true },
			};

			var remaining = options.Parse (args);

			if (help || args.Length < 1) {
				options.WriteOptionDescriptions (Out);

				Environment.Exit (0);
			}

			if (remaining.Count != 1) {
				Error ("Please specify one <methods-file> to process.");
				Environment.Exit (2);
			}

			return remaining [0];
		}

		static bool TryMatchTimeStamp (Regex regex, string line, out string method, out Timestamp time)
		{
			var match = regex.Match (line);

			if (!match.Success || match.Groups.Count <= 2) {
				method = null;
				time = new Timestamp ();
				return false;
			}

			method = match.Groups [1].Value;
			time = Timestamp.Parse (match.Groups [2].Value);

			return true;
		}

		static readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo> ();

		static bool ShouldPrint (string method)
		{
			if (methodNameRegexes.Count > 0) {
				var success = false;

				foreach (var filter in methodNameRegexes) {
					var match = filter.Match (method);
					success |= match.Success;
				}

				return success;
			}

			return true;
		}

		static void PrintIndented (MethodInfo info, ref Timestamp sum, int level = 0)
		{
			if (!ShouldPrint (info.method))
				return;

			sum += info.self;

			WriteLine ($"{info.total.Milliseconds (),10:F2} | {info.self.Milliseconds (),10:F2} | {"".PadRight (level * 2)}{info.method}");

			if (info.inner == null)
					return;

			foreach (var im in info.inner)
				PrintIndented (im, ref sum, level + 1);
		}

		static MethodInfo GetMethodInfo (string method)
		{
			MethodInfo info;

			if (methods.TryGetValue (method, out info))
				return info;

			info = new MethodInfo { method = method };
			methods [method] = info;

			return info;
		}

		public static int Main (string [] args)
		{
			var path = ProcessArguments (args);
			var file = File.OpenText (path);

			var beginRegex = new Regex (@"^JIT method +begin: (.*) elapsed: (.*)$");
			var doneRegex = new Regex (@"^JIT method +done: (.*) elapsed: (.*)$");

			string line;
			int lineNumber = 0;

			var jitMethods = new Stack<MethodInfo> ();
			string method;
			Timestamp time;
			Timestamp sum = new Timestamp ();
			ColorWriteLine ("Total (ms) |  Self (ms) | Method", ConsoleColor.Yellow);

			while ((line = file.ReadLine ()) != null) {
				lineNumber++;

				if (TryMatchTimeStamp (beginRegex, line, out method, out time)) {
					var info = GetMethodInfo (method);

					if (info.state != MethodInfo.State.None && Verbose)
						Warning ($"duplicite begin of `{info.method}`");

					info.state = MethodInfo.State.Begin;
					info.begin = time;

					jitMethods.Push (info);

					continue;
				}

				if (TryMatchTimeStamp (doneRegex, line, out method, out time)) {
					var info = GetMethodInfo (method);

					if (info.state != MethodInfo.State.Begin) {
						if (Verbose)
							Warning ($"missing JIT begin for method {method}");
						continue;
					}

					info.state = MethodInfo.State.Done;
					info.done = time;
					info.total = info.done - info.begin;

					info.CalcSelfTime ();
					if (Verbose) {
						if (info.self.nanoseconds < 0)
							Warning ($"negative self time for method {method}: {info.self}");
						if (info.total.nanoseconds < 0)
							Warning ($"negative total time for method {method}: {info.total}");
					}

					jitMethods.Pop ();

					if (jitMethods.Count > 0) {
						var outerMethod = jitMethods.Peek ();

						outerMethod.AddInner (info);
					} else if (sortKind == SortKind.Unsorted)
						PrintIndented (info, ref sum);
				}
			}

			if (sortKind != SortKind.Unsorted)
				sum = PrintSortedMethods ();

			ColorWriteLine ($"Sum of self time (ms): {sum.Milliseconds ():F2}", ConsoleColor.Yellow);

			return 0;
		}

		static Timestamp PrintSortedMethods ()
		{
			IOrderedEnumerable<KeyValuePair<string, MethodInfo>> enumerable = null;
			Timestamp sum = new Timestamp ();

			switch (sortKind) {
			case SortKind.Self:
				enumerable = methods.OrderByDescending (p => p.Value.self);
				break;
			case SortKind.Total:
				enumerable = methods.OrderByDescending (p => p.Value.total);
				break;
			case SortKind.Method:
				enumerable = methods.OrderByDescending (p => p.Value.method);
				break;
			default:
				throw new InvalidOperationException ("unknown sort order");
			}

			foreach (var pair in enumerable) {
				if (!ShouldPrint (pair.Key))
					continue;

				var info = pair.Value;
				WriteLine ($"{info.total.Milliseconds (),10:F2} | {info.self.Milliseconds (),10:F2} | {info.method}");

				sum += info.self.Positive();
			}

			return sum;
		}

		static void ColorMessage (string message, ConsoleColor color, TextWriter writer, bool writeLine = true)
		{
			ForegroundColor = color;

			if (writeLine)
				writer.WriteLine (message);
			else
				writer.Write (message);
			ResetColor ();
		}

		public static void ColorWriteLine (string message, ConsoleColor color) => ColorMessage (message, color, Out);

		public static void ColorWrite (string message, ConsoleColor color) => ColorMessage (message, color, Out, false);

		public static void Error (string message) => ColorMessage ($"Error: {Name}: {message}", ConsoleColor.Red, Console.Error);

		public static void Warning (string message) => ColorMessage ($"Warning: {Name}: {message}", ConsoleColor.Yellow, Console.Error);
	}
}
