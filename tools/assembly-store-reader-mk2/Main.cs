using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Options;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

class App
{
	static void ShowHelp ()
	{
	}

	static int WriteErrorAndReturn (string message)
	{
		Console.Error.WriteLine (message);
		return 1;
	}

	static HashSet<AndroidTargetArch>? ParseArchList (string values)
	{
		if (String.IsNullOrEmpty (values)) {
			return null;
		}

		var ret = new HashSet<AndroidTargetArch> ();
		foreach (string a in values.Split (',')) {
			string archName = a.Trim ();
			if (Enum.TryParse (archName, out AndroidTargetArch arch)) {
				ret.Add (arch);
				continue;
			}

			arch = archName.ToLowerInvariant () switch {
				"aarch64" => AndroidTargetArch.Arm64,
				"arm32"   => AndroidTargetArch.Arm,
				"arm64"   => AndroidTargetArch.Arm64,
				"armv7a"  => AndroidTargetArch.Arm,
				"armv8a"  => AndroidTargetArch.Arm64,
				"x64"     => AndroidTargetArch.X86_64,
				_ => throw new InvalidOperationException ($"Unknown architecture name '{archName}'")
			};
			ret.Add (arch);
		}

		return ret;
	}

	static string GetArchNames ()
	{
		return String.Join (", ", MonoAndroidHelper.SupportedTargetArchitectures.Select (a => a.ToString ().ToLowerInvariant ()));
	}

	static int Main (string[] args)
	{
		HashSet<AndroidTargetArch>? arches = null;
		bool showHelp = false;

		var options = new OptionSet {
			"Usage: read-assembly-store [OPTIONS] BLOB_PATH",
			"",
			"  where each BLOB_PATH can point to:",
			"    * aab file",
			"    * apk file",
			"    * index store file (e.g. base_assemblies.blob or assemblies.arm64_v8a.blob.so)",
			"    * arch store file (e.g. base_assemblies.arm64_v8a.blob)",
			"    * store manifest file (e.g. base_assemblies.manifest)",
			"    * store base name (e.g. base or base_assemblies)",
			"",
			"  In each case the whole set of stores and manifests will be read (if available). Search for the",
			"  various members of the store set (common/main store, arch stores, manifest) is based on this naming",
			"  convention:",
			"",
			"     {BASE_NAME}[.ARCH_NAME].{blob|blob.so|manifest}",
			"",
			"  Whichever file is referenced in `BLOB_PATH`, the BASE_NAME component is extracted and all the found files are read.",
			"  If `BLOB_PATH` points to an aab or an apk, BASE_NAME will always be `assemblies`",
			"",
			{"a|arch=", $"Limit listing of assemblies to these {{ARCHITECTURES}} only.  A comma-separated list of one or more of: {GetArchNames ()}", v => arches = ParseArchList (v) },
			"",
			{"?|h|help", "Show this help screen", v => showHelp = true},
		};

		List<string>? theRest = options.Parse (args);
		if (theRest == null || theRest.Count == 0 || showHelp) {
			options.WriteOptionDescriptions (Console.Out);
			return showHelp ? 0 : 1;
		}

		string inputFile = theRest[0];
		(FileFormat format, FileInfo? info) = Utils.DetectFileFormat (inputFile);
		if (info == null) {
			return WriteErrorAndReturn ($"File '{inputFile}' does not exist.");
		}

		(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (inputFile);
		if (explorers == null) {
			return WriteErrorAndReturn (errorMessage ?? "Unknown error");
		}

		foreach (AssemblyStoreExplorer store in explorers) {
			if (arches != null && store.TargetArch.HasValue && !arches.Contains (store.TargetArch.Value)) {
				continue;
			}

			var printer = new StorePrettyPrinter (store);
			printer.Show ();
		}

		return 0;
	}


}
