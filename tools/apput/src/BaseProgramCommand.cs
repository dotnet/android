using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace ApplicationUtility;

abstract class BaseProgramCommand : Command
{
	protected bool ShowHelp { get; private set; }
	protected bool NoColor { get; private set; }

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

	public override int Invoke (IEnumerable<string> args)
	{
		List<string> rest = Options.Parse (args);

		if (ShowHelp) {
			Options.WriteOptionDescriptions (CommandSet.Out);
			return 0;
		}

		if (rest.Count == 0) {
			Log.Error ($"Command '{Name}' requires at least a single input file.");
			return 1;
		}

		DoInvoke (rest);
		return 0;
	}

	protected IAspect? GetAspect (string filePath)
	{
		if (!File.Exists (filePath)) {
			Log.Error ($"Input file '{filePath}' does not exist");
			return null;
		}

		return Detector.FindAspect (filePath);
	}

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

	protected abstract int DoInvoke (List<string> rest);
}
