using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class AssemblyDSOGenerator
{
	class StandaloneAssemblyEntry
	{
		[NativeAssembler (Ignore = true)]
		public byte[]? AssemblyData;

		[NativeAssembler (Ignore = true)]
		public string InputFilePath;

		// If `true`, we have an instance of a standalone assembly being processed when
		// generating libxamarin-app.so sources.  This is necessary because we still need
		// all the assembly information (name, size etc) to generated indexes etc.  However,
		// in this case assembly data will not be needed as it's already in its own DSO
		[NativeAssembler (Ignore = true)]
		public bool IsStandalone;
	}

	// Must be identical to AssemblyEntry in src/monodroid/jni/xamarin-app.hh
	sealed class AssemblyEntry : StandaloneAssemblyEntry
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
	class AssemblyIndexEntryBase<T>
	{
		[NativeAssembler (Ignore = true)]
		public string Name;

		[NativeAssembler (Ignore = true)]
		public byte[] NameBytes;

		[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public T name_hash;

		// Index into the `xa_assemblies` descriptor array
		public uint index;

		// whether hashed name had extension
		public bool has_extension;

		// whether assembly data lives in a separate DSO
		public bool is_standalone;
	}

	sealed class AssemblyIndexEntry32 : AssemblyIndexEntryBase<uint>
	{}

	sealed class AssemblyIndexEntry64 : AssemblyIndexEntryBase<ulong>
	{}

	// Must be identical to AssembliesConfig in src/monodroid/jni/xamarin-app.hh
	sealed class AssembliesConfig
	{
		public uint input_assembly_data_size;
		public uint uncompressed_assembly_data_size;
		public uint assembly_name_length;
		public uint assembly_count;
	};

	// Members with underscores correspond to the native fields we output.
	sealed class ArchState
	{
		public readonly List<StructureInstance<AssemblyEntry>> xa_assemblies;
		public readonly List<StructureInstance<AssemblyIndexEntry32>>? xa_assembly_index32;
		public readonly List<StructureInstance<AssemblyIndexEntry64>>? xa_assembly_index64;
		public readonly AssembliesConfig xa_assemblies_config;
		public readonly StandaloneAssemblyEntry? StandaloneAssembly;

		public ArchState (int assemblyCount, AndroidTargetArch arch, StandaloneAssemblyEntry? standaloneAssembly = null)
		{
			if (assemblyCount < 0) {
				throw new ArgumentException ("must not be a negative number", nameof (assemblyCount));
			}

			StandaloneAssembly = standaloneAssembly;
			xa_assemblies = new List<StructureInstance<AssemblyEntry>> (assemblyCount);

			switch (arch) {
				case AndroidTargetArch.Arm64:
				case AndroidTargetArch.X86_64:
					xa_assembly_index64 = new List<StructureInstance<AssemblyIndexEntry64>> (assemblyCount);
					break;

				case AndroidTargetArch.Arm:
				case AndroidTargetArch.X86:
					xa_assembly_index32 = new List<StructureInstance<AssemblyIndexEntry32>> (assemblyCount);
					break;

				default:
					throw new InvalidOperationException ($"Internal error: architecture {arch} not supported");
			}

			xa_assemblies_config = new AssembliesConfig {
				input_assembly_data_size = 0,
				uncompressed_assembly_data_size = 0,
				assembly_name_length = 0,
				assembly_count = (uint)assemblyCount,
			};
		}
	}

	abstract class StreamedArrayDataProvider : LlvmIrStreamedArrayDataProvider
	{
		readonly Dictionary<AndroidTargetArch, ArchState> assemblyArchStates;

		protected StreamedArrayDataProvider (Type arrayElementType, Dictionary<AndroidTargetArch, ArchState> assemblyArchStates)
			: base (arrayElementType)
		{
			this.assemblyArchStates = assemblyArchStates;
		}

		protected ArchState GetArchState (LlvmIrModuleTarget target) => AssemblyDSOGenerator.GetArchState (target, assemblyArchStates);

		protected byte[] EnsureValidAssemblyData (StandaloneAssemblyEntry? entry)
		{
			if (entry == null) {
				throw new ArgumentNullException (nameof (entry));
			}

			if (!entry.IsStandalone) {
				if (entry.AssemblyData == null) {
					throw new InvalidOperationException ("Internal error: assembly data must be present");
				}

				if (entry.AssemblyData.Length == 0) {
					throw new InvalidOperationException ("Internal error: assembly data must not be empty");
				}
			}

			return entry.AssemblyData;
		}
	}

	sealed class StandaloneAssemblyInputDataArrayProvider : StreamedArrayDataProvider
	{
		public StandaloneAssemblyInputDataArrayProvider (Type arrayElementType, Dictionary<AndroidTargetArch, ArchState> assemblyArchStates)
			: base (arrayElementType, assemblyArchStates)
		{}

		public override (LlvmIrStreamedArrayDataProviderState status, ICollection data) GetData (LlvmIrModuleTarget target)
		{
			ArchState archState = GetArchState (target);

			return (
				LlvmIrStreamedArrayDataProviderState.LastSection,
				EnsureValidAssemblyData (archState.StandaloneAssembly)
			);
		}

		public override ulong GetTotalDataSize (LlvmIrModuleTarget target)
		{
			ArchState archState = GetArchState (target);
			return (ulong)(archState.StandaloneAssembly?.AssemblyData.Length ?? throw new InvalidOperationException ($"Internal error: standalone assembly not set"));
		}
	}

	sealed class AssemblyInputDataArrayProvider : StreamedArrayDataProvider
	{
		sealed class DataState
		{
			public int Index = 0;
			public string Comment = String.Empty;
			public ulong TotalDataSize = 0;
		}

		Dictionary<AndroidTargetArch, DataState> dataStates;

		public AssemblyInputDataArrayProvider (Type arrayElementType, Dictionary<AndroidTargetArch, ArchState> assemblyArchStates)
			: base (arrayElementType, assemblyArchStates)
		{
			dataStates = new Dictionary<AndroidTargetArch, DataState> ();
			foreach (var kvp in assemblyArchStates) {
				dataStates.Add (kvp.Key, new DataState ());
			}
		}

		public override (LlvmIrStreamedArrayDataProviderState status, ICollection? data) GetData (LlvmIrModuleTarget target)
		{
			ArchState archState = GetArchState (target);
			DataState dataState = GetDataState (target);
			int index = dataState.Index++;
			if (index >= archState.xa_assemblies.Count) {
				throw new InvalidOperationException ("Internal error: no more data left");
			}

			var entry = (AssemblyEntry)archState.xa_assemblies[index].Obj;
			if (entry.IsStandalone) {
				return (
					IsLastEntry () ? LlvmIrStreamedArrayDataProviderState.LastSectionNoData : LlvmIrStreamedArrayDataProviderState.NextSectionNoData,
					null
				);
			}

			string name;
			if (target.TargetArch == AndroidTargetArch.Arm64 || target.TargetArch == AndroidTargetArch.X86_64) {
				name = ((AssemblyIndexEntry64)archState.xa_assembly_index64[index].Obj).Name;
			} else if (target.TargetArch == AndroidTargetArch.Arm || target.TargetArch == AndroidTargetArch.X86) {
				name = ((AssemblyIndexEntry32)archState.xa_assembly_index32[index].Obj).Name;
			} else {
				throw new InvalidOperationException ($"Internal error: architecture {target.TargetArch} not supported");
			}

			string compressed = entry.uncompressed_data_size == 0 ? "no" : "yes";

			dataState.Comment = $" Assembly: {name} ({entry.InputFilePath}); Data size: {entry.AssemblyData.Length}; compressed: {compressed}";
			// Each assembly is a new "section"
			return (
				IsLastEntry () ? LlvmIrStreamedArrayDataProviderState.LastSection : LlvmIrStreamedArrayDataProviderState.NextSection,
				EnsureValidAssemblyData (entry)
			);

			bool IsLastEntry ()
			{
				if (index == archState.xa_assemblies.Count - 1) {
					return true;
				}

				// Special case: if between the current index and the end of array are only standalone assemblies, we need to terminate now or we're going to have
				// a dangling comma in the output which llc doesn't like.  Since we're in a forward-only streaming mode, we must take care of that corner case here,
				// alas.
				for (int i = index + 1; i < archState.xa_assemblies.Count; i++) {
					if (!((AssemblyEntry)archState.xa_assemblies[i].Obj).IsStandalone) {
						return false;
					}
				}

				return true;
			}
		}

		public override ulong GetTotalDataSize (LlvmIrModuleTarget target)
		{
			DataState dataState = GetDataState (target);
			if (dataState.TotalDataSize > 0) {
				return dataState.TotalDataSize;
			}

			ArchState archState = GetArchState (target);
			ulong totalSize = 0;
			foreach (StructureInstance<AssemblyEntry> si in archState.xa_assemblies) {
				var entry = (AssemblyEntry)si.Obj;
				if (entry.IsStandalone) {
					continue;
				}

				byte[] data = EnsureValidAssemblyData (entry);
				totalSize += (ulong)data.Length;
			}

			return dataState.TotalDataSize = totalSize;
		}

		public override string GetSectionStartComment (LlvmIrModuleTarget target)
		{
			DataState dataState = GetDataState (target);
			string ret = dataState.Comment;
			dataState.Comment = String.Empty;
			return ret;
		}

		DataState GetDataState (LlvmIrModuleTarget target)
		{
			if (!dataStates.TryGetValue (target.TargetArch, out DataState dataState)) {
				throw new InvalidOperationException ($"Internal error: data state for ABI {target.TargetArch} not available");
			}

			return dataState;
		}
	}
}
