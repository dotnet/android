#nullable enable
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
// [ASSEMBLY_NAMES]
// [ASSEMBLY DATA]
//
// Formats of the sections above are as follows:
//
// HEADER (fixed size)
//  [MAGIC]              uint; value: 0x41424158
//  [FORMAT_VERSION]     uint; store format version number
//  [ENTRY_COUNT]        uint; number of entries in the store
//  [INDEX_ENTRY_COUNT]  uint; number of entries in the index
//  [INDEX_SIZE]         uint; index size in bytes
//
// INDEX (variable size, HEADER.ENTRY_COUNT*2 entries, for assembly names with and without the extension)
//  [NAME_HASH]          uint CRC32
//  [DESCRIPTOR_INDEX]   uint; index into in-store assembly descriptor array
//  [IGNORE]             byte; if set to anything other than 0, the assembly is to be ignored when loading
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
// ASSEMBLY_NAMES (variable size, HEADER.ENTRY_COUNT entries), each entry formatted as follows:
//  [NAME_LENGTH]        uint: length of assembly name
//  [NAME]               byte: UTF-8 bytes of assembly name, without the NUL terminator
//
partial class AssemblyStoreGenerator
{
	// The constants below must match their counterparts in src/native/*/include/xamarin-app.hh
	const uint ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian, must match the BUNDLED_ASSEMBLIES_BLOB_MAGIC native constant

	// Bit 31 is set for 64-bit platforms, cleared for the 32-bit ones
	const uint ASSEMBLY_STORE_FORMAT_VERSION_64BIT = 0x80000003; // Must match the ASSEMBLY_STORE_FORMAT_VERSION native constant
	const uint ASSEMBLY_STORE_FORMAT_VERSION_32BIT = 0x00000003;

	const uint ASSEMBLY_STORE_ABI_AARCH64 = 0x00010000;
	const uint ASSEMBLY_STORE_ABI_ARM = 0x00020000;
	const uint ASSEMBLY_STORE_ABI_X64 = 0x00030000;
	const uint ASSEMBLY_STORE_ABI_X86 = 0x00040000;

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
		(bool is64Bit, uint abiFlag) = arch switch {
			AndroidTargetArch.Arm    => (false, ASSEMBLY_STORE_ABI_ARM),
			AndroidTargetArch.X86    => (false, ASSEMBLY_STORE_ABI_X86),
			AndroidTargetArch.Arm64  => (true, ASSEMBLY_STORE_ABI_AARCH64),
			AndroidTargetArch.X86_64 => (true, ASSEMBLY_STORE_ABI_X64),
			_ => throw new NotSupportedException ($"Internal error: arch {arch} not supported")
		};

		string androidAbi = MonoAndroidHelper.ArchToAbi (arch);
		string outputDir = Path.Combine (baseOutputDirectory, androidAbi);
		Directory.CreateDirectory (outputDir);

		uint infoCount = (uint)infos.Count;
		string storePath = Path.Combine (outputDir, "assembly-store.so");
		var index = new List<AssemblyStoreIndexEntry> ();
		var descriptors = new List<AssemblyStoreEntryDescriptor> ();
		ulong namesSize = 0;

		foreach (AssemblyStoreAssemblyInfo info in infos) {
			namesSize += (ulong)info.AssemblyNameBytes.Length;
			namesSize += sizeof (uint);
		}

		ulong assemblyDataStart = (infoCount * AssemblyStoreIndexEntry.NativeSize * 2) + (AssemblyStoreEntryDescriptor.NativeSize * infoCount) + AssemblyStoreHeader.NativeSize + namesSize;
		// We'll start writing to the stream after we seek to the position just after the header, index, descriptors and name data.
		ulong curPos = assemblyDataStart;

		Directory.CreateDirectory (Path.GetDirectoryName (storePath));
		using var fs = File.Open (storePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
		fs.Seek ((long)curPos, SeekOrigin.Begin);

		uint mappingIndex = 0;
		foreach (AssemblyStoreAssemblyInfo info in infos) {
			(AssemblyStoreEntryDescriptor desc, curPos) = MakeDescriptor (info, curPos);
			if (info.Ignored) {
				desc.mapping_index = 0;
			} else {
				desc.mapping_index = mappingIndex++;
			}
			uint entryIndex = (uint)descriptors.Count;
			descriptors.Add (desc);

			if (!info.Ignored && (uint)fs.Position != desc.data_offset) {
				throw new InvalidOperationException ($"Internal error: corrupted store '{storePath}' stream");
			}

			uint name_with_ext_hash = TypeMapHelper.HashBytesForCLR (info.AssemblyNameBytes);
			uint name_no_ext_hash = TypeMapHelper.HashBytesForCLR (info.AssemblyNameNoExtBytes);
			index.Add (new AssemblyStoreIndexEntry (info.AssemblyName, name_with_ext_hash, entryIndex, info.Ignored));
			index.Add (new AssemblyStoreIndexEntry (info.AssemblyNameNoExt, name_no_ext_hash, entryIndex, info.Ignored));

			if (info.Ignored) {
				continue;
			}

			CopyData (info.SourceFile, fs, storePath);
			CopyData (info.SymbolsFile, fs, storePath);
			CopyData (info.ConfigFile, fs, storePath);
		}
		fs.Flush ();
		fs.Seek (0, SeekOrigin.Begin);

		uint storeVersion = is64Bit ? ASSEMBLY_STORE_FORMAT_VERSION_64BIT : ASSEMBLY_STORE_FORMAT_VERSION_32BIT;
		var header = new AssemblyStoreHeader (storeVersion | abiFlag, infoCount, (uint)index.Count, (uint)(index.Count * AssemblyStoreIndexEntry.NativeSize));
		using var writer = new BinaryWriter (fs);
		WriteHeader (writer, header);

		using var manifestFs = File.Open ($"{storePath}.manifest", FileMode.Create, FileAccess.Write, FileShare.Read);
		using var mw = new StreamWriter (manifestFs, new System.Text.UTF8Encoding (false));
		WriteIndex (writer, mw, index, descriptors);
		mw.Flush ();

		log.LogDebugMessage ($"Number of descriptors: {descriptors.Count}; index entries: {index.Count}");
		log.LogDebugMessage ($"Header size: {AssemblyStoreHeader.NativeSize}; index entry size: {AssemblyStoreIndexEntry.NativeSize}; descriptor size: {AssemblyStoreEntryDescriptor.NativeSize}");

		WriteDescriptors (writer, descriptors);
		WriteNames (writer, infos);
		writer.Flush ();

		if (fs.Position != (long)assemblyDataStart) {
			log.LogDebugMessage ($"fs.Position == {fs.Position}; assemblyDataStart == {assemblyDataStart}");
			throw new InvalidOperationException ($"Internal error: store '{storePath}' position is different than metadata size after header write");
		}

		return storePath;
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
			data_offset = info.Ignored ? 0 : (uint)curPos,
			data_size = info.Ignored ? 0 : GetDataLength (info.SourceFile),
		};
		if (info.SymbolsFile != null) {
			ret.debug_data_offset = ret.data_offset + ret.data_size;
			ret.debug_data_size = GetDataLength (info.SymbolsFile);
		}

		if (info.ConfigFile != null) {
			ret.config_data_offset = ret.data_offset + ret.data_size + ret.debug_data_size;
			ret.config_data_size = GetDataLength (info.ConfigFile);
		}

		if (!info.Ignored) {
			curPos += ret.data_size + ret.debug_data_size + ret.config_data_size;
			if (curPos > UInt32.MaxValue) {
				throw new NotSupportedException ("Assembly store size exceeds the maximum supported value");
			}
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

	void WriteHeader (BinaryWriter writer, AssemblyStoreHeader header)
	{
		writer.Write (header.magic);
		writer.Write (header.version);
		writer.Write (header.entry_count);
		writer.Write (header.index_entry_count);
		writer.Write (header.index_size);
	}
	void WriteIndex (BinaryWriter writer, StreamWriter manifestWriter, List<AssemblyStoreIndexEntry> index, List<AssemblyStoreEntryDescriptor> descriptors)
	{
		index.Sort ((AssemblyStoreIndexEntry a, AssemblyStoreIndexEntry b) => a.name_hash.CompareTo (b.name_hash));

		foreach (AssemblyStoreIndexEntry entry in index) {
			writer.Write (entry.name_hash);
			manifestWriter.Write ($"0x{entry.name_hash:x}");
			writer.Write (entry.descriptor_index);
			writer.Write ((byte)(entry.ignore ? 1 : 0));

			manifestWriter.Write ($" di:{entry.descriptor_index}");
			AssemblyStoreEntryDescriptor desc = descriptors[(int)entry.descriptor_index];
			manifestWriter.Write ($" mi:{desc.mapping_index}");
			manifestWriter.Write ($" do:{desc.data_offset}");
			manifestWriter.Write ($" ds:{desc.data_size}");
			manifestWriter.Write ($" ddo:{desc.debug_data_offset}");
			manifestWriter.Write ($" dds:{desc.debug_data_size}");
			manifestWriter.Write ($" cdo:{desc.config_data_offset}");
			manifestWriter.Write ($" cds:{desc.config_data_size}");
			manifestWriter.Write ($" {entry.name}");
			if (entry.ignore) {
				manifestWriter.Write (" (ignored)");
			}
			manifestWriter.WriteLine ();
		}
	}

	void WriteDescriptors (BinaryWriter writer, List<AssemblyStoreEntryDescriptor> descriptors)
	{
		foreach (AssemblyStoreEntryDescriptor desc in descriptors) {
			writer.Write (desc.mapping_index);
			writer.Write (desc.data_offset);
			writer.Write (desc.data_size);
			writer.Write (desc.debug_data_offset);
			writer.Write (desc.debug_data_size);
			writer.Write (desc.config_data_offset);
			writer.Write (desc.config_data_size);
		}
	}

	void WriteNames (BinaryWriter writer, List<AssemblyStoreAssemblyInfo> infos)
	{
		foreach (AssemblyStoreAssemblyInfo info in infos) {
			writer.Write ((uint)info.AssemblyNameBytes.Length);
			writer.Write (info.AssemblyNameBytes);
		}
	}
}
