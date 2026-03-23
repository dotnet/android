using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace ApplicationUtility;

/// <summary>
/// Base class for all CLI commands in the application utility. Provides common option parsing
/// (help, no-color) and shared functionality for command implementations.
/// </summary>
abstract class BaseProgramCommand : Command
{
	protected bool ShowHelp { get; private set; }
	protected bool NoColor { get; private set; }

	/// <summary>
	/// Initializes a new command with the given name and description, and registers the shared help and no-color options.
	/// </summary>
	/// <param name="name">The command name as typed on the command line.</param>
	/// <param name="commandDescription">A description of what the command does.</param>
	/// <param name="help">Optional help text; defaults to <paramref name="commandDescription"/>.</param>
	/// <param name="synopsis">Optional usage synopsis for the help output.</param>
	protected BaseProgramCommand (string name, string commandDescription, string? help = null, string? synopsis = null)
		: base (name, help ?? commandDescription)
	{
		Options = new OptionSet {
			synopsis ?? $"{name} [OPTIONS]",
			"",
			commandDescription,
			"",
			{ "help|h|?", "Show command help.", v => ShowHelp = v != null },
			{ "no-color", "Do not colorize messages when outputting to the screen.", v => NoColor = v != null },
		};
	}

	/// <summary>
	/// Parses arguments, displays help if requested, and delegates to <see cref="DoInvoke"/>.
	/// </summary>
	/// <param name="args">Command-line arguments passed to this command.</param>
	/// <returns>Exit code: 0 for success, non-zero for failure.</returns>
	public override int Invoke (IEnumerable<string> args)
	{
		List<string> rest = Options.Parse (args);

		if (ShowHelp) {
			Options.WriteOptionDescriptions (CommandSet?.Out ?? Console.Error);
			return 0;
		}

		if (rest == null || rest.Count == 0) {
			Log.Error ($"Command '{Name}' requires at least a single input file.");
			return 1;
		}

		return DoInvoke (rest);
	}

	/// <summary>
	/// Attempts to detect and load the application aspect from the given file path.
	/// </summary>
	/// <param name="filePath">Path to the input file.</param>
	/// <returns>The detected <see cref="IAspect"/>, or <c>null</c> if the file does not exist or is not recognized.</returns>
	protected IAspect? GetAspect (string filePath)
	{
		if (!File.Exists (filePath)) {
			Log.Error ($"Input file '{filePath}' does not exist");
			return null;
		}

		return Detector.FindAspect (filePath);
	}

	/// <summary>
	/// Appends supported native architecture names to the options help text.
	/// </summary>
	protected void AddArchNamesForHelp ()
	{
		IDictionary<NativeArchitecture, string> names = ArchitectureName.GetSupportedNames ();

		Options.Add ("");
		Options.Add ("Supported native architecture names (case-insensitive):");
		Options.Add ("  * all");
		foreach (var kvp in names) {
			Options.Add ($"  * {kvp.Key}: {kvp.Value}");
		}
		Options.Add ("");
	}

	/// <summary>
	/// Implements the actual command logic. Called after argument parsing.
	/// </summary>
	/// <param name="rest">Positional arguments remaining after option parsing.</param>
	/// <returns>Exit code: 0 for success, non-zero for failure.</returns>
	protected abstract int DoInvoke (List<string> rest);
}
