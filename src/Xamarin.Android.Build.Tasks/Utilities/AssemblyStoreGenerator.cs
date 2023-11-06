using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

//
// Assembly store format
//
// Each target ABI/architecture has a single assembly store file, composed of the following parts:
//
// [HEADER]
// [INDEX]
// [ASSEMBLY_DESCRIPTORS]
// [ASSEMBLY DATA]
//
// Formats of the sections above are as follows:
//
// HEADER (fixed size)
//  [MAGIC]       uint; value: 0x41424158
//  [FORMAT]      uint; store format version number
//  [ENTRY_COUNT] uint; number of entries in the store
//
// INDEX (variable size, HEADER.ENTRY_COUNT*2 entries, for assembly names with and without the extension)
//  [NAME_HASH]        uint on 32-bit platforms, ulong on 64-bit platforms; xxhash of the assembly name
//  [DESCRIPTOR_INDEX] uint; index into in-store assembly descriptor array
//
// ASSEMBLY_DESCRIPTORS (variable size, HEADER.ENTRY_COUNT entries), each entry formatted as follows:
//  [MAPPING_INDEX]      uint; index into a runtime array where assembly data pointers are stored
//  [DATA_OFFSET]        uint; offset from the beginning of the store to the start of assembly data
//  [DATA_SIZE]          uint; size of the stored assembly data
//  [DEBUG_DATA_OFFSET]  uint; offset from the beginning of the store to the start of assembly PDB data, 0 if absent
//  [DEBUG_DATA_SIZE]    uint; size of the stored assembly PDB data, 0 if absent
//  [CONFIG_DATA_OFFSET] uint; offset from the beginning of the store to the start of assembly .config contents, 0 if absent
//  [CONFIG_DATA_SIZE]   uint; size of the stored assembly .config contents, 0 if absent
//
partial class AssemblyStoreGenerator
{
	// The two constants below must match their counterparts in src/monodroid/jni/xamarin-app.hh
	const uint ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian, must match the BUNDLED_ASSEMBLIES_BLOB_MAGIC native constant

        // Bit 31 is set for 64-bit platforms, cleared for the 32-bit ones
	const uint ASSEMBLY_STORE_FORMAT_VERSION_64BIT = 0x80000002; // Must match the ASSEMBLY_STORE_FORMAT_VERSION native constant
	const uint ASSEMBLY_STORE_FORMAT_VERSION_32BIT = 0x00000002;

	readonly TaskLoggingHelper log;
	readonly Dictionary<AndroidTargetArch, List<AssemblyStoreAssemblyInfo>> assemblies;

	public AssemblyStoreGenerator (TaskLoggingHelper log)
	{
		this.log = log;
		assemblies = new Dictionary<AndroidTargetArch, List<AssemblyStoreAssemblyInfo>> ();
	}

	public void Add (AssemblyStoreAssemblyInfo asmInfo)
	{
		if (!assemblies.TryGetValue (asmInfo.Arch, out List<AssemblyStoreAssemblyInfo> infos)) {
			infos = new List<AssemblyStoreAssemblyInfo> ();
			assemblies.Add (asmInfo.Arch, infos);
		}

		infos.Add (asmInfo);
	}

	public Dictionary<AndroidTargetArch, string> Generate (string baseOutputDirectory)
	{
		var ret = new Dictionary<AndroidTargetArch, string> ();

		foreach (var kvp in assemblies) {
			string storePath = Generate (baseOutputDirectory, kvp.Key, kvp.Value);
			ret.Add (kvp.Key, storePath);
		}

		return ret;
	}

	string Generate (string baseOutputDirectory, AndroidTargetArch arch, List<AssemblyStoreAssemblyInfo> infos)
	{
		bool is64Bit = arch switch {
			AndroidTargetArch.Arm => false,
			AndroidTargetArch.X86 => false,
			AndroidTargetArch.Arm64 => true,
			AndroidTargetArch.X86_64 => true,
			_ => throw new NotSupportedException ($"Internal error: arch {arch} not supported")
		};

		string androidAbi = MonoAndroidHelper.ArchToAbi (arch);
		uint infoCount = (uint)infos.Count;
		string storePath = Path.Combine (baseOutputDirectory, androidAbi, $"assemblies.{androidAbi}.blob.so");
		var index = new List<AssemblyStoreIndexEntry> ();
		var descriptors = new List<AssemblyStoreEntryDescriptor> ();

		ulong assemblyDataStart = (infoCount * IndexEntrySize () * 2) + (AssemblyStoreEntryDescriptor.NativeSize * infoCount) + AssemblyStoreHeader.NativeSize;
		// We'll start writing to the stream after we seek to the position just after the header, index and descriptors data.
		ulong curPos = assemblyDataStart;

		using var fs = File.Open (storePath, FileMode.Create, FileAccess.Write, FileShare.Read);
		fs.Seek ((long)curPos, SeekOrigin.Begin);

		foreach (AssemblyStoreAssemblyInfo info in infos) {
			(AssemblyStoreEntryDescriptor desc, curPos) = MakeDescriptor (info, curPos);
			desc.mapping_index = (uint)descriptors.Count;
			descriptors.Add (desc);

			if ((uint)fs.Position != desc.data_offset) {
				throw new InvalidOperationException ($"Internal error: corrupted store '{storePath}' stream");
			}

			string name_with_ext = Path.GetFileName (info.SourceFile.Name);
			ulong name_with_ext_hash = LLVMIR.LlvmIrComposer.GetXxHash (name_with_ext, is64Bit);

			string name_no_ext = Path.GetFileNameWithoutExtension (info.SourceFile.Name);
			ulong name_no_ext_hash = LLVMIR.LlvmIrComposer.GetXxHash (name_no_ext, is64Bit);
			index.Add (new AssemblyStoreIndexEntry (name_with_ext, name_with_ext_hash, desc.mapping_index));
			index.Add (new AssemblyStoreIndexEntry (name_no_ext, name_no_ext_hash, desc.mapping_index));

			CopyData (info.SourceFile, fs, storePath);
			CopyData (info.SymbolsFile, fs, storePath);
			CopyData (info.ConfigFile, fs, storePath);
		}
		fs.Flush ();
		fs.Seek (0, SeekOrigin.Begin);

		uint storeVersion = is64Bit ? ASSEMBLY_STORE_FORMAT_VERSION_64BIT : ASSEMBLY_STORE_FORMAT_VERSION_32BIT;
		var header = new AssemblyStoreHeader (storeVersion, infoCount);
		using var writer = new BinaryWriter (fs);
		writer.Write (header.magic);
		writer.Write (header.version);
		writer.Write (header.entry_count);

		index.Sort ((AssemblyStoreIndexEntry a, AssemblyStoreIndexEntry b) => a.name_hash.CompareTo (b.name_hash));

		using var manifestFs = File.Open ($"{storePath}.manifest", FileMode.Create, FileAccess.Write, FileShare.Read);
		using var mw = new StreamWriter (manifestFs, new System.Text.UTF8Encoding (false));
		foreach (AssemblyStoreIndexEntry entry in index) {
			if (is64Bit) {
				writer.Write (entry.name_hash);
				mw.Write ($"0x{entry.name_hash:x}");
			} else {
				writer.Write ((uint)entry.name_hash);
				mw.Write ($"0x{(uint)entry.name_hash:x}");
			}
			writer.Write (entry.descriptor_index);
			mw.Write ($" di:{entry.descriptor_index}");

			AssemblyStoreEntryDescriptor desc = descriptors[(int)entry.descriptor_index];
			mw.Write ($" mi:{desc.mapping_index}");
			mw.Write ($" do:{desc.data_offset}");
			mw.Write ($" ds:{desc.data_size}");
			mw.Write ($" ddo:{desc.debug_data_offset}");
			mw.Write ($" dds:{desc.debug_data_size}");
			mw.Write ($" cdo:{desc.config_data_offset}");
			mw.Write ($" cds:{desc.config_data_size}");
			mw.WriteLine ($" {entry.name}");
		}
		writer.Flush ();
		mw.Flush ();

		Console.WriteLine ($"Number of descriptors: {descriptors.Count}; index entries: {index.Count}");
		Console.WriteLine ($"Header size: {AssemblyStoreHeader.NativeSize}; index entry size: {IndexEntrySize ()}; descriptor size: {AssemblyStoreEntryDescriptor.NativeSize}");

		foreach (AssemblyStoreEntryDescriptor desc in descriptors) {
			writer.Write (desc.mapping_index);
			writer.Write (desc.data_offset);
			writer.Write (desc.data_size);
			writer.Write (desc.debug_data_offset);
			writer.Write (desc.debug_data_size);
			writer.Write (desc.config_data_offset);
			writer.Write (desc.config_data_size);
		}
		writer.Flush ();

		if (fs.Position != (long)assemblyDataStart) {
			Console.WriteLine ($"fs.Position == {fs.Position}; assemblyDataStart == {assemblyDataStart}");
			throw new InvalidOperationException ($"Internal error: store '{storePath}' position is different than metadata size after header write");
		}

		return storePath;

		uint IndexEntrySize () => is64Bit ? AssemblyStoreIndexEntry.NativeSize64 : AssemblyStoreIndexEntry.NativeSize32;
	}

	void CopyData (FileInfo? src, Stream dest, string storePath)
	{
		if (src == null) {
			return;
		}

		log.LogDebugMessage ($"Adding file '{src.Name}' to assembly store '{storePath}'");
		using var fs = src.Open (FileMode.Open, FileAccess.Read, FileShare.Read);
		fs.CopyTo (dest);
	}

	static (AssemblyStoreEntryDescriptor desc, ulong newPos) MakeDescriptor (AssemblyStoreAssemblyInfo info, ulong curPos)
	{
		var ret = new AssemblyStoreEntryDescriptor {
			data_offset = (uint)curPos,
			data_size = GetDataLength (info.SourceFile),
		};
		if (info.SymbolsFile != null) {
			ret.debug_data_offset = ret.data_offset + ret.data_size;
			ret.debug_data_size = GetDataLength (info.SymbolsFile);
		}

		if (info.ConfigFile != null) {
			ret.config_data_offset = ret.data_offset + ret.data_size + ret.debug_data_size;
			ret.config_data_size = GetDataLength (info.ConfigFile);
		}

		curPos += ret.data_size + ret.debug_data_size + ret.config_data_size;
		if (curPos > UInt32.MaxValue) {
			throw new NotSupportedException ("Assembly store size exceeds the maximum supported value");
		}

		return (ret, curPos);

		uint GetDataLength (FileInfo? info) {
			if (info == null) {
				return 0;
			}

			if (info.Length > UInt32.MaxValue) {
				throw new NotSupportedException ($"File '{info.Name}' exceeds the maximum supported size");
			}

			return (uint)info.Length;
		}
	}
}
