using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class AssemblyBlobDSOGenerator : LlvmIrComposer
{
	// The XA* constants must have values identical to their native counterparts as defined in src/monodroid/jni/xamarin-app.hh
	const string XAAssembliesConfigVarName    = "xa_assemblies_config";
	const string XAAssemblyConfigFilesVarName = "xa_assembly_config_files";
	const string XAAssemblyDataVarName        = "xa_assembly_data";
	const string XAAssemblyIndexVarName       = "xa_assembly_index";
	const string XAAssemblyNamesVarName       = "xa_assembly_names";
	const string XALoadedAssembliesVarName    = "xa_loaded_assemblies";

	readonly Dictionary<AndroidTargetArch, List<BlobAssemblyInfo>> assemblies;
	readonly Dictionary<AndroidTargetArch, ArchState> archStates;

	StructureInfo? assemblyIndexEntry32StructureInfo;
	StructureInfo? assemblyIndexEntry64StructureInfo;
	StructureInfo? assembliesConfigStructureInfo;

	public AssemblyBlobDSOGenerator (Dictionary<AndroidTargetArch, List<BlobAssemblyInfo>> assemblies)
	{
		archStates = new Dictionary<AndroidTargetArch, ArchState> ();

		this.assemblies = assemblies;
	}

	void PrepareData (out ulong assemblyCount, out List<string?> assemblyConfigs)
	{
		assemblyConfigs = new List<string?> ();
		uint assemblyNameLength = 0;
		int expectedAssemblyCount = -1;
		foreach (var kvp in assemblies) {
			AndroidTargetArch arch = kvp.Key;
			List<BlobAssemblyInfo> asmList = kvp.Value;

			if (expectedAssemblyCount == -1) {
				expectedAssemblyCount = asmList.Count;
			} else if (expectedAssemblyCount != asmList.Count) {
				throw new InvalidOperationException ($"Internal error: target architecture {arch} has an invalid number of assemblies ({asmList.Count} instead of {expectedAssemblyCount}");
			}

			if (!archStates.TryGetValue (arch, out ArchState archState)) {
				archState = new ArchState (asmList, arch);
				archStates.Add (arch, archState);
			}

			ProcessAssemblies (archState, asmList, assemblyConfigs, ref assemblyNameLength);
		}

		if (expectedAssemblyCount <= 0) {
			throw new InvalidOperationException ($"Internal error: no assemblies found");
		}

		assemblyNameLength++; // Must be nul-terminated
		assemblyCount = (ulong)expectedAssemblyCount;

		foreach (var kvp in archStates) {
			ArchState state = kvp.Value;

			state.AssemblyNameLength = assemblyNameLength;
			state.AssemblyCount = assemblyCount;
		}
	}

	void ProcessAssemblies (ArchState archState, List<BlobAssemblyInfo> asmList, List<string?> assemblyConfigs, ref uint assemblyNameLength)
	{
		ulong outputDataOffset = 0;
		uint infoIndex = 0;
		var usedHashes = new HashSet<ulong> ();

		// We add configs only for the first ABI, since all assemblies, linked or not, share their .config files
		bool addConfigs = assemblyConfigs.Count == 0;

		foreach (BlobAssemblyInfo info in asmList) {
			archState.DataSize = AddWithCheck (archState.DataSize, info.Size, UInt32.MaxValue, "Output data too big");
			archState.BlobSize = AddWithCheck (archState.BlobSize, info.SizeInBlob, UInt32.MaxValue, "Input data too big");

			if (info.NameBytes.Length > assemblyNameLength) {
				assemblyNameLength = (uint)info.NameBytes.Length;
			}

			// Offset in the output data array (the `xa_assembly_data` variable), after decompression
			info.Offset = outputDataOffset;
			outputDataOffset = AddWithCheck (outputDataOffset, info.Size, UInt32.MaxValue, "Output data too big");
			archState.AssemblyNames.Add (info.NameBytes);

			if (addConfigs) {
				assemblyConfigs.Add (info.Config);
			}

			string nameWithoutExtension = Path.GetFileNameWithoutExtension (info.Name);
			byte[] nameWithoutExtensionBytes = StringToBytes (nameWithoutExtension);
			ulong nameWithExtensionHash = EnsureUniqueHash (GetXxHash (info.NameBytes, archState.Is64Bit), info.Name);
			ulong nameWithoutExtensionHash = EnsureUniqueHash (GetXxHash (nameWithoutExtensionBytes, archState.Is64Bit), nameWithoutExtension);
			uint  entryFlags = 0;

			if (info.IsCompressed) {
				entryFlags |= AssemblyEntry_IsCompressed;
			}

			if (!String.IsNullOrEmpty (info.Config)) {
				entryFlags |= AssemblyEntry_HasConfig;
			}

			if (archState.Is64Bit) {
				var entryWithExtension = new AssemblyIndexEntry64 {
					Name = info.Name,
					name_hash = nameWithExtensionHash,
					input_data_offset = (uint)info.OffsetInBlob,
					input_data_size = (uint)info.SizeInBlob,
					output_data_offset = (uint)info.Offset,
					output_data_size = (uint)info.Size,
					info_index = infoIndex,
					flags = entryFlags,
				};
				archState.Index64.Add (new StructureInstance<AssemblyIndexEntry64> (assemblyIndexEntry64StructureInfo, entryWithExtension));

				var entryWithoutExtension = new AssemblyIndexEntry64 {
					Name = nameWithoutExtension,
					name_hash = nameWithoutExtensionHash,
					input_data_offset = entryWithExtension.input_data_offset,
					input_data_size = entryWithExtension.input_data_size,
					output_data_offset = entryWithExtension.output_data_offset,
					output_data_size = entryWithExtension.output_data_size,
					info_index = entryWithExtension.info_index,
					flags = entryWithExtension.flags,
				};
				archState.Index64.Add (new StructureInstance<AssemblyIndexEntry64> (assemblyIndexEntry64StructureInfo, entryWithoutExtension));
			} else {
				var entryWithExtension = new AssemblyIndexEntry32 {
					Name = info.Name,
					name_hash = (uint)nameWithExtensionHash,
					input_data_offset = (uint)info.OffsetInBlob,
					input_data_size = (uint)info.SizeInBlob,
					output_data_offset = (uint)info.Offset,
					output_data_size = (uint)info.Size,
					info_index = infoIndex,
					flags = entryFlags,
				};
				archState.Index32.Add (new StructureInstance<AssemblyIndexEntry32> (assemblyIndexEntry32StructureInfo, entryWithExtension));

				var entryWithoutExtension = new AssemblyIndexEntry32 {
					Name = nameWithoutExtension,
					name_hash = (uint)nameWithoutExtensionHash,
					input_data_offset = entryWithExtension.input_data_offset,
					input_data_size = entryWithExtension.input_data_size,
					output_data_offset = entryWithExtension.output_data_offset,
					output_data_size = entryWithExtension.output_data_size,
					info_index = entryWithExtension.info_index,
					flags = entryWithExtension.flags,
				};
				archState.Index32.Add (new StructureInstance<AssemblyIndexEntry32> (assemblyIndexEntry32StructureInfo, entryWithoutExtension));
			}
			infoIndex++;
		}

		if (archState.Is64Bit) {
			archState.Index64.Sort (
				(StructureInstance<AssemblyIndexEntry64> a, StructureInstance<AssemblyIndexEntry64> b) => ((AssemblyIndexEntry64)a.Obj).name_hash.CompareTo (((AssemblyIndexEntry64)b.Obj).name_hash)
			);
		} else {
			archState.Index32.Sort (
				(StructureInstance<AssemblyIndexEntry32> a, StructureInstance<AssemblyIndexEntry32> b) => ((AssemblyIndexEntry32)a.Obj).name_hash.CompareTo (((AssemblyIndexEntry32)b.Obj).name_hash)
			);
		}

		ulong AddWithCheck (ulong lhs, ulong rhs, ulong maxValue, string errorMessage)
		{
			ulong v = lhs + rhs;
			if (v > maxValue) {
				throw new InvalidOperationException ($"{errorMessage}, exceeding the maximum by {v - maxValue}");
			}

			return v;
		}

		ulong EnsureUniqueHash (ulong hash, string name)
		{
			if (usedHashes.Contains (hash)) {
				throw new InvalidOperationException ($"Hash 0x{hash:x} for name '{name}' is not unique");
			}

			usedHashes.Add (hash);
			return hash;
		}
	}

	protected override void Construct (LlvmIrModule module)
	{
		MapStructures (module);
		PrepareData (out ulong assemblyCount, out List<string?> assemblyConfigs);

		var xa_assemblies_config = new LlvmIrGlobalVariable (typeof(StructureInstance<AssembliesConfig>), XAAssembliesConfigVarName) {
			BeforeWriteCallback = AssembliesConfigBeforeWrite,
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (xa_assemblies_config);

		var xa_assembly_index = new LlvmIrGlobalVariable (typeof(List<StructureInstance<AssemblyIndexEntry32>>), XAAssemblyIndexVarName) {
			BeforeWriteCallback = AssemblyIndexBeforeWrite,
			GetArrayItemCommentCallback = AssemblyIndexItemComment,
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (xa_assembly_index);

		var xa_assembly_names = new LlvmIrGlobalVariable (typeof(List<byte[]>), XAAssemblyNamesVarName) {
			BeforeWriteCallback = AssemblyNamesBeforeWrite,
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (xa_assembly_names);

		var xa_assembly_config_files = new LlvmIrGlobalVariable (typeof(List<string?>), XAAssemblyConfigFilesVarName) {
			Value = assemblyConfigs,
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (xa_assembly_config_files);

		var xa_loaded_assemblies = new LlvmIrGlobalVariable (typeof(IntPtr[]), XALoadedAssembliesVarName) {
			ArrayItemCount = assemblyCount,
			Options = LlvmIrVariableOptions.GlobalWritable,
			ZeroInitializeArray = true,
		};
		module.Add (xa_loaded_assemblies);

		var xa_assembly_data = new LlvmIrGlobalVariable (typeof(byte[]), XAAssemblyDataVarName) {
			BeforeWriteCallback = AssemblyDataBeforeWrite,
			Options = LlvmIrVariableOptions.GlobalWritable,
			ZeroInitializeArray = true,
		};
		module.Add (xa_assembly_data);
	}

	void AssemblyNamesBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var names = new List<byte[]> ((int)archState.AssemblyCount);

		foreach (byte[] nameBytes in archState.AssemblyNames) {
			names.Add (GetProperlySizedBytesForNameArray ((uint)archState.AssemblyNameLength, nameBytes));
		}

		variable.Value = names;
	}

	static byte[] GetProperlySizedBytesForNameArray (uint requiredSize, byte[] inputBytes)
	{
		if (inputBytes.Length > requiredSize - 1) {
			throw new ArgumentOutOfRangeException (nameof (inputBytes), $"Must not exceed {requiredSize - 1} bytes");
		}

		var ret = new byte[requiredSize];
		Array.Clear (ret, 0, ret.Length);
		inputBytes.CopyTo (ret, 0);

		return ret;
	}

	string AssemblyIndexItemComment (LlvmIrVariable variable, LlvmIrModuleTarget target, ulong index, object? itemValue, object? state)
	{
		var value32 = itemValue as StructureInstance<AssemblyIndexEntry32>;
		if (value32 != null) {
			return MakeComment (((AssemblyIndexEntry32)value32.Obj).Name);
		}

		var value64 = itemValue as StructureInstance<AssemblyIndexEntry64>;
		if (value64 != null) {
			return MakeComment (((AssemblyIndexEntry64)value64.Obj).Name);
		}

		throw new InvalidOperationException ($"Internal error: assembly index array member has unsupported type '{itemValue?.GetType ()}'");
	}

	void AssemblyIndexBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var gv = (LlvmIrGlobalVariable)variable;
		object value;
		Type type;

		if (target.TargetArch == AndroidTargetArch.Arm64 || target.TargetArch == AndroidTargetArch.X86_64) {
			value = archState.Index64;
			type = archState.Index64.GetType ();
		} else if (target.TargetArch == AndroidTargetArch.Arm || target.TargetArch == AndroidTargetArch.X86) {
			value = archState.Index32;
			type = archState.Index32.GetType ();
		} else {
			throw new InvalidOperationException ($"Internal error: architecture {target.TargetArch} not supported");
		}

		gv.OverrideValueAndType (type, value);
	}

	void AssembliesConfigBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var gv = (LlvmIrGlobalVariable)variable;

		if (archState.BlobSize > UInt32.MaxValue) {
			throw new InvalidOperationException ("Assembly blob is too big");
		}

		var cfg = new AssembliesConfig {
			assembly_blob_size = (uint)archState.BlobSize,
			assembly_name_length = (uint)archState.AssemblyNameLength,
			assembly_count = (uint)archState.AssemblyCount,
			assembly_index_size = (uint)archState.AssemblyCount * 2,
		};
		variable.Value = new StructureInstance<AssembliesConfig> (assembliesConfigStructureInfo, cfg);
	}

	void AssemblyDataBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var gv = (LlvmIrGlobalVariable)variable;

		gv.ArrayItemCount = archState.DataSize;
	}

	static string MakeComment (string name) => $" => {name}";
	ArchState GetArchState (LlvmIrModuleTarget target) => GetArchState (target, archStates);

	static ArchState GetArchState (LlvmIrModuleTarget target, Dictionary<AndroidTargetArch, ArchState> archStates)
	{
		if (!archStates.TryGetValue (target.TargetArch, out ArchState archState)) {
			throw new InvalidOperationException ($"Internal error: architecture state for ABI {target.TargetArch} not available");
		}

		return archState;
	}

	void MapStructures (LlvmIrModule module)
	{
		assemblyIndexEntry32StructureInfo = module.MapStructure<AssemblyIndexEntry32> ();
		assemblyIndexEntry64StructureInfo = module.MapStructure<AssemblyIndexEntry64> ();
		assembliesConfigStructureInfo = module.MapStructure<AssembliesConfig> ();
	}
}
