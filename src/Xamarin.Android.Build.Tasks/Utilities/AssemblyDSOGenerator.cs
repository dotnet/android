using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyDSOGenerator : LlvmIrComposer
{
	// Must be identical to AssemblyEntry in src/monodroid/jni/xamarin-app.hh
	sealed class AssemblyEntry
	{
		// offset into the `xa_input_assembly_data` array
		public uint input_data_offset;

		// number of bytes data of this assembly occupies
		public uint input_data_size;

		// offset into the `xa_uncompressed_assembly_data` array where the uncompressed
		// assembly data (if any) lives.
		public uint uncompressed_data_offset;

		// Size of the uncompressed data. 0 if assembly wasn't compressed.
		public uint uncompressed_data_size;
	}

	// Must be identical to AssemblyIndexEntry in src/monodroid/jni/xamarin-app.hh
	sealed class AssemblyIndexEntry
	{
		ulong name_hash;

		// Index into the `xa_assemblies` descriptor array
		uint index;
	}

	Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> assemblies;
	StructureInfo? assemblyEntryStructureInfo;
	StructureInfo? assemblyIndexEntryStructureInfo;

	public AssemblyDSOGenerator (Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssemblies)
	{
		assemblies = dsoAssemblies;
	}

	protected override void Construct (LlvmIrModule module)
	{
		MapStructures (module);

		if (assemblies.Count == 0) {
			ConstructEmptyModule ();
			return;
		}

		int expectedCount = -1;
		foreach (var kvp in assemblies) {
			AndroidTargetArch arch = kvp.Key;
			List<DSOAssemblyInfo> infos = kvp.Value;

			if (expectedCount < 0) {
				expectedCount = infos.Count;
			}

			if (infos.Count != expectedCount) {
				throw new InvalidOperationException ($"Collection of assemblies for architecture {arch} has a different number of entries ({infos.Count}) than expected ({expectedCount})");
			}

			if (infos.Count == 0) {
				continue;
			}
		}

		if (expectedCount <= 0) {
			ConstructEmptyModule ();
		}

		throw new NotImplementedException ();
	}

	void ConstructEmptyModule ()
	{
		throw new NotImplementedException ();
	}

	void MapStructures (LlvmIrModule module)
	{
		assemblyEntryStructureInfo = module.MapStructure<AssemblyEntry> ();
		assemblyIndexEntryStructureInfo = module.MapStructure<AssemblyIndexEntry> ();
	}
}
