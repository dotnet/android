using System;
using System.Collections.Generic;

using Mono.Options;

using Xamarin.Android.Application;

namespace Xamarin.XApp;

sealed class ParsedOptions
{
	public bool TypeMaps;
	public bool AssemblyStore;
	public bool ExtractAssemblies;
}

class App
{
	static int Main (string[] args)
	{
		var parsedOptions = new ParsedOptions ();

		var opts = new OptionSet {
			"Usage: xapp [OPTIONS] <path/to/file> [path/to/file ..]",
			"",
			"OPTIONS are:",
			"",
			{ "t|typemaps", "Show detailed typemap information", v => parsedOptions.TypeMaps = true },
			{ "s|assembly-store", "Show detailed assembly store information", v => parsedOptions.AssemblyStore = true },
			{ "e|extract-assemblies", "Extract assemblies from the APK/AAB archive or assembly store blob", v => parsedOptions.ExtractAssemblies = true },
		};

		List<string> rest= opts.Parse (args);

		foreach (string inputFile in rest) {
			InputReader? reader = InputType.Detect (inputFile);
			if (reader == null) {
				// TODO: proper logging
				Console.WriteLine ($"Input file '{inputFile}' is not of supported type.");
				continue;
			}

			Console.WriteLine ($"Got reader '{reader}' for file '{inputFile}'");
			PrintAssemblyStoreInfo (reader);
		}

		return 0;
	}

	static void PrintAssemblyStoreInfo (InputReader reader)
	{
		if (!reader.SupportsAssemblyStore) {
			return;
		}

		DataProviderAssemblyStore? assemblyStore = reader.GetAssemblyStore ();
		if (assemblyStore == null) {
			return;
		}

		Console.WriteLine ("Assembly store info:");
		Console.WriteLine ($"  Version: {assemblyStore.Version}");
		Console.WriteLine ($"       ID: {assemblyStore.ID}");
		if (assemblyStore.IsArchSpecific) {
			Console.WriteLine ($"  Specific to architecture: {ArchitectureName (assemblyStore)}");
		}
		Console.WriteLine ($"  Local entry count: {assemblyStore.LocalEntryCount}");
		Console.WriteLine ($"  Global entry count: {assemblyStore.GlobalEntryCount}");
		Console.WriteLine ();
	}

	static string ArchitectureName (DataProviderAssemblyStore store)
	{
		return String.IsNullOrEmpty (store.DetectedArchitecture) ? "yes, undetermined" : store.DetectedArchitecture;
	}
}
