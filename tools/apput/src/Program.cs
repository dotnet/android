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
	string? architectures;

	public ExtractAssemblyCommand ()
		: base ("assembly", "Extract assemblies from the application package", synopsis: "assembly [OPTIONS] <assembly_name> [<assembly_name>]")
	{
		Options.Add ("output|o=", $"Write output to directory `VALUE`; Defaults to '{AssembliesOutputDirDefault}'", v => outputDir = v);
		Options.Add ("a|arch=", "Extract only entries for the architectures specified by `VALUE`, a comma-separated list of architecture names. Defaults to 'all'", v => architectures = v);
		Options.Add ("r", "Treat each <assembly_name> as a regular expression.", v => useRegex = v != null);
		Options.Add ("d", "Extract also the accompanying PDB file, if found.", v => extractPdb = v != null);
		Options.Add ("");
		Options.Add ("<assembly_name> does not have to specify the extension. By default assembly names are treated as standard glob patterns.");
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

		var options = new AssemblyExtractorOptions (targetDir) {
			UseRegex = useRegex,
			ExtractPDB = extractPdb,
			Architectures = ArchitectureName.ParseList (archList),
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
