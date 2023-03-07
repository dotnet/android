using System;
using System.Collections.Generic;

using Mono.Options;

using Xamarin.Android.Application;
using Xamarin.Android.AssemblyStore;

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

		assemblyStore.EnsureFullAssemblyInformation ();

		Console.WriteLine ("Assembly store info:");
		Console.WriteLine ($"  Number of read blobs: {assemblyStore.Blobs.Count}");
		Console.WriteLine ($"  Manifest present: {YesNo (assemblyStore.Manifest != null)}");
		Console.WriteLine ();
		Console.WriteLine ("Individual blob information:");

		foreach (AssemblyStoreReader blob in assemblyStore.Blobs) {
			Console.WriteLine ($"       ID: {blob.StoreID}");
			Console.WriteLine ($"  Version: {blob.Version}");
			if (blob.IsArchSpecific) {
				Console.WriteLine ($"  Specific to architecture: {ArchitectureName (blob)}");
			}
			Console.WriteLine ($"  Local entry count: {blob.LocalEntryCount}");
			Console.WriteLine ($"  Global entry count: {blob.GlobalEntryCount}");
			Console.WriteLine ("  Assemblies in this blob:");
			foreach (AssemblyStoreAssembly asm in blob.Assemblies) {
				string not = asm.IsCompressed ? String.Empty : "not ";
				Console.WriteLine ($"    {asm.Name} ({not}compressed)");
			}
			Console.WriteLine ();
		}
	}

	static string YesNo (bool yes) => yes ? "yes" : "no";

	static string ArchitectureName (AssemblyStoreReader blob)
	{
		return String.IsNullOrEmpty (blob.Arch) ? "yes, undetermined" : blob.Arch;
	}
}
