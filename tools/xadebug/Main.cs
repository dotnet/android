using System;
using System.Collections.Generic;
using System.IO;

using Mono.Options;
using Xamarin.Android.Utilities;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Debug;

class XADebug
{
	sealed class ParsedOptions
	{
		public bool ShowHelp;
		public bool Verbose = true; // TODO: remove the default once development is done
		public string Configuration = "Debug";
		public string? PackageName;
	}

	static XamarinLoggingHelper log = new XamarinLoggingHelper ();

	static int Main (string[] args)
	{
		bool haveOptionErrors = false;
		var parsedOptions = new ParsedOptions ();
		log.Verbose = parsedOptions.Verbose;

		var opts = new OptionSet {
			"Usage: dotnet xadebug [OPTIONS] <PROJECT_DIRECTORY_PATH | APPLICATION_APK_PATH>",
			"",
			{ "p|package-name=", "name of the application package", v => parsedOptions.PackageName = EnsureNonEmptyString (log, "-p|--package-name", v, ref haveOptionErrors) },
			{ "c|configuration=", "{CONFIGURATION} in which to build the application. Ignored when running in APK-only mode", v => parsedOptions.Configuration = v },
			"",
			{ "v|verbose", "Show debug messages", v => parsedOptions.Verbose = true },
			{ "h|help|?", "Show this help screen", v => parsedOptions.ShowHelp = true },
		};

		List<string> rest = opts.Parse (args);
		log.Verbose = parsedOptions.Verbose;

		if (parsedOptions.ShowHelp || rest.Count == 0) {
			int ret = 0;
			if (rest.Count == 0) {
				log.ErrorLine ("Path to application APK or directory with a C# project must be specified");
				log.ErrorLine ();
				ret = 1;
			}

			opts.WriteOptionDescriptions (Console.Out);
			return ret;
		}

		if (haveOptionErrors) {
			return 1;
		}

		string? apkFilePath = null;
		ZipArchive? apk = null;

		if (Directory.Exists (rest[0])) {
			// TODO: build app in this directory and set apkFilePath appropriately
			throw new NotImplementedException ("Building the application is not implemented yet");
		} else if (File.Exists (rest[0])) {
			if (!IsAndroidPackageFile (rest[0], out apk)) {
				log.ErrorLine ($"File '{rest[0]}' is not an Android APK package");
				log.ErrorLine ();
			} else {
				apkFilePath = rest[0];
			}
		} else {
			log.ErrorLine ($"Neither directory nor file '{rest[0]}' exist");
			log.ErrorLine ();
		}

		if (String.IsNullOrEmpty (apkFilePath)) {
			return 1;
		}

		return 0;
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

	static bool IsAndroidPackageFile (string filePath, out ZipArchive? apk)
	{
		try {
			apk = ZipArchive.Open (filePath, FileMode.Open);
		} catch (ZipIOException ex) {
			log.DebugLine ($"Failed to open '{filePath}' as ZIP archive: {ex.Message}");
			apk = null;
			return false;
		}

		return apk.ContainsEntry ("AndroidManifest.xml");
	}
}
