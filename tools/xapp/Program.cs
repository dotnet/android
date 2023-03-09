using System;
using System.Collections.Generic;

using Mono.Options;

using Xamarin.Android.Application;
using Xamarin.Android.Application.Utilities;
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
	static XamarinLoggingHelper log;

	static int Main (string[] args)
	{
		log = new XamarinLoggingHelper {
			Verbose = true,
		};

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
			InputReader? reader = InputType.Detect (inputFile, log);
			if (reader == null) {
				// TODO: proper logging
				log.WarningLine ($"Input file '{inputFile}' is not of supported type.");
				continue;
			}

			GenerateInfo (reader, inputFile);
		}

		return 0;
	}

	static void GenerateInfo (InputReader reader, string inputFile)
	{
		log.DebugLine ($"Got reader '{reader}' for file '{inputFile}'");
		PrintAssemblyStoreInfo (reader);

		if (reader.SupportsAppInfo) {
			PrintAppInfo (reader.GetAppInfo ());
		}
	}

	static void PrintAppInfo (DataProviderAppInfo? appInfo)
	{
		if (appInfo == null) {
			return;
		}

		// TODO: Validation
		//  - Assembly stores: number of blobs must be number of supported ABIs (if abi-specific blobs count is != 0)
		//  - Assembly stores: must have a manifest
		//  - Assembly stores: total number of stores must be at least 1
		//  - Assembly stores: validate number of assemblies - must match the manifest, all ABI-specific stores must have the same number of assemblies
		//  - Supported ABIs must be at least 1

		log.InfoLine ();
		log.MessageLine ("Application info:");
		log.StatusLine ("  Package name", appInfo.PackageName);
		log.StatusLine ("  Archive type", appInfo.ArchiveType);
		log.StatusYesNoLine ($"  Signed", appInfo.IsSigned);

		string supportedAbis;
		if (appInfo.SupportedAbis.Length == 0) {
			supportedAbis = "none";
		} else {
			supportedAbis = String.Join (", ", appInfo.SupportedAbis);
		}
		log.StatusLine ($"  Supported ABIs", supportedAbis);

		log.StatusYesNoLine ($"  Uses assembly stores", appInfo.UsesAssemblyStores);
		if (appInfo.UsesAssemblyStores) {
			log.StatusYesNoLine ($"  Assembly stores manifest present", appInfo.HasAssemblyStoresManifest);
			log.StatusLine ($"  Total number of assembly store blobs", appInfo.TotalNumberOfAssemblyStores);
			log.StatusLine ($"  Number of ABI-specific assembly store blobs", appInfo.NumberOfAbiAssemblyStores);
		}

		log.StatusYesNoLine ($"  Is a classic XA app", appInfo.IsClassicXA);
		log.StatusYesNoLine ($"  Contains runtime config blob", appInfo.HasRuntimeConfigBlob);
		log.StatusYesNoLine ($"  Debuggable", appInfo.IsDebug);
		log.StatusYesNoLine ($"  Profileable", appInfo.IsProfileable);
		log.StatusYesNoLine ($"  Test build", appInfo.IsTesting);
		log.StatusYesNoLine ($"  Uses AOT", appInfo.UsesAOT);
		log.StatusYesNoLine ($"  Uses MAUI", appInfo.UsesMAUI);
		log.StatusYesNoLine ($"  Uses Xamarin.Forms", appInfo.UsesXamarinForms);
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

		log.MessageLine ("Assembly store info:");
		log.StatusLine ("  Number of read blobs", assemblyStore.Blobs.Count);
		log.StatusYesNoLine ("  Manifest present", assemblyStore.Manifest != null);
		log.MessageLine ();
		log.MessageLine ("Individual blob information:");

		foreach (AssemblyStoreReader blob in assemblyStore.Blobs) {
			log.StatusLine ("       ID", blob.StoreID);
			log.StatusLine ("  Version", blob.Version);
			if (blob.IsArchSpecific) {
				log.StatusLine ($"  Specific to architecture", ArchitectureName (blob));
			}
			log.StatusLine ("  Local entry count", blob.LocalEntryCount);
			log.StatusLine ("  Global entry count", blob.GlobalEntryCount);
			log.MessageLine ("  Assemblies in this blob:");
			foreach (AssemblyStoreAssembly asm in blob.Assemblies) {
				string not = asm.IsCompressed ? String.Empty : "not ";
				log.StatusLine ($"    {asm.Name}", $"{not}compressed");
			}
			log.MessageLine ();
		}
	}

	static string YesNo (bool yes) => yes ? "yes" : "no";

	static string ArchitectureName (AssemblyStoreReader blob)
	{
		return String.IsNullOrEmpty (blob.Arch) ? "yes, undetermined" : blob.Arch;
	}
}
