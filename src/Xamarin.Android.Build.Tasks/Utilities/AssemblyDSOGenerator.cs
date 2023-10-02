using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class AssemblyDSOGenerator : LlvmIrComposer
{
	const string XAAssembliesConfigVarName         = "xa_assemblies_config";
	const string XAAssemblyIndexVarName            = "xa_assembly_index";
	const string XAAssemblyNamesVarName            = "xa_assembly_names";
	public const string XAInputAssemblyDataVarName = "xa_input_assembly_data";
	const string XAUncompressedAssemblyDataVarName = "xa_uncompressed_assembly_data";

	readonly Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>>? allAssemblies;
	readonly Dictionary<AndroidTargetArch, DSOAssemblyInfo>? standaloneAssemblies;
	readonly Dictionary<AndroidTargetArch, ArchState> assemblyArchStates;
	readonly HashSet<string>? fastPathAssemblies;
	readonly uint inputAssemblyDataSize;
	readonly uint uncompressedAssemblyDataSize;
	StructureInfo? assemblyEntryStructureInfo;
	StructureInfo? assemblyIndexEntry32StructureInfo;
	StructureInfo? assemblyIndexEntry64StructureInfo;
	StructureInfo? assembliesConfigStructureInfo;

	public AssemblyDSOGenerator (Dictionary<AndroidTargetArch, DSOAssemblyInfo> dsoAssemblies)
	{
		standaloneAssemblies = dsoAssemblies;
		assemblyArchStates = MakeArchStates ();
	}

	public AssemblyDSOGenerator (ICollection<string> fastPathAssemblyNames, Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssemblies, ulong inputAssemblyDataSize, ulong uncompressedAssemblyDataSize)
	{
		this.inputAssemblyDataSize = EnsureValidSize (inputAssemblyDataSize, nameof (inputAssemblyDataSize));
		this.uncompressedAssemblyDataSize = EnsureValidSize (uncompressedAssemblyDataSize, nameof (uncompressedAssemblyDataSize));
		allAssemblies = dsoAssemblies;
		assemblyArchStates = MakeArchStates ();

		if (fastPathAssemblyNames.Count == 0) {
			return;
		}

		fastPathAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (string asmName in fastPathAssemblyNames) {
			fastPathAssemblies.Add (asmName);
		}

		uint EnsureValidSize (ulong v, string name)
		{
			if (v > UInt32.MaxValue) {
				throw new ArgumentOutOfRangeException (name, "must not exceed UInt32.MaxValue");
			}

			return (uint)v;
		}
	}

	Dictionary<AndroidTargetArch, ArchState> MakeArchStates () => new Dictionary<AndroidTargetArch, ArchState> ();

	protected override void Construct (LlvmIrModule module)
	{
		if (standaloneAssemblies != null) {
			ConstructStandalone (module);
		} else {
			ConstructFastPath (module);
		}
	}

	void ConstructStandalone (LlvmIrModule module)
	{
		foreach (var kvp in standaloneAssemblies) {
			AndroidTargetArch arch = kvp.Key;
			DSOAssemblyInfo info = kvp.Value;

			AddStandaloneAssemblyData (arch, info);
		}

		var xa_input_assembly_data = new LlvmIrGlobalVariable (typeof(byte[]), XAInputAssemblyDataVarName) {
			Alignment = 4096,
			ArrayDataProvider = new StandaloneAssemblyInputDataArrayProvider (typeof(byte), assemblyArchStates),
			ArrayStride = 16,
			NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
			Options = LlvmIrVariableOptions.GlobalConstant,
			WriteOptions = LlvmIrVariableWriteOptions.ArrayFormatInRows,
		};

		module.Add (xa_input_assembly_data);
	}

	void ConstructFastPath (LlvmIrModule module)
	{
		MapStructures (module);

		if (allAssemblies.Count == 0) {
			ConstructEmptyModule ();
			return;
		}

		int expectedCount = -1;
		foreach (var kvp in allAssemblies) {
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
			NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
			Options = LlvmIrVariableOptions.GlobalConstant,
			WriteOptions = LlvmIrVariableWriteOptions.ArrayFormatInRows,
		};
		module.Add (xa_input_assembly_data);
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

		string MakeComment (string name) => $" => {name}";
	}

	void AssemblyNamesBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var names = new List<byte[]> ();

		if (target.TargetArch == AndroidTargetArch.Arm64 || target.TargetArch == AndroidTargetArch.X86_64) {
			foreach (StructureInstance<AssemblyIndexEntry64> e in archState.xa_assembly_index64) {
				var entry = (AssemblyIndexEntry64)e.Obj;
				names.Add (GetProperlySizedBytes (entry.NameBytes));
			}
		} else if (target.TargetArch == AndroidTargetArch.Arm || target.TargetArch == AndroidTargetArch.X86) {
			foreach (StructureInstance<AssemblyIndexEntry32> e in archState.xa_assembly_index32) {
				var entry = (AssemblyIndexEntry32)e.Obj;
				names.Add (GetProperlySizedBytes (entry.NameBytes));
			}
		} else {
			throw new InvalidOperationException ($"Internal error: architecture {target.TargetArch} not supported");
		}

		variable.Value = names;

		byte[] GetProperlySizedBytes (byte[] inputBytes)
		{
			if (inputBytes.Length > archState.xa_assemblies_config.assembly_name_length - 1) {
				throw new ArgumentOutOfRangeException (nameof (inputBytes), $"Must not exceed {archState.xa_assemblies_config.assembly_name_length - 1} bytes");
			}

			var ret = new byte[archState.xa_assemblies_config.assembly_name_length];
			Array.Clear (ret, 0, ret.Length);
			inputBytes.CopyTo (ret, 0);

			return ret;
		}
	}

	void AssemblyIndexBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		var gv = (LlvmIrGlobalVariable)variable;
		object value;
		Type type;

		if (target.TargetArch == AndroidTargetArch.Arm64 || target.TargetArch == AndroidTargetArch.X86_64) {
			value = archState.xa_assembly_index64;
			type = archState.xa_assembly_index64.GetType ();
		} else if (target.TargetArch == AndroidTargetArch.Arm || target.TargetArch == AndroidTargetArch.X86) {
			value = archState.xa_assembly_index32;
			type = archState.xa_assembly_index32.GetType ();
		} else {
			throw new InvalidOperationException ($"Internal error: architecture {target.TargetArch} not supported");
		}

		gv.OverrideValueAndType (type, value);
	}

	void AssembliesConfigBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		ArchState archState = GetArchState (target);
		variable.Value = new StructureInstance<AssembliesConfig> (assembliesConfigStructureInfo, archState.xa_assemblies_config);
	}

	ArchState GetArchState (LlvmIrModuleTarget target) => GetArchState (target, assemblyArchStates);

	static ArchState GetArchState (LlvmIrModuleTarget target, Dictionary<AndroidTargetArch, ArchState> archStates)
	{
		if (!archStates.TryGetValue (target.TargetArch, out ArchState archState)) {
			throw new InvalidOperationException ($"Internal error: architecture state for ABI {target.TargetArch} not available");
		}

		return archState;
	}

	protected override void CleanupAfterGeneration (AndroidTargetArch arch)
	{
		if (!assemblyArchStates.TryGetValue (arch, out ArchState archState)) {
			throw new InvalidOperationException ($"Internal error: data for ABI {arch} not available");
		}

		foreach (StructureInstance<AssemblyEntry> si in archState.xa_assemblies) {
			var entry = (AssemblyEntry)si.Obj;
			entry.AssemblyData = null; // Help the GC a bit
		}
	}

	(ArchState archState, bool is64Bit) GetArchState (AndroidTargetArch arch, int assemblyCount, StandaloneAssemblyEntry? standaloneAssembly = null)
	{
		if (!assemblyArchStates.TryGetValue (arch, out ArchState archState)) {
			archState = new ArchState (assemblyCount, arch, standaloneAssembly);
			assemblyArchStates.Add (arch, archState);
		}

		bool is64Bit = arch switch {
			AndroidTargetArch.Arm => false,
			AndroidTargetArch.X86 => false,
			AndroidTargetArch.Arm64 => true,
			AndroidTargetArch.X86_64 => true,
			_ => throw new NotSupportedException ($"Architecture '{arch}' is not supported")
		};

		return (archState, is64Bit);
	}

	uint GetInputSize (DSOAssemblyInfo info)
	{
		uint ret = info.CompressedDataSize == 0 ? (uint)info.DataSize : (uint)info.CompressedDataSize;
		if (ret > Int32.MaxValue) {
			throw new InvalidOperationException ($"Assembly {info.InputFile} size exceeds 2GB");
		}

		return ret;
	}

	void ReadAssemblyData (DSOAssemblyInfo info, StandaloneAssemblyEntry entry)
	{
		using (var asmFile = File.Open (info.InputFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
			asmFile.Read (entry.AssemblyData, 0, entry.AssemblyData.Length);
		}
	}

	void AddStandaloneAssemblyData (AndroidTargetArch arch, DSOAssemblyInfo info)
	{
		uint inputSize = GetInputSize (info);
		var entry = new StandaloneAssemblyEntry {
			AssemblyData = new byte[inputSize],
			InputFilePath = info.InputFile,
		};
		(ArchState archState, bool _) = GetArchState (arch, 1, entry);

		ReadAssemblyData (info, entry);
	}

	void AddAssemblyData (AndroidTargetArch arch, List<DSOAssemblyInfo> infos)
	{
		if (infos.Count == 0) {
			return;
		}

		(ArchState archState, bool is64Bit) = GetArchState (arch, infos.Count);
		var usedHashes = new HashSet<ulong> ();
		ulong inputOffset = 0;
		ulong uncompressedOffset = 0;
		ulong assemblyNameLength = 0;
		foreach (DSOAssemblyInfo info in infos) {
			uint inputSize = GetInputSize (info);

			// We need to read each file into a separate array, as it is (theoretically) possible that all the assemblies data will exceed 2GB,
			// which is the limit of we can allocate (or rent, below) in .NET, per single array.
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
			ReadAssemblyData (info, entry);

			// This is way, way more than Google Play Store supports now, but we won't limit ourselves more than we have to
			uncompressedOffset = AddWithCheck (uncompressedOffset, entry.uncompressed_data_offset, UInt32.MaxValue, "Compressed data too long");
			archState.xa_assemblies.Add (new StructureInstance<AssemblyEntry> (assemblyEntryStructureInfo, entry));

			byte[] nameBytes = StringToBytes (info.Name);
			if ((ulong)nameBytes.Length > assemblyNameLength) {
				assemblyNameLength = (ulong)nameBytes.Length;
			}
			ulong nameHash = EnsureUniqueHash (GetXxHash (nameBytes, is64Bit), info.Name);

			string nameWithoutExtension;
			string? dirName = Path.GetDirectoryName (info.Name);

			if (String.IsNullOrEmpty (dirName)) {
				nameWithoutExtension = Path.GetFileNameWithoutExtension (info.Name);
			} else {
				// Don't use Path.Combine because the `/` separator must remain as such, since it's not a "real"
				// directory separator but a culture/name separator.  Path.Combine would use `\` on Windows.
				nameWithoutExtension = $"{dirName}/{Path.GetFileNameWithoutExtension (info.Name)}";
			}

			byte[] nameWithoutExtensionBytes = StringToBytes (nameWithoutExtension);
			ulong nameWithoutExtensionHash = EnsureUniqueHash (GetXxHash (nameWithoutExtensionBytes, is64Bit), nameWithoutExtension);

			uint assemblyIndex = (uint)archState.xa_assemblies.Count - 1;

			if (is64Bit) {
				var indexEntry = new AssemblyIndexEntry64 {
					Name = info.Name,
					NameBytes = nameBytes,
					name_hash = nameHash,
					index = assemblyIndex,
					has_extension = true,
				};
				archState.xa_assembly_index64.Add (new StructureInstance<AssemblyIndexEntry64> (assemblyIndexEntry64StructureInfo, indexEntry));

				indexEntry = new AssemblyIndexEntry64 {
					Name = nameWithoutExtension,
					NameBytes = nameWithoutExtensionBytes,
					name_hash = nameWithoutExtensionHash,
					index = assemblyIndex,
					has_extension = false,
				};
				archState.xa_assembly_index64.Add (new StructureInstance<AssemblyIndexEntry64> (assemblyIndexEntry64StructureInfo, indexEntry));
			} else {
				var indexEntry = new AssemblyIndexEntry32 {
					Name = info.Name,
					NameBytes = nameBytes,
					name_hash = (uint)nameHash,
					index = assemblyIndex,
				};
				archState.xa_assembly_index32.Add (new StructureInstance<AssemblyIndexEntry32> (assemblyIndexEntry32StructureInfo, indexEntry));

				indexEntry = new AssemblyIndexEntry32 {
					Name = nameWithoutExtension,
					NameBytes = nameWithoutExtensionBytes,
					name_hash = (uint)nameWithoutExtensionHash,
					index = assemblyIndex,
					has_extension = false,
				};
				archState.xa_assembly_index32.Add (new StructureInstance<AssemblyIndexEntry32> (assemblyIndexEntry32StructureInfo, indexEntry));
			}
		}

		if (is64Bit) {
			archState.xa_assembly_index64.Sort (
				(StructureInstance<AssemblyIndexEntry64> a, StructureInstance<AssemblyIndexEntry64> b) => ((AssemblyIndexEntry64)a.Obj).name_hash.CompareTo (((AssemblyIndexEntry64)b.Obj).name_hash)
			);
		} else {
			archState.xa_assembly_index32.Sort (
				(StructureInstance<AssemblyIndexEntry32> a, StructureInstance<AssemblyIndexEntry32> b) => ((AssemblyIndexEntry32)a.Obj).name_hash.CompareTo (((AssemblyIndexEntry32)b.Obj).name_hash)
			);
		}

		archState.xa_assemblies_config.assembly_count = (uint)archState.xa_assemblies.Count;
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

		ulong EnsureUniqueHash (ulong hash, string name)
		{
			if (usedHashes.Contains (hash)) {
				throw new InvalidOperationException ($"Hash 0x{hash:x} for name '{name}' is not unique");
			}

			usedHashes.Add (hash);
			return hash;
		}
	}

	void ConstructEmptyModule ()
	{
		throw new NotImplementedException ();
	}

	void MapStructures (LlvmIrModule module)
	{
		assemblyEntryStructureInfo = module.MapStructure<AssemblyEntry> ();
		assemblyIndexEntry32StructureInfo = module.MapStructure<AssemblyIndexEntry32> ();
		assemblyIndexEntry64StructureInfo = module.MapStructure<AssemblyIndexEntry64> ();
		assembliesConfigStructureInfo = module.MapStructure<AssembliesConfig> ();
	}
}
