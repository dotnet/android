using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Utilities;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Debug;

class DebugSession
{
	static readonly Dictionary<string, string> KnownAbiDirs = new Dictionary<string, string> (StringComparer.Ordinal) {
		{ "lib/arm64-v8a", "arm64-v8a" },
		{ "lib/armeabi-v7a", "armeabi-v7a" },
		{ "lib/x86_64", "x86_64" },
		{ "lib/x86", "x86" },
	};

	readonly string apkPath;
	readonly ParsedOptions parsedOptions;
	readonly XamarinLoggingHelper log;
	readonly ZipArchive apk;
	readonly string workDirectory;

	public DebugSession (XamarinLoggingHelper logger, string apkPath, ZipArchive apk, ParsedOptions parsedOptions)
	{
		log = logger;
		this.apkPath = apkPath;
		this.parsedOptions = parsedOptions;
		this.apk = apk;
		workDirectory = Path.Combine (parsedOptions.WorkDirectory, Utilities.StringHash (apkPath));
    }

	public bool Prepare ()
	{
		List<string> supportedAbis = DetectSupportedAbis ();
		if (supportedAbis.Count == 0) {
			log.ErrorLine ("Unable to detect ABIs supported by the application");
			return false;
		}

		var ndk = new AndroidNdk (log, parsedOptions.NdkDirPath!, supportedAbis);
		var device = new AndroidDevice (
			log,
			ndk,
			workDirectory,
			parsedOptions.AdbPath,
			parsedOptions.PackageName!,
			supportedAbis,
			parsedOptions.TargetDevice
		);

		if (!device.GatherInfo ()) {
			return false;
		}

		return false;
	}

	List<string> DetectSupportedAbis ()
	{
		var ret = new List<string> ();

		log.DebugLine ($"Detecting ABIs supported by '{apkPath}'");
		HashSet<string> seenAbis = new HashSet<string> (StringComparer.Ordinal);
		foreach (ZipEntry entry in apk) {
			if (seenAbis.Count == KnownAbiDirs.Count) {
				break;
			}

			// There might not be separate entries for lib/{ARCH} directories, so we look for the first file
			// inside one of them to determine if an ABI is supported
			string entryDir = Path.GetDirectoryName (entry.FullName) ?? String.Empty;
			if (!KnownAbiDirs.TryGetValue (entryDir, out string? abi) || seenAbis.Contains (abi)) {
				continue;
			}

			seenAbis.Add (abi);
			ret.Add (abi);
		}

		if (ret.Count > 0) {
			log.StatusLine ("Supported ABIs", String.Join (", ", ret));
		}

		return ret;
	}
}
