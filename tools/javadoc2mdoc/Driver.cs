using System;
using System.Collections.Generic;
using System.IO;

using Mono.Options;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{

	class Driver
	{
		const string AppName = "javadoc-to-mdoc";
		static bool importSamples = false;

		public static int Main (string [] args)
		{
			bool show_help = false;
			var types = new HashSet<string> ();
			var skip = new HashSet<string> ();
			string samplesPath = null;
			string sourceRoot = null, destinationRoot = null;
			int messageLevel = 0;

			var options = new OptionSet () {
				{ "doc-dir=",
					"{DIRECTORY} for the JavaDoc HTML docs to import.\n" +
						"Defaults to `SDK-DIR/docs/reference`.",
					v => sourceRoot = v },
				{ "enum-map=",
					"Ignored; obsolete option.",
					v => {} },
				{ "metadata=",
					"Ignored; obsolete option.",
					v => {} },
				{ "o|out=",
					"Directory containing mdoc(5) documentation to import into.",
					v => destinationRoot = v },
				{ "sdk-dir=",
					"{DIRECTORY} for the Android SDK.",
					v => sourceRoot = Path.Combine (v, "docs/reference") },
				{ "skip-type=",
					".NET {TYPE} to skip doc importfor.\n",
					v => skip.Add (v) },
				{ "type=",
					".NET {TYPE} to import documentation for.\n" +
					"Largely for debugging purposes, so we don't need to import everything.",
					v => types.Add (v) },
				{ "v|verbose:",
					"Increase verbosity of messages, or set verbosity to {LEVEL}.",
					(int? v) => messageLevel = v.HasValue ? v.Value : messageLevel + 1},
				{ "h|?|help",
					"Show this help message and exit.",
					v => show_help = v != null },
				{ "import-samples",
				  "If set, the tool will import samples from an external repository",
				  v => importSamples = true },
				{ "samples-repository=",
				  "Tell where to find the sample .zip repository",
				  v => samplesPath = v},
			};

			List<string> assemblies;
			try {
				assemblies = options.Parse (args);
			} catch (Exception e) {
				Error (messageLevel == 0 ? e.Message : e.ToString ());
				Console.Error.WriteLine ("See '{0} --help' for more information.", AppName);
				return 1;
			}

			if (show_help) {
				ShowHelp (options);
				return 0;
			}

			if (string.IsNullOrEmpty (sourceRoot) || !Directory.Exists (sourceRoot)) {
				Error ("Missing or invalid --doc-dir=DIR or --sdk-dir=DIR directory.");
				return 1;
			}

			if (string.IsNullOrEmpty (destinationRoot) || !Directory.Exists (destinationRoot)) {
				Error ("Missing or invalid --out=DIR directory.");
				return 1;
			}

			if (string.IsNullOrEmpty (samplesPath) || !File.Exists (samplesPath))
				samplesPath = Path.Combine (destinationRoot, "samples.zip");

			if (assemblies.Count == 0) {
				ShowHelp (options);
				return 1;
			}
			try {
				Application.Run (new ProcessingContext () {
					SourceDocumentationRoot = sourceRoot,
					DestDocumentationRoot = destinationRoot,
					ImportSamples = importSamples,
					SamplesPath = samplesPath,
					MessageLevel = messageLevel,
					Assemblies = assemblies,
					TypesToProcess = types,
					TypesToSkip = skip
				});
				return 0;
			} catch (Exception e) {
				Error ("{0}", messageLevel == 0 ? e.Message : e.ToString ());
				return 1;
			}
		}

		internal static void Error (string message)
		{
			Console.Error.Write (AppName);
			Console.Error.Write (" : ");
			Console.Error.WriteLine (message);
		}

		internal static void Error (string formatMessage, params object [] args)
		{
			Console.Error.Write (AppName);
			Console.Error.Write (" : ");
			Console.Error.WriteLine (formatMessage, args);
		}

		static void ShowHelp (OptionSet options)
		{
			Console.WriteLine ("Usage: {0} [OPTIONS]+ ASSEMBLY+", AppName);
			Console.WriteLine ();
			Console.WriteLine ("JavaDoc to mdoc(5) translation tool.");
			Console.WriteLine ();
			Console.WriteLine ("Available Options:");
			options.WriteOptionDescriptions (Console.Out);
			Console.WriteLine ();
			Console.WriteLine ("Either the --doc-dir or the --sdk-dir option must be specified.");
			Console.WriteLine ("The --out option is required.");
			Console.WriteLine ();
			Console.WriteLine ("Copyright (C) 2010, Novell, Inc.");
			Console.WriteLine ("Copyright (C) 2015, Xamarin, Inc.");
			Console.WriteLine ("Copyright (C) 2017, Microsoft, Inc.");
		}
	}
}
