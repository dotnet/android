using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class AssemblyBlobDSOGenerator : LlvmIrComposer
{
	Dictionary<AndroidTargetArch, List<BlobAssemblyInfo>> assemblies;

        StructureInfo? assemblyIndexEntry32StructureInfo;
        StructureInfo? assemblyIndexEntry64StructureInfo;
        StructureInfo? assembliesConfigStructureInfo;

	public AssemblyBlobDSOGenerator (Dictionary<AndroidTargetArch, List<BlobAssemblyInfo>> assemblies)
	{
		int expectedAssemblyCount = -1;
		foreach (var kvp in assemblies) {
			if (expectedAssemblyCount == -1) {
				expectedAssemblyCount = kvp.Value.Count;
				continue;
			}

			if (expectedAssemblyCount != kvp.Value.Count) {
				throw new InvalidOperationException ($"Internal error: target architecture {kvp.Key} has an invalid number of assemblies ({kvp.Value.Count} instead of {expectedAssemblyCount}");
			}
		}

		this.assemblies = assemblies;
	}

	protected override void Construct (LlvmIrModule module)
	{
		MapStructures (module);
	}

	void MapStructures (LlvmIrModule module)
	{
		assemblyIndexEntry32StructureInfo = module.MapStructure<AssemblyIndexEntry32> ();
		assemblyIndexEntry64StructureInfo = module.MapStructure<AssemblyIndexEntry64> ();
		assembliesConfigStructureInfo = module.MapStructure<AssembliesConfig> ();
	}
}
