using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;

namespace ApplicationUtility;

/// <summary>
/// Application entry point for the <c>apput</c> (.NET for Android application analysis) command-line tool.
/// </summary>
class Program
{
	public const string AppName = "apput";
	const LogLevel DefaultLogLevel = LogLevel.Info;

	static int Main (string[] args)
	{
		var logBuffer = new StandardLogBuffer (DefaultLogLevel);
		logBuffer.SetLogFile (null); // will use the default path
		Log.SetLogBuffer (logBuffer);

		try {
			Log.Debug ($"Session started at: {DateTime.Now}");
			return Run (args, logBuffer);
		} catch (Exception ex) {
			Log.Error ("Unhandled exception.", ex);
			return 1;
		} finally {
			TempFileManager.Cleanup ();
			WriteLogFilePathToConsole (logBuffer);
			logBuffer.Dispose (); // Make sure it's done last
		}
	}

	static void WriteLogFilePathToConsole (StandardLogBuffer logBuffer)
	{
		if (String.IsNullOrWhiteSpace (logBuffer.LogFilePath)) {
			return;
		}

		try {
			Console.Error.WriteLine ();
			Console.Error.WriteLine ($"Session log file: {logBuffer.LogFilePath}");
		} catch (Exception) {
			// Ignore, there's not much we can do here anyway
		}
	}

	static string GetLogLevels ()
	{
		return String.Join (", ", Enum.GetValues<LogLevel> ().Select (v => v.ToString ().ToLowerInvariant ()));
	}

	static LogLevel ParseLogLevel (string? l)
	{
		if (String.IsNullOrEmpty (l)) {
			return GetDefaultAndLog (invalid: false);
		}

		if (Enum.TryParse<LogLevel> (l, ignoreCase: true, out LogLevel level)) {
			return level;
		}

		return GetDefaultAndLog (invalid: true);

		LogLevel GetDefaultAndLog (bool invalid)
		{
			string what = invalid ? "Invalid" : "No";
			Log.Debug ($"{what} log level name specified, using default: {DefaultLogLevel}");
			return DefaultLogLevel;
		}
	}

	static int Run (string[] args, StandardLogBuffer logBuffer)
	{
		bool showHelp = false;
		bool logFileSetByUser = false;
		var defaultCommand = new ReportCommand ();

		var commands = new CommandSet (AppName) {
			$"usage: {AppName} COMMAND <input_file>",
			"",
			".NET for Android application analysis utility.",
			"",
			"Global options:",
			{ "v=", $"Log/verbosity level. One of (case-insensitive): {GetLogLevels ()}", (string l) => logBuffer.MinimumConsoleLogLevel = ParseLogLevel (l) },
			{ "l=", $"Write all log messages to the specified file.", (string l) => { logBuffer.SetLogFile (l); logFileSetByUser = true; }},
			{ "help|h|?", "Show this message and exit.", v => showHelp = v != null },
			"",
			"Available commands (all commands support the `--help` parameter):",
			defaultCommand,
			new CommandSet ("extract") {
				new ExtractAssemblyCommand (),
				new ExtractManifestCommand (),
			},
		};

		commands.GetDefaultCommand = (List<string> extra) => {
			var newExtra = new List<string> {
				defaultCommand.Name,
			};
			newExtra.AddRange (extra);

			return newExtra;
		};

		commands.BeforeCommandInvoke = (Command command, List<string> extra) => {
			// All our commands require an input file as the first positional parameter.
			// If there's none, then we don't signal any error, just do nothing.
			if (extra.Count == 0) {
				return;
			}

			// If, however, there is one, we use it to override the log file name, if not
			// otherwise overridden by the user.
			if (logFileSetByUser) {
				return;
			}

			string fileName = $"{Program.AppName}-{Path.GetFileName (extra[0])}.log";
			logBuffer.SetLogFile (fileName);
		};

		return commands.Run (args);
	}
}

/// <summary>
/// CLI command that generates a full analysis report for an application aspect.
/// </summary>
class ReportCommand : BaseProgramCommand
{
	string? outputFile;

	public ReportCommand ()
		: base ("report", "Generate a full report on <input_file>")
	{
		Options.Add (
			"output|o=",
			"Write report to the specified output file. Defaults to the current directory, with report file name based on the aspect name as passed on the command line. Pass `-` to write to console.",
			v => outputFile = v
		);
	}

	protected override int DoInvoke (List<string> rest)
	{
		// We're invoked only if `rest` has at least one element.
		IAspect? aspect = GetAspect (rest[0]);
		if (aspect == null) {
			return 1;
		}

		if (String.IsNullOrEmpty (outputFile)) {
			outputFile = $"report-{Path.GetFileName (rest[0])}.md";
		} else if (outputFile == "-") {
			outputFile = null;
		}

		try {
			Reporter.Report (aspect, plainTextRendering: NoColor == true, outputFile: outputFile);
		} finally {
			if (!String.IsNullOrEmpty (outputFile)) {
				Console.Error.WriteLine ($"Report file: '{outputFile}'");
			}
		}

		return 0;
	}
}

/// <summary>
/// CLI command that extracts .NET assemblies (and optionally PDB files) from an application package.
/// </summary>
class ExtractAssemblyCommand : BaseProgramCommand
{
	const string AssembliesOutputDirDefault = "assemblies";

	string? outputDir;
	bool useRegex;
	bool extractPdb;
	bool noDecompress;
	string? architectures;

	public ExtractAssemblyCommand ()
		: base ("assembly", "Extract assemblies from the application package", synopsis: "assembly [OPTIONS] <input_file> [<assembly_name> [<assembly_name> ...]]")
	{
		Options.Add ("output|o=", $"Write output to directory `VALUE`; Defaults to '{AssembliesOutputDirDefault}'", v => outputDir = v);
		Options.Add ("a|arch=", "Extract only entries for the architectures specified by `VALUE`, a comma-separated list of architecture names. Defaults to 'all'", v => architectures = v);
		Options.Add ("r", "Treat each <assembly_name> as a regular expression.", v => useRegex = v != null);
		Options.Add ("d", "Extract also the accompanying PDB file, if found.", v => extractPdb = v != null);
		Options.Add ("c", "Do not decompress the assembly data if it is compressed in the container.", v => noDecompress = v != null);
		Options.Add ("");
		Options.Add ("<assembly_name> does not have to specify the extension. By default assembly names are treated as simplified glob patterns.");
		Options.Add ("If <assembly_name> is omitted, all assemblies will be extracted.");
		Options.Add ("If the glob pattern contains any path segments, they will be ignored. The `**` pattern will be ignored as well.");
		Options.Add ("Glob patterns, therefore, should contain only the `*` and `?` components.");
		AddArchNamesForHelp ();
	}

	protected override int DoInvoke (List<string> rest)
	{
		IAspect? aspect = GetAspect (rest[0]);
		if (aspect == null) {
			return 1;
		}

		string targetDir;
		if (String.IsNullOrEmpty (outputDir)) {
			targetDir = Path.Combine (Environment.CurrentDirectory, AssembliesOutputDirDefault);
		} else {
			targetDir = Path.GetFullPath (outputDir);
		}

		string archList;
		if (String.IsNullOrEmpty (architectures)) {
			archList = "all";
		} else {
			archList = architectures;
		}

		var assemblyPatterns = rest.Count switch {
			1 => null,
			_ => new List<string> (rest.Slice (1, rest.Count - 1))
		};

		var options = new AssemblyExtractorOptions (targetDir, assemblyPatterns) {
			Architectures = ArchitectureName.ParseList (archList),
			ExtractPDB = extractPdb,
			NoDecompress = noDecompress,
			UseRegex = useRegex,
		};

		return Extractor.ExtractMultiple<ApplicationAssembly, AssemblyExtractorOptions> (aspect, targetDir, options) ? 0 : 1;
	}
}

/// <summary>
/// CLI command that extracts the Android manifest as plain XML from an application package.
/// </summary>
class ExtractManifestCommand : BaseProgramCommand
{
	const string ManifestOutputDefault = "AndroidManifest.xml";

	string? outputFile;

	public ExtractManifestCommand ()
		: base ("manifest", "Extract manifest as plain XML from the application package")
	{
		Options.Add ("output|o=", $"Write output to file `VALUE`; Defaults to '{ManifestOutputDefault}'", v => outputFile = v);
	}

	protected override int DoInvoke (List<string> rest)
	{
		IAspect? aspect = GetAspect (rest[0]);
		if (aspect == null) {
			return 1;
		}

		return Extractor.Extract<AndroidManifest> (aspect, outputFile ?? ManifestOutputDefault) ? 0 : 1;
	}
}
