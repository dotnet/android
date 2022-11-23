using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Options;
using Xamarin.Android.Utilities;

namespace Xamarin.Debug.Session.Prep;

class App
{
	const string DefaultAdbPath = "adb";

	static readonly Dictionary<string, string> SupportedAbiMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
		{"arm32",       "armeabi-v7a"},
		{"arm64",       "arm64-v8a"},
		{"arm64-v8a",   "arm64-v8a"},
		{"armeabi",     "armeabi-v7a"},
		{"armeabi-v7a", "armeabi-v7a"},
		{"x86",         "x86"},
		{"x86_64",      "x86_64"},
		{"x64",         "x86_64"}
	};

	sealed class ParsedOptions
	{
		public string?   AdbPath;
		public string?   PackageName;
		public string[]? SupportedABIs;
		public string?   TargetDevice;
		public bool      ShowHelp;
		public bool      Verbose = true; // TODO: remove the default once development is done
		public string?   AppNativeLibrariesDir;
		public string?   NdkDirPath;
		public string?   OutputDirPath;
	}

	static int Main (string[] args)
	{
		bool haveOptionErrors = false;
		var parsedOptions = new ParsedOptions ();
		var log = new XamarinLoggingHelper {
			Verbose = parsedOptions.Verbose,
		};

		var opts = new OptionSet {
			"Usage: debug-session-prep [REQUIRED_OPTIONS] [OPTIONS]",
			"",
			"REQUIRED_OPTIONS are:",
			{ "p|package-name=", "name of the application package", v => parsedOptions.PackageName = EnsureNonEmptyString (log, "-p|--package-name", v, ref haveOptionErrors) },
			{ "s|supported-abis=", "comma-separated list of ABIs the application supports", v => parsedOptions.SupportedABIs = EnsureSupportedABIs (log, "-s|--supported-abis", v, ref haveOptionErrors) },
			{ "l|lib-dir=", "{PATH} to the directory where application native libraries were copied", v => parsedOptions.AppNativeLibrariesDir = v },
			{ "n|ndk-dir=", "{PATH} to to the Android NDK root directory", v => parsedOptions.NdkDirPath = v },
			{ "o|output-dir=", "{PATH} to directory which will contain various generated files (logs, scripts etc)", v => parsedOptions.OutputDirPath = v },
			"",
			"OPTIONS are:",
			{ "a|adb=", "{PATH} to adb to use for this session", v => parsedOptions.AdbPath = EnsureNonEmptyString (log, "-a|--adb", v, ref haveOptionErrors) },
			{ "d|device=", "ID of {DEVICE} to target for this session", v => parsedOptions.TargetDevice = EnsureNonEmptyString (log, "-d|--device", v, ref haveOptionErrors) },
			"",
			{ "v|verbose", "Show debug messages", v => parsedOptions.Verbose = true },
			{ "h|help|?", "Show this help screen", v => parsedOptions.ShowHelp = true },
			"",
			$"Supported ABI names are: {GetSupportedAbiNames ()}",
			"",
		};

		List<string> rest = opts.Parse (args);
		log.Verbose = parsedOptions.Verbose;

		if (parsedOptions.ShowHelp) {
			opts.WriteOptionDescriptions (Console.Out);
			return 0;
		}

		if (haveOptionErrors) {
			return 1;
		}

		bool missingRequiredOptions = false;
		if (parsedOptions.SupportedABIs == null || parsedOptions.SupportedABIs.Length == 0) {
			log.ErrorLine ("The '-s|--supported-abis' option must be used to provide a non-empty list of Android ABIs supported by the application");
			missingRequiredOptions = true;
		}

		if (String.IsNullOrEmpty (parsedOptions.PackageName)) {
			log.ErrorLine ("The '-p|--package-name' option must be used to provide non-empty application package name");
			missingRequiredOptions = true;
		}

		if (String.IsNullOrEmpty (parsedOptions.NdkDirPath)) {
			log.ErrorLine ("The '-n|--ndk-dir' option must be used to specify the directory where Android NDK is installed");
			missingRequiredOptions = true;
		}

		if (String.IsNullOrEmpty (parsedOptions.OutputDirPath)) {
			log.ErrorLine ("The '-o|--output-dir' option must be used to specify the directory where generated files will be placed");
			missingRequiredOptions = true;
		}

		if (String.IsNullOrEmpty (parsedOptions.AppNativeLibrariesDir)) {
			log.ErrorLine ("The '-l|--lib-dir' option must be used to specify the directory where application shared libraries were copied");
			// missingRequiredOptions = true;
		}

		if (missingRequiredOptions) {
			return 1;
		}

		var ndk = new AndroidNdk (log, parsedOptions.NdkDirPath!, parsedOptions.SupportedABIs!);
		var device = new AndroidDevice (
			log,
			ndk,
			parsedOptions.OutputDirPath!,
			parsedOptions.AdbPath ?? DefaultAdbPath,
			parsedOptions.PackageName!,
			parsedOptions.SupportedABIs!,
			parsedOptions.TargetDevice
		);

		if (!device.GatherInfo ()) {
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

		bool haveInvalidAbis = false;
		var list = new List<string> ();
		foreach (string s in abis.Split (',')) {
			string? abi = s?.Trim ();
			if (String.IsNullOrEmpty (abi)) {
				continue;
			}

			if (!SupportedAbiMap.TryGetValue (abi, out string? mappedAbi) || String.IsNullOrEmpty (mappedAbi)) {
				log.ErrorLine ($"Unsupported ABI: {abi}");
				haveInvalidAbis = true;
			}

			list.Add (mappedAbi!);
		}

		if (haveInvalidAbis) {
			return null;
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

	static string GetSupportedAbiNames () => String.Join (", ", SupportedAbiMap.Keys);
}
