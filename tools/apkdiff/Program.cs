using System;
using System.IO;
using Mono.Options;

using static System.Console;

namespace apkdiff {
	class Program {
		static readonly string Name = "apkdiff";

		public static string Comment;
		public static bool SaveDescriptions;
		public static string SaveDescription1, SaveDescription2;
		public static bool Verbose;

		public static long AssemblyRegressionThreshold;
		public static long ApkRegressionThreshold;
		public static int RegressionCount;

		public static void Main (string [] args)
		{
			var (path1, path2) = ProcessArguments (args);

			var desc1 = ApkDescription.Load (path1, SaveDescription1);

			if (path2 != null) {
				var desc2 = ApkDescription.Load (path2, SaveDescription2);

				desc1.Compare (desc2);

				if (ApkRegressionThreshold != 0 && (desc2.PackageSize - desc1.PackageSize) > ApkRegressionThreshold) {
					Error ($"PackageSize increase {desc2.PackageSize - desc1.PackageSize:#,0} is {desc2.PackageSize - desc1.PackageSize - ApkRegressionThreshold:#,0} bytes more than the threshold {ApkRegressionThreshold:#,0}. apk1 size: {desc1.PackageSize:#,0} bytes, apk2 size: {desc2.PackageSize:#,0} bytes.");
					RegressionCount ++;
				}
			}

			if (RegressionCount > 0) {
				Error ($"Size regression occured, {RegressionCount:#,0} check(s) failed.");
				Environment.Exit (3);
			}
		}

		static (string, string) ProcessArguments (string [] args)
		{
			var help = false;
			int helpExitCode = 0;
			var options = new OptionSet {
				$"Usage: {Name}.exe OPTIONS* <package1.[apk|aab][desc]> [<package2.[apk|aab][desc]>]",
				"",
				"Compares APK/AAB packages content or APK/AAB package with content description",
				"",
				"Copyright 2020 Microsoft Corporation",
				"",
				"Options:",
				{ "c|comment=",
					"Comment to be saved inside description file",
				  v => Comment = v },
				{ "h|help|?",
					"Show this message and exit",
				  v => help = v != null },
				{ "test-apk-size-regression=",
					"Check whether apk size increased more than {BYTES}",
				  v => ApkRegressionThreshold = long.Parse (v) },
				{ "test-assembly-size-regression=",
					"Check whether any assembly size increased more than {BYTES}",
				  v => AssemblyRegressionThreshold = long.Parse (v) },
				{ "s|save-descriptions",
					"Save .[apk|aab]desc description files next to the package(s) or to the specified path",
				  v => SaveDescriptions = true },
				{ "save-description-1=",
					"Save .[apk|aab]desc description for first package to {PATH}",
				  v => SaveDescription1 = v },
				{ "save-description-2=",
					"Save .[apk|aab]desc description for second package to {PATH}",
				  v => SaveDescription2 = v },
				{ "v|verbose",
					"Output information about progress during the run of the tool",
				  v => Verbose = true },
			};

			var remaining = options.Parse (args);

			foreach (var s in remaining) {
				if (s.Length > 0 && (s [0] == '-' || s [0] == '/') && !File.Exists (s)) {
					Error ($"Unknown option: {s}");
					help = true;
					helpExitCode = 99;
				}
			}

			if (help || args.Length < 1) {
				options.WriteOptionDescriptions (Out);

				Environment.Exit (helpExitCode);
			}

			if (remaining.Count != 2 && (ApkRegressionThreshold != 0 || AssemblyRegressionThreshold != 0)) {
				Error ("Please specify 2 APK packages for regression testing.");
				Environment.Exit (2);
			}

			if (remaining.Count != 2 && (remaining.Count != 1 || !SaveDescriptions)) {
				Error ("Please specify 2 APK/AAB packages to compare or 1 when using the -s option.");
				Environment.Exit (1);
			}

			return (remaining [0], remaining.Count > 1 ? remaining [1] : null);
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
