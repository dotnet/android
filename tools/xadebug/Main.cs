using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

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

	const string AndroidManifestZipPath = "AndroidManifest.xml";

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

		string aPath = rest[0];
		string? apkFilePath = null;
		ZipArchive? apk = null;

		if (Directory.Exists (aPath)) {
			apkFilePath = BuildApp (aPath);
		} else if (File.Exists (aPath)) {
			if (String.Compare (".csproj", Path.GetExtension (aPath), StringComparison.OrdinalIgnoreCase) == 0) {
				// Let's see if we can trust the file name...
				apkFilePath = BuildApp (aPath);
			} else if (IsAndroidPackageFile (aPath, out apk)) {
				apkFilePath = aPath;
			} else {
				log.ErrorLine ($"File '{aPath}' is not an Android APK package");
				log.ErrorLine ();
			}
		} else {
			log.ErrorLine ($"Neither directory nor file '{aPath}' exist");
			log.ErrorLine ();
		}

		if (String.IsNullOrEmpty (apkFilePath)) {
			return 1;
		}

		if (apk == null) {
			apk = OpenApk (apkFilePath);
		}

		// Extract app information fromn the embedded manifest
		ApplicationInfo appInfo = ReadManifest (apk);

		return 0;
	}

	static ApplicationInfo ReadManifest (ZipArchive apk)
	{
		ZipEntry entry = apk.ReadEntry (AndroidManifestZipPath);

		using var manifestData = new MemoryStream ();
		entry.Extract (manifestData);
		manifestData.Seek (0, SeekOrigin.Begin);

		// TODO: make provisions for plain XML AndroidManifest.xml, perhaps? Although not sure if it's really necesary these days anymore as the APKs should all have the
		// binary version of the manifest.
		var axml = new AXMLParser (manifestData, log);
		XmlDocument? manifest = axml.Parse ();

		string packageName = String.Empty;

		return new ApplicationInfo (packageName);
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

	static ZipArchive OpenApk (string filePath) => ZipArchive.Open (filePath, FileMode.Open);

	static bool IsAndroidPackageFile (string filePath, out ZipArchive? apk)
	{
		try {
			apk = OpenApk (filePath);
		} catch (ZipIOException ex) {
			log.DebugLine ($"Failed to open '{filePath}' as ZIP archive: {ex.Message}");
			apk = null;
			return false;
		}

		return apk.ContainsEntry (AndroidManifestZipPath);
	}

	static string BuildApp (string projectPath)
	{
		throw new NotImplementedException ("Building the application is not implemented yet");
	}
}
