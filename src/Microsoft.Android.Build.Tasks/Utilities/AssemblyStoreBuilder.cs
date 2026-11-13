#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Microsoft.Android.Tasks;

class AssemblyStoreBuilder
{
	readonly TaskLoggingHelper log;
	readonly AssemblyStoreGenerator storeGenerator;
	readonly TaskEnvironment taskEnvironment;

	public AssemblyStoreBuilder (TaskLoggingHelper log, TaskEnvironment taskEnvironment)
	{
		this.log = log;
		this.taskEnvironment = taskEnvironment;
		storeGenerator = new (log, taskEnvironment);
	}

	public void AddAssembly (string assemblySourcePath, ITaskItem assemblyItem, bool includeDebugSymbols)
	{
		var storeAssemblyInfo = new AssemblyStoreAssemblyInfo (assemblySourcePath, assemblyItem, taskEnvironment: taskEnvironment);

		// Try to add config if exists.  We use assemblyItem, because `sourcePath` might refer to a compressed
		// assembly file in a different location.
		var config = taskEnvironment.GetAbsolutePath (Path.ChangeExtension (assemblyItem.ItemSpec, "dll.config"));
		if (File.Exists (config)) {
			storeAssemblyInfo.ConfigFile = new FileInfo (config);
		}

		if (includeDebugSymbols) {
			var debugSymbolsPath = taskEnvironment.GetAbsolutePath (Path.ChangeExtension (assemblyItem.ItemSpec, "pdb"));
			if (File.Exists (debugSymbolsPath)) {
				storeAssemblyInfo.SymbolsFile = new FileInfo (debugSymbolsPath);
			}
		}

		storeGenerator.Add (storeAssemblyInfo);
	}

	public Dictionary<AndroidTargetArch, string> Generate (string outputDirectoryPath) => storeGenerator.Generate (outputDirectoryPath);
}
