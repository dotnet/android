using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Options;

namespace Xamarin.Android.Tools.ExtractApiMergedXml
{
	class MainClass
	{
		public const string GeneratorToolName = "generator.exe";
		public const string ApiMergeToolName = "api-merge.exe";
		
		class CommandLineOptions
		{
			public IList<string> SourceJars { get; set; } = new List<string> ();
			public IList<string> ReferenceJars { get; set; } = new List<string> ();
			public IList<string> ManagedLibraryPaths { get; set; } = new List<string> ();
			public IList<string> AssemblyReferences { get; set; } = new List<string> ();
			public string ApiLevel { get; set; }
			public string LoggingVerbosity { get; set; }
			public string OutputDirectory { get; set; }
			public string OutputFile { get; set; }
			public IList<string> JavaDocs { get; set; } = new List<string> ();
		}

		public static int Main (string [] args)
		{
			var opts = new CommandLineOptions ();

			bool help = false;

			var parser = new OptionSet {
				"Usage: generator.exe OPTIONS+ API_DESCRIPTION",
				"",
				"Generates C# source files to bind Java code described by API_DESCRIPTION.",
				"",
				"Copyright 2016 Xamarin, Inc.",
				"",
				"Options:",
				{ "L=",
					"{PATH} to look for referenced assemblies..",
					v => opts.ManagedLibraryPaths.Add (v) },
				{ "outdir=",
					"[PATH] to intermediate XML output directory.",
					v => opts.OutputDirectory = v },
				{ "o|out=",
					"API definition output XML file.",
					v => opts.OutputFile = v },
				{ "jar=",
					"{Jar} to extract API.",
					v => opts.SourceJars.Add (v) },
				{ "ref=",
					"{Jar} to references, without extracting API.",
					v => opts.ReferenceJars.Add (v) },
				{ "dll=",
					"{ASSEMBLY} to reference.",
					v => opts.AssemblyReferences.Add (v) },
				{ "sdk-platform|api-level=",
					"SDK Platform {VERSION}/API level.",
					v => opts.ApiLevel = v },
				{ "v:",
					"Logging Verbosity",
					v => opts.LoggingVerbosity = v },
				{ "javadocs=",
					"{PATH} to API reference.",
					v => opts.JavaDocs.Add (v) },
				{ "h|?|help",
					"Show this message and exit.",
					v => help = v != null },
			};

			parser.Parse (args);

			if (help) {
				parser.WriteOptionDescriptions (Console.Error);
				return 255;
			}

			var thisDir = Path.GetDirectoryName (new Uri (typeof (MainClass).Assembly.CodeBase).LocalPath);
			var generator = Path.Combine (thisDir, GeneratorToolName);
			var apiMerge = Path.Combine (thisDir, ApiMergeToolName);

			// class-parse
			bool hasConflicts = opts.SourceJars.Select (s => Path.GetFileName (s)).Distinct ().Count () != opts.SourceJars.Count;
			var classParseXmls = new List<string> ();
			var apiAdjustedXmls = new List<string> ();

			foreach (var jar in opts.SourceJars) {
				var classPath = new Bytecode.ClassPath () {
					ApiSource = "class-parse",
					DocumentationPaths = (opts.JavaDocs ?? Enumerable.Empty<string> ())
				};
				if (Bytecode.ClassPath.IsJarFile (jar))
					classPath.Load (jar);
				var outname = hasConflicts ? Path.GetFileName (jar) : Path.GetFileName (Path.GetDirectoryName (jar)) + '_' + Path.GetFileName (jar);
				var outfile = Path.Combine (opts.OutputDirectory, Path.ChangeExtension (outname, ".class-parse"));
				classParseXmls.Add (outfile);
				Console.Error.WriteLine ("Saving {0} ...", outfile);
				classPath.SaveXmlDescription (outfile);
			}

			// api-xml-adjuster
			foreach (var xml in classParseXmls) {
				var outfile = Path.ChangeExtension (xml, ".xml");
				apiAdjustedXmls.Add (outfile);

				var aargs = new List<string> ();
				aargs.Add (xml);
				if (!string.IsNullOrWhiteSpace (opts.LoggingVerbosity))
					aargs.Add ("-v:" + opts.LoggingVerbosity);
				aargs.Add ("--assembly=dummy");
				aargs.Add ("--only-xml-adjuster");
				aargs.Add ("--xml-adjuster-output=" + outfile);
				aargs.Add ("--api-level=" + opts.ApiLevel);
				foreach (var dll in opts.AssemblyReferences)
					aargs.Add ("--ref=" + dll);
				foreach (var docs in opts.JavaDocs)
					aargs.Add ("--docs=" + docs);

				Console.Error.WriteLine ("# {0} {1}", generator, string.Join (" ", aargs));
				var aproc = Process.Start (generator, string.Join (" ", aargs));
				aproc.WaitForExit ();
				if (aproc.ExitCode != 0)
					return aproc.ExitCode > 0 ? 1 : 0;
			}

			// api-merge
			Console.Error.WriteLine ("# {0} {1}", apiMerge, string.Join (" ", apiAdjustedXmls));
			var margs = new List<string> ();
			margs.Add ("-o:" + opts.OutputFile);
			margs.AddRange (apiAdjustedXmls);
			var mproc = Process.Start (apiMerge, string.Join (" ", margs));
			mproc.WaitForExit ();
			return mproc.ExitCode > 0 ? 2 : 0;
		}
	}
}
