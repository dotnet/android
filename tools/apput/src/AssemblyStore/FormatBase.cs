using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApplicationUtility;

/// <summary>
/// `FormatBase` class is the base class for all format-specific validators/readers. It will
/// always implement reading the current (i.e. the `main` branch) assembly store format, with
/// subclasses required to handle differences. Subclasses are expected to override the virtual
/// `Read*` methods and completely handle reading of the respective structure, without calling
/// up to the base class.
/// </summary>
abstract class FormatBase
{
	protected Stream StoreStream { get; }
	protected string? Description { get; }

	public AssemblyStoreHeader? Header { get; protected set; }
	public List<AssemblyStoreAssemblyDescriptor>? Descriptors { get; protected set; }

	protected FormatBase (Stream storeStream, string? description)
	{
		this.StoreStream = storeStream;
		this.Description = description;
	}

	public void Read ()
	{
		using var reader = new BinaryReader (StoreStream, Encoding.UTF8, leaveOpen: true);

		// They can be `null` if `Validate` wasn't called for some reason.
		if (Header == null && ReadHeader (reader, out AssemblyStoreHeader? header)) {
			Header = header;
		}

		if (Descriptors == null && ReadAssemblyDescriptors (reader, out List<AssemblyStoreAssemblyDescriptor>? descriptors)) {
			Descriptors = descriptors;
		}
	}

	public IAspectState Validate ()
	{
		using var reader = new BinaryReader (StoreStream, Encoding.UTF8, leaveOpen: true);

		if (ReadHeader (reader, out AssemblyStoreHeader? header)) {
			Header = header;
		}

		if (ReadAssemblyDescriptors (reader, out List<AssemblyStoreAssemblyDescriptor>? descriptors)) {
			Descriptors = descriptors;
		}

		return ValidateInner ();
	}

	protected abstract IAspectState ValidateInner ();

	protected virtual bool ReadHeader (BinaryReader reader, out AssemblyStoreHeader? header)
	{
		header = null;
		try {
			header = DoReadHeader (reader);
			Log.Debug ("AssemblyStore/FormatBase: read store header.");
			Log.Debug ($"  Raw version: 0x{header.Version.RawVersion:x}");
			Log.Debug ($"  Main version: {header.Version.MainVersion}");
			Log.Debug ($"  ABI: {header.Version.ABI}");
			Log.Debug ($"  64-bit: {header.Version.Is64Bit}");
			Log.Debug ($"  Entry count: {header.EntryCount}");
			Log.Debug ($"  Index entry count: {header.IndexEntryCount}");
			Log.Debug ($"  Index size (bytes): {header.IndexSize}");
		} catch (Exception ex) {
			Log.Debug ($"AssemblyStore/FormatBase: Failed to read assembly store header. Exception thrown:", ex);
			return false;
		}

		return header != null;
	}

	AssemblyStoreHeader? DoReadHeader (BinaryReader reader)
	{
		StoreStream.Seek (0, SeekOrigin.Begin);

		// From src/native/clr/include/xamarin-app.hh
		//
		// HEADER (fixed size)
		//  [MAGIC]              uint; value: 0x41424158
		//  [FORMAT_VERSION]     uint; store format version number
		//  [ENTRY_COUNT]        uint; number of entries in the store
		//  [INDEX_ENTRY_COUNT]  uint; number of entries in the index
		//  [INDEX_SIZE]         uint; index size in bytes
		//

		// By the time we are called, the magic number has been verified. We simply ignore it.
		uint uintValue = reader.ReadUInt32 (); // magic
		uintValue = reader.ReadUInt32 (); // format version
		var storeVersion = new AssemblyStoreVersion (uintValue);

		uint entryCount = reader.ReadUInt32 ();
		uint indexEntryCount  = reader.ReadUInt32 ();
		uint indexSize = reader.ReadUInt32 ();

		return new AssemblyStoreHeader (storeVersion) {
			EntryCount = entryCount,
			IndexEntryCount = indexEntryCount,
			IndexSize = indexSize,
		};
	}

	protected virtual bool ReadAssemblyDescriptors (BinaryReader reader, out List<AssemblyStoreAssemblyDescriptor>? descriptors)
	{
		descriptors = null;
		try {
			descriptors = DoReadAssemblyDescriptors (reader);
		} catch (Exception ex) {
			Log.Debug ($"AssemblyStore/FormatBase: failed to read assembly descriptors. Exception thrown:", ex);
			return false;
		}

		return descriptors != null && descriptors.Count > 0;
	}

	List<AssemblyStoreAssemblyDescriptor>? DoReadAssemblyDescriptors (BinaryReader reader)
	{
		if (Header == null) {
			Log.Debug ($"AssemblyStore/FormatBase: unable to read descriptors, header hasn't been read.");
			return null;
		}

		if (Header.EntryCount == null) {
			Log.Debug ($"AssemblyStore/FormatBase: unable to read descriptors, header entry count hasn't been read.");
			return null;
		}

		ulong indexEntrySize = Header.Version.Is64Bit ? Format_V3.IndexEntrySize64 : Format_V3.IndexEntrySize32;
		ulong descriptorsOffset = (ulong)(Format_V3.HeaderSize + ((Header.EntryCount * 2) * indexEntrySize));

		if (descriptorsOffset > Int64.MaxValue) {
			Log.Debug ($"AssemblyStore/FormatBase: descriptors offset exceeds the maximum value handled by System.IO.Stream");
			return null;
		}

		reader.BaseStream.Seek ((long)descriptorsOffset, SeekOrigin.Begin);
		var descriptors = new List<AssemblyStoreAssemblyDescriptor> ();

		for (uint i = 0; i < Header.EntryCount; i++) {
			uint mappingIndex = reader.ReadUInt32 ();
			uint dataOffset = reader.ReadUInt32 ();
			uint dataSize = reader.ReadUInt32 ();
			uint debugDataOffset = reader.ReadUInt32 ();
			uint debugDataSize = reader.ReadUInt32 ();
			uint configDataOffset = reader.ReadUInt32 ();
			uint configDataSize = reader.ReadUInt32 ();

			var desc = new AssemblyStoreAssemblyDescriptorV3 {
				MappingIndex = mappingIndex,
				DataOffset = dataOffset,
				DataSize = dataSize,
				DebugDataOffset = debugDataOffset,
				DebugDataSize = debugDataSize,
				ConfigDataOffset = configDataOffset,
				ConfigDataSize = configDataSize,
			};
			descriptors.Add (desc);
		}

		return descriptors;
	}
}
