using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyStoreBuilder
{
	readonly TaskLoggingHelper log;
	readonly AssemblyStoreGenerator storeGenerator;

	public AssemblyStoreBuilder (TaskLoggingHelper log)
	{
		this.log = log;
		storeGenerator = new (log);
	}

	public void AddAssembly (string assemblySourcePath, ITaskItem assemblyItem, bool includeDebugSymbols)
	{
		var storeAssemblyInfo = new AssemblyStoreAssemblyInfo (assemblySourcePath, assemblyItem);

		// Try to add config if exists.  We use assemblyItem, because `sourcePath` might refer to a compressed
		// assembly file in a different location.
		var config = Path.ChangeExtension (assemblyItem.ItemSpec, "dll.config");
		if (File.Exists (config)) {
			storeAssemblyInfo.ConfigFile = new FileInfo (config);
		}

		if (includeDebugSymbols) {
			string debugSymbolsPath = Path.ChangeExtension (assemblyItem.ItemSpec, "pdb");
			if (File.Exists (debugSymbolsPath)) {
				storeAssemblyInfo.SymbolsFile = new FileInfo (debugSymbolsPath);
			}
		}

		storeGenerator.Add (storeAssemblyInfo);
	}

	public Dictionary<AndroidTargetArch, string> Generate (string outputDirectoryPath) => storeGenerator.Generate (outputDirectoryPath);
}
