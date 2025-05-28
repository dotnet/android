using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

static class AssemblyPackagingHelper
{
	public static bool ShouldSkipAssembly (TaskLoggingHelper log, ITaskItem asm)
	{
		var should_skip = asm.GetMetadataOrDefault ("AndroidSkipAddToPackage", false);

		if (should_skip) {
			log.LogDebugMessage ($"Skipping {asm.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");
		}

		return should_skip;
	}

	public static Dictionary<AndroidTargetArch, string> CreateAssemblyStore (TaskLoggingHelper log, IEnumerable<ITaskItem> assemblies, string outputDir, string[] supportedAbis, bool includeDebugSymbols)
	{
		var storeBuilder = new AssemblyStoreBuilder (log);
		var per_arch_assemblies = MonoAndroidHelper.GetPerArchAssemblies (assemblies, supportedAbis, true);

		foreach (var kvp in per_arch_assemblies) {
			log.LogDebugMessage ($"Adding assemblies for architecture '{kvp.Key}'");

			foreach (var assembly in kvp.Value.Values) {
				var sourcePath = assembly.GetMetadataOrDefault ("CompressedAssembly", assembly.ItemSpec);
				storeBuilder.AddAssembly (sourcePath, assembly, includeDebugSymbols: includeDebugSymbols);

				log.LogDebugMessage ($"Added '{sourcePath}' to assembly store.");
			}
		}

		Dictionary<AndroidTargetArch, string> assemblyStorePaths = storeBuilder.Generate (outputDir);
		if (assemblyStorePaths.Count == 0) {
			throw new InvalidOperationException ("Assembly store generator did not generate any stores");
		}

		if (assemblyStorePaths.Count != supportedAbis.Length) {
			throw new InvalidOperationException ("Internal error: assembly store did not generate store for each supported ABI");
		}

		return assemblyStorePaths;
	}

	public static void AddAssembliesFromCollection (TaskLoggingHelper Log, ICollection<string> SupportedAbis, ICollection<ITaskItem> assemblies, Action<TaskLoggingHelper, AndroidTargetArch, ITaskItem> doAddAssembly)
	{
		Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
			assemblies,
			SupportedAbis,
			validate: true,
			shouldSkip: (ITaskItem asm) => ShouldSkipAssembly (Log, asm)
		);

		foreach (var kvp in perArchAssemblies) {
			Log.LogDebugMessage ($"Adding assemblies for architecture '{kvp.Key}'");
			DoAddAssembliesFromArchCollection (Log, kvp.Key, kvp.Value, doAddAssembly);
		}
	}

	static void DoAddAssembliesFromArchCollection (TaskLoggingHelper Log, AndroidTargetArch arch, Dictionary<string, ITaskItem> assemblies, Action<TaskLoggingHelper, AndroidTargetArch, ITaskItem> doAddAssembly)
	{
		foreach (ITaskItem assembly in assemblies.Values) {
			if (MonoAndroidHelper.IsReferenceAssembly (assembly.ItemSpec, Log)) {
				Log.LogCodedWarning ("XA0107", assembly.ItemSpec, 0, Properties.Resources.XA0107, assembly.ItemSpec);
			}

			doAddAssembly (Log, arch, assembly);
		}
	}
}
