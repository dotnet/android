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

	// Members with underscores correspond to the native fields we output.
	sealed class ArchState
	{
		public readonly List<byte> xa_input_assembly_data = new List<byte> ();
		public ulong xa_uncompressed_assembly_data_length = 0;
		public readonly List<AssemblyEntry> xa_assemblies;
		public readonly List<AssemblyIndexEntry> xa_assembly_index;
		public ulong assemblyNameLength = 0;
		public readonly List<string> xa_assembly_names = new List<string> ();

		public ArchState (int assemblyCount)
		{
			if (assemblyCount < 0) {
				throw new ArgumentException ("must not be a negative number", nameof (assemblyCount));
			}

			xa_assemblies = new List<AssemblyEntry> (assemblyCount);
			xa_assembly_index = new List<AssemblyIndexEntry> (assemblyCount);
		}
	}

	readonly Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> assemblies;
	readonly Dictionary<AndroidTargetArch, ArchState> assemblyArchStates;
	StructureInfo? assemblyEntryStructureInfo;
	StructureInfo? assemblyIndexEntryStructureInfo;

	public AssemblyDSOGenerator (Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssemblies)
	{
		assemblies = dsoAssemblies;
		assemblyArchStates = new Dictionary<AndroidTargetArch, ArchState> ();
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

			AddAssemblyData (arch, infos);
		}

		if (expectedCount <= 0) {
			ConstructEmptyModule ();
		}

		throw new NotImplementedException ();
	}

	void AddAssemblyData (AndroidTargetArch arch, List<DSOAssemblyInfo> infos)
	{
		if (infos.Count == 0) {
			return;
		}

		if (!assemblyArchStates.TryGetValue (arch, out ArchState archState)) {
			archState = new ArchState (infos.Count);
			assemblyArchStates.Add (arch, archState);
		}

		ulong uncompressed_offset = 0;
		foreach (DSOAssemblyInfo info in infos) {
			var entry = new AssemblyEntry {
				input_data_offset = (uint)archState.xa_input_assembly_data.Count,
				input_data_size = info.CompressedDataSize == 0 ? (uint)info.DataSize : (uint)info.CompressedDataSize,
				uncompressed_data_size = info.CompressedDataSize == 0 ? 0 : (uint)info.DataSize,
				uncompressed_data_offset = (uint)uncompressed_offset,
			};

			uncompressed_offset += entry.uncompressed_data_offset;

			// This is way, way more than Google Play Store supports now, but we won't limit ourselves more than we have to
			if (uncompressed_offset > UInt32.MaxValue) {
				throw new InvalidOperationException ($"Excessive amount of uncompressed data, exceeding the maximum by {uncompressed_offset - UInt32.MaxValue}");
			}
		}
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
