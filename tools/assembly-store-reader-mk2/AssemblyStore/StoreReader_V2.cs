using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.AssemblyStore;

partial class StoreReader_V2 : AssemblyStoreReader
{
	// Bit 31 is set for 64-bit platforms, cleared for the 32-bit ones
	const uint ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V2 = 0x80000002;
	const uint ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V2 = 0x00000002;
	const uint ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V3 = 0x80000003; // Must match the ASSEMBLY_STORE_FORMAT_VERSION native constant
	const uint ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V3 = 0x00000003;
	const uint ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_64BIT_V4 = 0x80000004; // Must match the ASSEMBLY_STORE_FORMAT_VERSION native constant
	const uint ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_32BIT_V4 = 0x00000004;
	const uint ASSEMBLY_STORE_FORMAT_VERSION_MASK  = 0xF0000000;
	const uint ASSEMBLY_STORE_FORMAT_NUMBER_MASK   = 0x0000FFFF;

	const uint ASSEMBLY_STORE_ABI_AARCH64          = 0x00010000;
	const uint ASSEMBLY_STORE_ABI_ARM              = 0x00020000;
	const uint ASSEMBLY_STORE_ABI_X64              = 0x00030000;
	const uint ASSEMBLY_STORE_ABI_X86              = 0x00040000;
	const uint ASSEMBLY_STORE_ABI_MASK             = 0x00FF0000;

	public override string Description => "Assembly store v2";
	public override bool NeedsExtensionInName => true;

	public static IList<string> ApkPaths      { get; }
	public static IList<string> AabPaths      { get; }
	public static IList<string> AabBasePaths  { get; }

	readonly HashSet<uint> supportedVersions;

	Header? header;
	ulong elfOffset = 0;

	static StoreReader_V2 ()
	{
		var paths = new List<string> ();
		AddArchPaths (paths, AndroidTargetArch.Arm64);
		AddArchPaths (paths, AndroidTargetArch.Arm);
		AddArchPaths (paths, AndroidTargetArch.X86_64);
		AddArchPaths (paths, AndroidTargetArch.X86);
		ApkPaths = paths.AsReadOnly ();
		AabBasePaths = ApkPaths;

		const string AabBaseDir = "base";
		paths = new List<string> ();
		AddArchPaths (paths, AndroidTargetArch.Arm64, AabBaseDir);
		AddArchPaths (paths, AndroidTargetArch.Arm, AabBaseDir);
		AddArchPaths (paths, AndroidTargetArch.X86_64, AabBaseDir);
		AddArchPaths (paths, AndroidTargetArch.X86, AabBaseDir);
		AabPaths = paths.AsReadOnly ();

		void AddArchPaths (List<string> targetPaths, AndroidTargetArch arch, string? root = null)
		{
			string abi = MonoAndroidHelper.ArchToAbi (arch);
			targetPaths.Add (GetArchPath (abi, "libassembly-store.so", root));
			targetPaths.Add (GetArchPath (abi, $"libassemblies.{abi}.blob.so", root));
		}

		string GetArchPath (string abi, string fileName, string? root)
		{
			const string LibDirName = "lib";
			var parts = new List<string> ();
			if (!String.IsNullOrEmpty (root)) {
				parts.Add (LibDirName);
			} else {
				root = LibDirName;
			}
			parts.Add (abi);
			parts.Add (fileName);
			return MonoAndroidHelper.MakeZipArchivePath (root, parts);
		}
	}

	public StoreReader_V2 (Stream store, string path)
		: base (store, path)
	{
		supportedVersions = new HashSet<uint> {
			ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V2 | ASSEMBLY_STORE_ABI_AARCH64,
			ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V2 | ASSEMBLY_STORE_ABI_X64,
			ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V2 | ASSEMBLY_STORE_ABI_ARM,
			ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V2 | ASSEMBLY_STORE_ABI_X86,
			ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V3 | ASSEMBLY_STORE_ABI_AARCH64,
			ASSEMBLY_STORE_FORMAT_VERSION_64BIT_V3 | ASSEMBLY_STORE_ABI_X64,
			ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V3 | ASSEMBLY_STORE_ABI_ARM,
			ASSEMBLY_STORE_FORMAT_VERSION_32BIT_V3 | ASSEMBLY_STORE_ABI_X86,
			ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_64BIT_V4 | ASSEMBLY_STORE_ABI_AARCH64,
			ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_64BIT_V4 | ASSEMBLY_STORE_ABI_X64,
			ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_32BIT_V4 | ASSEMBLY_STORE_ABI_ARM,
			ASSEMBLY_STORE_FORMAT_VERSION_CORECLR_32BIT_V4 | ASSEMBLY_STORE_ABI_X86,
		};
	}

	protected override ulong GetStoreStartDataOffset () => elfOffset;

	protected override bool IsSupported ()
	{
		StoreStream.Seek (0, SeekOrigin.Begin);
		using var reader = CreateReader ();

		uint magic = reader.ReadUInt32 ();
		if (magic == Utils.ELF_MAGIC) {
			ELFPayloadError error;
			(elfOffset, _, error) = Utils.FindELFPayloadOffsetAndSize (StoreStream);

			if (error != ELFPayloadError.None) {
				string message = error switch {
					ELFPayloadError.NotELF           => $"Store '{StorePath}' is not a valid ELF binary",
					ELFPayloadError.LoadFailed       => $"Store '{StorePath}' could not be loaded",
					ELFPayloadError.NotSharedLibrary => $"Store '{StorePath}' is not a shared ELF library",
					ELFPayloadError.NotLittleEndian  => $"Store '{StorePath}' is not a little-endian ELF image",
					ELFPayloadError.InvalidPayloadSymbol => $"Store '{StorePath}' has an invalid '_assembly_store' symbol",
					ELFPayloadError.NoPayloadSection     => $"Store '{StorePath}' does not contain the 'payload' section",
					_                                => $"Unknown ELF payload section error for store '{StorePath}': {error}"
				};
				Log.Debug (message);
			} else if (elfOffset >= 0) {
				StoreStream.Seek ((long)elfOffset, SeekOrigin.Begin);
				magic = reader.ReadUInt32 ();
			}
		}

		if (magic != Utils.ASSEMBLY_STORE_MAGIC) {
			Log.Debug ($"Store '{StorePath}' has invalid header magic number.");
			return false;
		}

		uint version = reader.ReadUInt32 ();
		if (!supportedVersions.Contains (version)) {
			Log.Debug ($"Store '{StorePath}' has unsupported version 0x{version:x}");
			return false;
		}

		uint entry_count       = reader.ReadUInt32 ();
		uint index_entry_count = reader.ReadUInt32 ();
		uint index_size        = reader.ReadUInt32 ();
		ulong content_id        = (version & ASSEMBLY_STORE_FORMAT_NUMBER_MASK) >= 4 ? reader.ReadUInt64 () : 0;

		header = new Header (magic, version, entry_count, index_entry_count, index_size, content_id);
		return true;
	}

	protected override void Prepare ()
	{
		if (header == null) {
			throw new InvalidOperationException ("Internal error: header not set, was IsSupported() called?");
		}

		TargetArch = (header.version & ASSEMBLY_STORE_ABI_MASK) switch {
			ASSEMBLY_STORE_ABI_AARCH64 => AndroidTargetArch.Arm64,
			ASSEMBLY_STORE_ABI_ARM     => AndroidTargetArch.Arm,
			ASSEMBLY_STORE_ABI_X64     => AndroidTargetArch.X86_64,
			ASSEMBLY_STORE_ABI_X86     => AndroidTargetArch.X86,
			_ => throw new NotSupportedException ($"Unsupported ABI in store version: 0x{header.version:x}")
		};

		Is64Bit = (header.version & ASSEMBLY_STORE_FORMAT_VERSION_MASK) != 0;
		AssemblyCount = header.entry_count;
		IndexEntryCount = header.index_entry_count;

		StoreStream.Seek ((long)elfOffset + header.NativeSize, SeekOrigin.Begin);
		using var reader = CreateReader ();

		uint indexEntrySize = GetIndexEntrySize ();
		var index = new List<IndexEntry> ();
		for (uint i = 0; i < header.index_entry_count; i++) {
			ulong name_hash;
			bool hasIgnoreFlag;
			if (indexEntrySize == IndexEntry.NativeSize64) {
				name_hash = reader.ReadUInt64 ();
				hasIgnoreFlag = true;
			} else if (indexEntrySize == IndexEntry.NativeSize32) {
				name_hash = (ulong)reader.ReadUInt32 ();
				hasIgnoreFlag = true;
			} else if (indexEntrySize == IndexEntry.NativeSize64_V2) {
				name_hash = reader.ReadUInt64 ();
				hasIgnoreFlag = false;
			} else if (indexEntrySize == IndexEntry.NativeSize32_V2) {
				name_hash = (ulong)reader.ReadUInt32 ();
				hasIgnoreFlag = false;
			} else {
				throw new InvalidOperationException ($"Assembly store '{StorePath}' index entry size {indexEntrySize} is not supported.");
			}

			uint descriptor_index = reader.ReadUInt32 ();
			bool ignore = hasIgnoreFlag && reader.ReadByte () != 0;
			index.Add (new IndexEntry (name_hash, descriptor_index, ignore));
		}

		var descriptors = new List<EntryDescriptor> ();
		for (uint i = 0; i < header.entry_count; i++) {
			uint mapping_index      = reader.ReadUInt32 ();
			uint data_offset        = reader.ReadUInt32 ();
			uint data_size          = reader.ReadUInt32 ();
			uint debug_data_offset  = reader.ReadUInt32 ();
			uint debug_data_size    = reader.ReadUInt32 ();
			uint config_data_offset = reader.ReadUInt32 ();
			uint config_data_size   = reader.ReadUInt32 ();

			var desc = new EntryDescriptor {
				mapping_index      = mapping_index,
				data_offset        = data_offset,
				data_size          = data_size,
				debug_data_offset  = debug_data_offset,
				debug_data_size    = debug_data_size,
				config_data_offset = config_data_offset,
				config_data_size   = config_data_size,
			};
			descriptors.Add (desc);
		}

		var names = new List<string> ();
		for (uint i = 0; i < header.entry_count; i++) {
			uint name_length = reader.ReadUInt32 ();
			byte[] name_bytes = reader.ReadBytes ((int)name_length);
			names.Add (Encoding.UTF8.GetString (name_bytes));
		}

		var tempItems = new Dictionary<uint, TemporaryItem> ();
		foreach (IndexEntry ie in index) {
			if (!tempItems.TryGetValue (ie.descriptor_index, out TemporaryItem? item)) {
				item = new TemporaryItem (names[(int)ie.descriptor_index], descriptors[(int)ie.descriptor_index], ie.ignore);
				tempItems.Add (ie.descriptor_index, item);
			}
			item.IndexEntries.Add (ie);
		}

		if (tempItems.Count != descriptors.Count) {
			throw new InvalidOperationException ($"Assembly store '{StorePath}' index is corrupted.");
		}

		var storeItems = new List<AssemblyStoreItem> ();
		foreach (var kvp in tempItems) {
			TemporaryItem ti = kvp.Value;
			var item = new StoreItem_V2 (TargetArch, ti.Name, Is64Bit, ti.IndexEntries, ti.Descriptor, ti.Ignored);
			storeItems.Add (item);
		}
		Assemblies = storeItems.AsReadOnly ();

		uint GetIndexEntrySize ()
		{
			if (header.index_entry_count == 0) {
				return 0;
			}

			if (header.index_size % header.index_entry_count != 0) {
				throw new InvalidOperationException ($"Assembly store '{StorePath}' index is corrupted: index size {header.index_size} is not evenly divisible by entry count {header.index_entry_count}.");
			}

			return header.index_size / header.index_entry_count;
		}
	}
}
