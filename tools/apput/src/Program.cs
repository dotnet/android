using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace ApplicationUtility;

// TODO: add support for individual assemblies as payload in shared libraries
class Program
{
	const string AppName = "apput";

	static int Main (string[] args)
	{
		Log.SetVerbose (true);
		try {
			return Run (args);
		} catch (Exception ex) {
			Log.ExceptionError ("Unhandled exception", ex);
			return 1;
		} finally {
			TempFileManager.Cleanup ();
		}
	}

	static int Run (string[] args)
	{
		bool showHelp = false;

		var commands = new CommandSet (AppName) {
			$"usage: {AppName} COMMAND <input_file>",
			"",
			".NET for Android application analysis utility.",
			"",
			"Global options:",
			{ "l=", $"Log level. One of: ", (string? l) => throw new NotImplementedException () },
			{ "help|h|?", "Show this message and exit.", v => showHelp = v != null },
			"",
			"Available commands (all commands support the `--help` parameter):",
			new ReportCommand (),
			new SummaryCommand (),
			new CommandSet ("extract") {
				new ExtractAssemblyCommand (),
				new ExtractManifestCommand (),
			},
			new ListCommand (),
		};

		// TODO: figure out a way to implement a default command without having to modify Mono.Options
		return commands.Run (args);
	}
}

class ReportCommand : BaseProgramCommand
{
	public ReportCommand ()
		: base ("report", "Generate a full report on <input_file>")
	{}

	protected override int DoInvoke (List<string> rest)
	{
		// We're invoked only if `rest` has at least one element.
		IAspect? aspect = GetAspect (rest[0]);
		if (aspect == null) {
			return 1;
		}

		Reporter.Report (aspect, plainTextRendering: NoColor == true);
		return 0;
	}
}

class SummaryCommand : BaseProgramCommand
{
	public SummaryCommand ()
		: base ("summary", "Show a short summary of the application aspect passed on the command line.")
	{}

	protected override int DoInvoke (List<string> rest)
	{
		throw new NotImplementedException ();
	}
}

class ListCommand : BaseProgramCommand
{
	public ListCommand ()
		: base ("list", "List selected components of the application package")
	{}

	protected override int DoInvoke (List<string> rest)
	{
		throw new NotImplementedException ();
	}
}

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
