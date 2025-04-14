using System.Collections.Generic;

using Mono.Options;
using Microsoft.Android.AppTools;

namespace Microsoft.Android.AppTools.XAPP;

sealed class ParsedOptions
{
        public bool TypeMaps;
        public bool AssemblyStore;
        public bool ExtractAssemblies;
}

class Xapp
{
	static readonly ConsoleLogger log = new ();

	static int Main (string[] args)
	{
		log.Level = LogLevel.Debug;

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
                List<string> rest = opts.Parse (args);

		return 0;
	}
}
