using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class CreateEmbeddedAssemblyStore : AndroidTask
{
	public override string TaskPrefix => "CEAS";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string AppSharedLibrariesDir { get; set; } = "";

	[Required]
	public string AssemblySourcesDir { get; set; } = "";

	[Required]
	public bool AssemblyStoreEmbeddedInRuntime { get; set; }

	[Required]
	public bool IncludeDebugSymbols { get; set; }

	[Required]
	public ITaskItem[] ResolvedUserAssemblies { get; set; } = [];

	[Required]
	public ITaskItem[] ResolvedFrameworkAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public override bool RunTask ()
	{
		if (AssemblyStoreEmbeddedInRuntime) {
			return EmbedAssemblyStore ();
		}

		// Generate sources to satisfy libmonodroid's ABI requirements
		foreach (string abi in SupportedAbis) {
			ELFEmbeddingHelper.EmbedBinary (
				Log,
				abi,
				AndroidBinUtilsDirectory,
				inputFile: null,
				ELFEmbeddingHelper.KnownEmbedItems.AssemblyStore,
				AssemblySourcesDir,
				missingContentOK: true
			);
		}

		return !Log.HasLoggedErrors;
	}

	bool EmbedAssemblyStore ()
	{
		var assemblies = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Where (asm => !(AssemblyPackagingHelper.ShouldSkipAssembly (Log, asm)));
		var assemblyStorePaths = AssemblyPackagingHelper.CreateAssemblyStore (
			Log, assemblies,
			Path.Combine (AppSharedLibrariesDir, "embedded"),
			SupportedAbis,
			IncludeDebugSymbols
		);

		foreach (var kvp in assemblyStorePaths) {
			string abi = MonoAndroidHelper.ArchToAbi (kvp.Key);
			string inputFile = kvp.Value;

			ELFEmbeddingHelper.EmbedBinary (
				Log,
				abi,
				AndroidBinUtilsDirectory,
				inputFile,
				ELFEmbeddingHelper.KnownEmbedItems.AssemblyStore,
				AssemblySourcesDir,
				missingContentOK: false
			);
		}

		return !Log.HasLoggedErrors;
	}
}
