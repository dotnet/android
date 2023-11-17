using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.AssemblyStore;

class Program
{
	static void ShowHelp ()
	{
		Console.WriteLine ("Usage: read-assembly-store BLOB_PATH [BLOB_PATH ...]");
		Console.WriteLine ();
		Console.WriteLine (@"  where each BLOB_PATH can point to:
    * aab file
    * apk file
    * index store file (e.g. base_assemblies.blob or assemblies.arm64_v8a.blob.so)
    * arch store file (e.g. base_assemblies.arm64_v8a.blob)
    * store manifest file (e.g. base_assemblies.manifest)
    * store base name (e.g. base or base_assemblies)

  In each case the whole set of stores and manifests will be read (if available). Search for the
  various members of the store set (common/main store, arch stores, manifest) is based on this naming
  convention:

     {BASE_NAME}[.ARCH_NAME].{blob|manifest}

  Whichever file is referenced in `BLOB_PATH`, the BASE_NAME component is extracted and all the found files are read.
  If `BLOB_PATH` points to an aab or an apk, BASE_NAME will always be `assemblies`

");
	}

	static int WriteErrorAndReturn (string message)
	{
		Console.Error.WriteLine (message);
		return 1;
	}

	static int Main (string[] args)
	{
		if (args.Length == 0) {
			ShowHelp ();
			return 1;
		}

		string inputFile = args[0];
		(FileFormat format, FileInfo? info) = Utils.DetectFileFormat (inputFile);
		if (info == null) {
			return WriteErrorAndReturn ($"File '{inputFile}' does not exist.");
		}

		(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (inputFile);
		if (explorers == null) {
			return WriteErrorAndReturn (errorMessage ?? "Unknown error");
		}

		foreach (AssemblyStoreExplorer store in explorers) {
			var printer = new StorePrettyPrinter (store);
			printer.Show ();
		}

		return 0;
	}


}
