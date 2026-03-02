using System;
using System.Collections.Generic;
using Mono.Options;

namespace ApplicationUtility;

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
			"Available commands:",
			new ReportCommand (),
			new ExtractCommand (),
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

class ExtractCommand : BaseProgramCommand
{
	public ExtractCommand ()
		: base ("extract", "Extract selected components from the application package")
	{}

	protected override int DoInvoke (List<string> rest)
	{
		throw new NotImplementedException ();
	}
}
