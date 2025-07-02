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
	readonly AndroidRuntime targetRuntime;

	public AssemblyStoreBuilder (TaskLoggingHelper log, AndroidRuntime targetRuntime)
	{
		this.log = log;
		this.targetRuntime = targetRuntime;
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

		ClrAddIgnoredNativeImageAssembly (assemblyItem);
	}

	// When CoreCLR tries to load an assembly (say `AssemblyName.dll`) it will always first try to load
	// a "native image" assembly from `AssemblyName.ni.dll` which will **never** exist. The native image
	// assemblies were once supported only on Windows and were never (nor will ever be) supported on
	// Unix. In order to speed up load times, we add an empty entry for each `*.ni.dll` to the assembly
	// store index.
	void ClrAddIgnoredNativeImageAssembly (ITaskItem assemblyItem)
	{
		if (targetRuntime != AndroidRuntime.CoreCLR) {
			return;
		}

		string ignoredName = Path.GetFileName (Path.ChangeExtension (assemblyItem.ItemSpec, ".ni.dll"));
		var storeAssemblyInfo = new AssemblyStoreAssemblyInfo (ignoredName, assemblyItem, assemblyIsIgnored: true);
		storeGenerator.Add (storeAssemblyInfo);
	}

	public Dictionary<AndroidTargetArch, string> Generate (string outputDirectoryPath) => storeGenerator.Generate (outputDirectoryPath);
}
