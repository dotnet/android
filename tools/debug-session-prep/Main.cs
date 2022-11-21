using System;
using System.Collections.Generic;

using Mono.Options;
using Xamarin.Android.Utilities;

namespace Xamarin.Debug.Session.Prep;

class App
{
	sealed class ParsedOptions
	{
		public string?   AdbPath = "adb";
		public string?   PackageName;
		public string[]? SupportedABIs;
		public string?   TargetDevice;
		public bool      ShowHelp;
		public bool      Verbose;
	}

	static int Main (string[] args)
	{
		bool haveOptionErrors = false;
		var log = new XamarinLoggingHelper ();
		var parsedOptions = new ParsedOptions ();

		var opts = new OptionSet {
			"Usage: debug-session-prep [REQUIRED_OPTIONS] [OPTIONS]",
			"",
			"REQUIRED_OPTIONS are:",
			{ "p|package-name=", "name of the application package", v => parsedOptions.PackageName = EnsureNonEmptyString (log, "p|package-name", v, ref haveOptionErrors) },
			{ "s|supported-abis=", "comma-separated list of ABIs the application supports", v => parsedOptions.SupportedABIs = EnsureSupportedABIs (log, "s|supported-abis", v, ref haveOptionErrors) },
			"",
			"OPTIONS are:",
			{ "a|adb=", "{PATH} to adb to use for this session", v => parsedOptions.AdbPath = EnsureNonEmptyString (log, "a|adb", v, ref haveOptionErrors) },
			{ "d|device=", "ID of {DEVICE} to target for this session", v => parsedOptions.TargetDevice = EnsureNonEmptyString (log, "d|device", v, ref haveOptionErrors) },
			"",
			{ "v|verbose", "Show debug messages", v => parsedOptions.Verbose = true },
			{ "h|help|?", "Show this help screen", v => parsedOptions.ShowHelp = true },
		};

		List<string> rest = opts.Parse (args);

		if (parsedOptions.ShowHelp) {
			opts.WriteOptionDescriptions (Console.Out);
			return 0;
		}

		if (haveOptionErrors) {
			return 1;
		}

		return 0;
	}

	static string[]? EnsureSupportedABIs (XamarinLoggingHelper log, string paramName, string? value, ref bool haveOptionErrors)
	{
		string? abis = EnsureNonEmptyString (log, paramName, value, ref haveOptionErrors);
		if (abis == null) {
			return null;
		}

		var list = new List<string> ();
		foreach (string s in abis.Split (',')) {
			string? abi = s?.Trim ();
			if (String.IsNullOrEmpty (abi)) {
				continue;
			}

			list.Add (abi);
		}

		return list.ToArray ();
	}

	static string? EnsureNonEmptyString (XamarinLoggingHelper log, string paramName, string? value, ref bool haveOptionErrors)
	{
		if (String.IsNullOrEmpty (value)) {
			haveOptionErrors = true;
			log.ErrorLine ($"Parameter '{paramName}' requires a non-empty string as its value");
			return null;
		}

		return value;
	}
}
