#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// If using $(AndroidUseAssemblyStore), place all the assemblies in a single .blob file.
/// </summary>
public class CreateAssemblyStore : AndroidTask
{
	public override string TaskPrefix => "CST";

	[Required]
	public string AppSharedLibrariesDir { get; set; } = "";

	public bool IncludeDebugSymbols { get; set; }

	[Required]
	public ITaskItem [] ResolvedFrameworkAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public bool UseAssemblyStore { get; set; }

	[Output]
	public ITaskItem [] AssembliesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		// Get all the user and framework assemblies we may need to package
		var assemblies = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Where (asm => !(ShouldSkipAssembly (asm))).ToArray ();

		if (!UseAssemblyStore) {
			AssembliesToAddToArchive = assemblies;
			return !Log.HasLoggedErrors;
		}

		var store_builder = new AssemblyStoreBuilder (Log);
		var per_arch_assemblies = MonoAndroidHelper.GetPerArchAssemblies (assemblies, SupportedAbis, true);

		foreach (var kvp in per_arch_assemblies) {
			Log.LogDebugMessage ($"Adding assemblies for architecture '{kvp.Key}'");

			foreach (var assembly in kvp.Value.Values) {
				var sourcePath = assembly.GetMetadataOrDefault ("CompressedAssembly", assembly.ItemSpec);
				store_builder.AddAssembly (sourcePath, assembly, includeDebugSymbols: IncludeDebugSymbols);

				Log.LogDebugMessage ($"Added '{sourcePath}' to assembly store.");
			}
		}

		var assembly_store_paths = store_builder.Generate (AppSharedLibrariesDir);

		if (assembly_store_paths.Count == 0) {
			throw new InvalidOperationException ("Assembly store generator did not generate any stores");
		}

		if (assembly_store_paths.Count != SupportedAbis.Length) {
			throw new InvalidOperationException ("Internal error: assembly store did not generate store for each supported ABI");
		}

		AssembliesToAddToArchive = assembly_store_paths.Select (kvp => new TaskItem (kvp.Value, new Dictionary<string, string> { { "Abi", MonoAndroidHelper.ArchToAbi (kvp.Key) } })).ToArray ();

		return !Log.HasLoggedErrors;
	}

	bool ShouldSkipAssembly (ITaskItem asm)
	{
		var should_skip = asm.GetMetadataOrDefault ("AndroidSkipAddToPackage", false);

		if (should_skip)
			Log.LogDebugMessage ($"Skipping {asm.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");

		return should_skip;
	}
}
