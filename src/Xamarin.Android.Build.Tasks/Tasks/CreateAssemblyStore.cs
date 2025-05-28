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
		var assemblies = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Where (asm => !(AssemblyPackagingHelper.ShouldSkipAssembly (Log, asm)));

		if (!UseAssemblyStore) {
			AssembliesToAddToArchive = assemblies.ToArray ();
			return !Log.HasLoggedErrors;
		}

		var assembly_store_paths = AssemblyPackagingHelper.CreateAssemblyStore (Log, assemblies, AppSharedLibrariesDir, SupportedAbis, IncludeDebugSymbols);
		AssembliesToAddToArchive = assembly_store_paths.Select (kvp => new TaskItem (kvp.Value, new Dictionary<string, string> { { "Abi", MonoAndroidHelper.ArchToAbi (kvp.Key) } })).ToArray ();

		return !Log.HasLoggedErrors;
	}
}
