using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyDSOGenerator : LlvmIrComposer
{
	// Must be identical to AssemblyEntry in src/monodroid/jni/xamarin-app.hh
	sealed class AssemblyEntry
	{
		[NativeAssembler (Ignore = true)]
		public byte[]? AssemblyData;

		[NativeAssembler (Ignore = true)]
		public string InputFilePath;

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
		[NativeAssembler (Ignore = true)]
		public string Name;

		public ulong name_hash;

		// Index into the `xa_assemblies` descriptor array
		public uint index;
	}

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
		public readonly List<AssemblyEntry> xa_assemblies;
		public readonly List<AssemblyIndexEntry> xa_assembly_index;
		public readonly AssembliesConfig xa_assemblies_config;

		public ArchState (int assemblyCount)
		{
			if (assemblyCount < 0) {
				throw new ArgumentException ("must not be a negative number", nameof (assemblyCount));
			}

			xa_assemblies = new List<AssemblyEntry> (assemblyCount);
			xa_assembly_index = new List<AssemblyIndexEntry> (assemblyCount);
			xa_assemblies_config = new AssembliesConfig {
				input_assembly_data_size = 0,
				uncompressed_assembly_data_size = 0,
				assembly_name_length = 0,
				assembly_count = (uint)assemblyCount,
			};
		}
	}

	sealed class AssemblyInputDataArrayProvider : LlvmIrStreamedArrayDataProvider
	{
		sealed class DataState
		{
			public int Index = 0;
			public string Comment = String.Empty;
			public ulong TotalDataSize = 0;
		}

		Dictionary<AndroidTargetArch, ArchState> assemblyArchStates;
		Dictionary<AndroidTargetArch, DataState> dataStates;

		public AssemblyInputDataArrayProvider (Type arrayElementType, Dictionary<AndroidTargetArch, ArchState> assemblyArchStates)
			: base (arrayElementType)
		{
			this.assemblyArchStates = assemblyArchStates;

			dataStates = new Dictionary<AndroidTargetArch, DataState> ();
			foreach (var kvp in assemblyArchStates) {
				dataStates.Add (kvp.Key, new DataState ());
			}
		}

		public override (LlvmIrStreamedArrayDataProviderState status, ICollection data) GetData (LlvmIrModuleTarget target)
		{
			ArchState archState = GetArchState (target);
			DataState dataState = GetDataState (target);
			int index = dataState.Index++;
			if (index >= archState.xa_assemblies.Count) {
				throw new InvalidOperationException ("Internal error: no more data left");
			}

			AssemblyEntry entry = archState.xa_assemblies[index];
			AssemblyIndexEntry indexEntry = archState.xa_assembly_index[index];
			string compressed = entry.uncompressed_data_size == 0 ? "no" : "yes";

			dataState.Comment = $" Assembly: {indexEntry.Name} ({entry.InputFilePath}); Data size: {entry.AssemblyData.Length}; compressed: {compressed}";
			// Each assembly is a new "section"
			return (
				index == archState.xa_assemblies.Count - 1 ? LlvmIrStreamedArrayDataProviderState.LastSection : LlvmIrStreamedArrayDataProviderState.NextSection,
				EnsureValidAssemblyData (entry)
			);
		}

		public override ulong GetTotalDataSize (LlvmIrModuleTarget target)
		{
			DataState dataState = GetDataState (target);
			if (dataState.TotalDataSize > 0) {
				return dataState.TotalDataSize;
			}

			ArchState archState = GetArchState (target);
			ulong totalSize = 0;
			foreach (AssemblyEntry entry in archState.xa_assemblies) {
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

		ArchState GetArchState (LlvmIrModuleTarget target)
		{
			if (!assemblyArchStates.TryGetValue (target.TargetArch, out ArchState archState)) {
				throw new InvalidOperationException ($"Internal error: architecture state for ABI {target.TargetArch} not available");
			}

			return archState;
		}

		byte[] EnsureValidAssemblyData (AssemblyEntry entry)
		{
			if (entry.AssemblyData == null) {
				throw new InvalidOperationException ("Internal error: assembly data must be present");
			}

			if (entry.AssemblyData.Length == 0) {
				throw new InvalidOperationException ("Internal error: assembly data must not be empty");
			}

			return entry.AssemblyData;
		}
	}

	const string XAUncompressedAssemblyDataVarName = "xa_uncompressed_assembly_data";
	const string XAInputAssemblyDataVarName = "xa_input_assembly_data";

	readonly Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> assemblies;
	readonly Dictionary<AndroidTargetArch, ArchState> assemblyArchStates;
	readonly uint inputAssemblyDataSize;
	readonly uint uncompressedAssemblyDataSize;
	StructureInfo? assemblyEntryStructureInfo;
	StructureInfo? assemblyIndexEntryStructureInfo;
	StructureInfo? assembliesConfigStructureInfo;

	public AssemblyDSOGenerator (Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssemblies, ulong inputAssemblyDataSize, ulong uncompressedAssemblyDataSize)
	{
		this.inputAssemblyDataSize = EnsureValidSize (inputAssemblyDataSize, nameof (inputAssemblyDataSize));
		this.uncompressedAssemblyDataSize = EnsureValidSize (uncompressedAssemblyDataSize, nameof (uncompressedAssemblyDataSize));
		assemblies = dsoAssemblies;
		assemblyArchStates = new Dictionary<AndroidTargetArch, ArchState> ();

		uint EnsureValidSize (ulong v, string name)
		{
			if (v > UInt32.MaxValue) {
				throw new ArgumentOutOfRangeException (name, "must not exceed UInt32.MaxValue");
			}

			return (uint)v;
		}
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
			return;
		}

		var xa_uncompressed_assembly_data = new LlvmIrGlobalVariable (typeof(byte[]), XAUncompressedAssemblyDataVarName) {
			Alignment = 4096, // align to page boundary, may make access slightly faster
			ArrayItemCount = uncompressedAssemblyDataSize,
			Options = LlvmIrVariableOptions.GlobalWritable,
			ZeroInitializeArray = true,
		};
		module.Add (xa_uncompressed_assembly_data);

		var xa_input_assembly_data = new LlvmIrGlobalVariable (typeof(byte[]), XAInputAssemblyDataVarName) {
			Alignment = 4096,
			ArrayDataProvider = new AssemblyInputDataArrayProvider (typeof(byte), assemblyArchStates),
			ArrayStride = 16,
			Options = LlvmIrVariableOptions.GlobalConstant,
			WriteOptions = LlvmIrVariableWriteOptions.ArrayFormatInRows,
		};
		module.Add (xa_input_assembly_data);
	}

	protected override void CleanupAfterGeneration (AndroidTargetArch arch)
	{
		if (!assemblyArchStates.TryGetValue (arch, out ArchState archState)) {
			throw new InvalidOperationException ($"Internal error: data for ABI {arch} not available");
		}

		foreach (AssemblyEntry entry in archState.xa_assemblies) {
			entry.AssemblyData = null; // Help the GC a bit
		}
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

		bool is64Bit = arch switch {
			AndroidTargetArch.Arm => false,
			AndroidTargetArch.X86 => false,
			AndroidTargetArch.Arm64 => true,
			AndroidTargetArch.X86_64 => true,
			_ => throw new NotSupportedException ($"Architecture '{arch}' is not supported")
		};

		ulong inputOffset = 0;
		ulong uncompressedOffset = 0;
		ulong assemblyNameLength = 0;
		foreach (DSOAssemblyInfo info in infos) {
			uint inputSize = info.CompressedDataSize == 0 ? (uint)info.DataSize : (uint)info.CompressedDataSize;

			if (inputSize > Int32.MaxValue) {
				throw new InvalidOperationException ($"Assembly {info.InputFile} size exceeds 2GB");
			}

			// We need to read each file into a separate array, as it is (theoretically) possible that all the assemblies data will exceed 2GB,
			// which is the limit of we can allocate (or rent, below) in .NET.
			//
			// We also need to read all the assemblies for all the target ABIs, as it is possible that **all** of them will be different.
			//
			// All the data will then be concatenated on write time into a single native array.
			var entry = new AssemblyEntry {
				// We can't use the byte pool here, even though it would be more efficient, because the generator expects an ICollection,
				// which it then iterates on, and the rented arrays can (and frequently will) be bigger than the requested size.
				AssemblyData = new byte[inputSize],
				InputFilePath = info.InputFile,
				input_data_offset = (uint)inputOffset,
				input_data_size = inputSize,
				uncompressed_data_size = info.CompressedDataSize == 0 ? 0 : (uint)info.DataSize,
				uncompressed_data_offset = (uint)uncompressedOffset,
			};
			inputOffset = AddWithCheck (inputOffset, inputSize, UInt32.MaxValue, "Input data too long");

			using (var asmFile = File.Open (info.InputFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				asmFile.Read (entry.AssemblyData, 0, entry.AssemblyData.Length);
			}

			// This is way, way more than Google Play Store supports now, but we won't limit ourselves more than we have to
			uncompressedOffset = AddWithCheck (uncompressedOffset, entry.uncompressed_data_offset, UInt32.MaxValue, "Compressed data too long");
			archState.xa_assemblies.Add (entry);

			byte[] nameBytes = StringToBytes (info.Name);
			if ((ulong)nameBytes.Length > assemblyNameLength) {
				assemblyNameLength = (ulong)nameBytes.Length;
			}

			var index = new AssemblyIndexEntry {
				Name = info.Name,
				name_hash = GetXxHash (nameBytes, is64Bit),
				index = (uint)archState.xa_assemblies.Count - 1,
			};
			archState.xa_assembly_index.Add (index);
		}

		archState.xa_assemblies_config.input_assembly_data_size = (uint)inputOffset;
		archState.xa_assemblies_config.uncompressed_assembly_data_size = (uint)uncompressedOffset;

		// Must include the terminating NUL
		archState.xa_assemblies_config.assembly_name_length = (uint)assemblyNameLength + 1;

		ulong AddWithCheck (ulong lhs, ulong rhs, ulong maxValue, string errorMessage)
		{
			ulong v = lhs + rhs;
			if (v > maxValue) {
				throw new InvalidOperationException ($"{errorMessage}, exceeding the maximum by {uncompressedOffset - maxValue}");
			}

			return v;
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
		assembliesConfigStructureInfo = module.MapStructure<AssembliesConfig> ();
	}
}
