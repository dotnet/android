using System.Collections.Generic;

using Microsoft.Android.AppTools.Assemblies;
using Mono.Options;

namespace Microsoft.Android.AppTools.XAPP;

sealed class ParsedOptions
{
        public bool TypeMaps;
        public bool AssemblyStoreContent;
	public bool AssemblyDetails;
        public bool ExtractAssemblies;
}

class Xapp
{
	static readonly ConsoleLogger log = new ();
	static readonly ParsedOptions parsedOptions = new ();

	static int Main (string[] args)
	{
		log.Level = LogLevel.Debug;

                var opts = new OptionSet {
                        "Usage: xapp [OPTIONS] <path/to/file> [path/to/file ..]",
                        "",
                        "OPTIONS are:",
                        "",
                        { "t|typemaps", "Show detailed typemap information", v => parsedOptions.TypeMaps = true },
                        { "s|assembly-store-content", "List names of all assemblies in assembly stores", v => parsedOptions.AssemblyStoreContent = true },
			{ "a|assembly-details", "List details for each assembly", v => parsedOptions.AssemblyDetails = true },
                        { "e|extract-assemblies", "Extract assemblies from the APK/AAB archive or assembly store blob", v => parsedOptions.ExtractAssemblies = true },
                };
                List<string> rest = opts.Parse (args);
		bool haveErrors = false;

		foreach (string inputFile in rest) {
			var appInfo = new ApplicationInfo (log);
			if (!appInfo.Read (inputFile)) {
				haveErrors = true;
				continue;
			}

			log.StatusLine ("Input file path", inputFile);
			log.StatusLine ("Archive kind", appInfo.ArchiveKind.ToString ());
			if (appInfo.ArchiveKind != ArchiveKind.None) {
				log.StatusLine ("Runtime kind", appInfo.RuntimeKind);
			}

			bool haveAssemblyStores = appInfo.AssemblyStores != null;
			log.StatusYesNoLine ("Assembly store", haveAssemblyStores);
			if (haveAssemblyStores) {
				log.StatusLine ("Assembly store count", appInfo.AssemblyStores!.Count);
				foreach (AssemblyStore store in appInfo.AssemblyStores!) {
					log.InfoLine ($" {store.TargetArchitecture}");
					log.StatusLine ($"  Assembly count", store.NumberOfAssemblies);
					if (parsedOptions.AssemblyStoreContent) {
						ListAssemblies ("   ", store);
					}
				}
			}
			log.StatusLine ();
		}

		return haveErrors ? 1 : 0;
	}

	static void ListAssemblies (string indent, AssemblyStore store)
	{
		foreach (AssemblyStoreItem asm in store.Assemblies) {
			log.MessageLine ($"{indent}{asm.Name}");
			if (!parsedOptions.AssemblyDetails) {
				continue;
			}

			log.StatusYesNoLine ($"{indent} 64-bit", asm.Is64Bit);
			log.StatusLine ($"{indent} In-store size", asm.DataSize);
			log.StatusLine ($"{indent} Assembly image offset", asm.DataOffset);

			if (asm.DebugSize == 0 && asm.ConfigSize == 0) {
				log.MessageLine ($"{indent} Debug data and config file absent");
			} else {
				if (asm.DebugSize == 0) {
					log.MessageLine ($"{indent} Debug data absent");
				} else {
					log.StatusLine ($"{indent} Debug data size", asm.DebugSize);
					log.StatusLine ($"{indent} Debug data offset", asm.DebugOffset);
				}

				if (asm.ConfigSize == 0) {
					log.MessageLine ($"{indent} Config file absent");
				} else {
					log.StatusLine ($"{indent} Config file size", asm.ConfigSize);
					log.StatusLine ($"{indent} Config file offset", asm.ConfigOffset);
				}
			}

			log.InfoLine ($"{indent} Name hashes");
			foreach (ulong hash in asm.Hashes) {
				log.MessageLine ($"{indent}  0x{hash:x}");
			}
			log.MessageLine ();
		}
	}
}
