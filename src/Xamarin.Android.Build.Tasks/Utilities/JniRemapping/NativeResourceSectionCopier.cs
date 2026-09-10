#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Re-emits an assembly's Win32 resource directory (<c>.rsrc</c>) at whatever address the
	/// rebuilt PE places it. The directory tree is copied byte-for-byte; only the absolute RVAs
	/// stored in each <c>IMAGE_RESOURCE_DATA_ENTRY.OffsetToData</c> are relocated, since every
	/// other offset in the format is relative to the start of the directory.
	/// </summary>
	sealed class NativeResourceSectionCopier : ResourceSectionBuilder
	{
		const int ResourceDirectoryHeaderSize = 16;
		const int ResourceDirectoryEntrySize = 8;
		const int ResourceDataEntrySize = 16;
		const uint HighBit = 0x80000000;

		readonly byte [] section;
		readonly int originalRva;
		readonly HashSet<int> dataEntryRvaOffsets;

		NativeResourceSectionCopier (byte [] section, int originalRva, HashSet<int> dataEntryRvaOffsets)
		{
			this.section = section;
			this.originalRva = originalRva;
			this.dataEntryRvaOffsets = dataEntryRvaOffsets;
		}

		/// <summary>
		/// Returns null when the assembly has no Win32 resources.
		/// </summary>
		public static NativeResourceSectionCopier? TryCreate (PEReader peReader)
		{
			PEHeader? peHeader = peReader.PEHeaders.PEHeader;
			if (peHeader == null) {
				return null;
			}

			DirectoryEntry directory = peHeader.ResourceTableDirectory;
			if (directory.RelativeVirtualAddress == 0 && directory.Size == 0) {
				return null;
			}
			if (directory.RelativeVirtualAddress == 0 || directory.Size == 0) {
				throw new JniRewriteException ("The Win32 resource directory has an invalid RVA or size.");
			}

			PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
			if (block.Length < directory.Size) {
				throw new JniRewriteException ("The Win32 resource directory extends past the end of its PE section.");
			}

			byte [] section = block.GetReader (0, directory.Size).ReadBytes (directory.Size);
			var offsets = new HashSet<int> ();
			CollectDataEntryOffsets (section, directory.RelativeVirtualAddress, directoryOffset: 0, depth: 0, offsets, new HashSet<int> ());
			return new NativeResourceSectionCopier (section, directory.RelativeVirtualAddress, offsets);
		}

		protected override void Serialize (BlobBuilder builder, SectionLocation location)
		{
			var relocated = (byte []) section.Clone ();
			int delta = location.RelativeVirtualAddress - originalRva;

			foreach (int offset in dataEntryRvaOffsets) {
				uint value = ReadUInt32 (relocated, offset);
				WriteUInt32 (relocated, offset, unchecked ((uint) ((int) value + delta)));
			}

			builder.WriteBytes (relocated);
		}

		static void CollectDataEntryOffsets (byte [] section, int originalRva, int directoryOffset, int depth, HashSet<int> offsets, HashSet<int> visited)
		{
			if (depth > 8) {
				throw new JniRewriteException ("The Win32 resource directory nests more deeply than the PE format allows.");
			}
			if (!visited.Add (directoryOffset)) {
				throw new JniRewriteException ("The Win32 resource directory contains a cycle.");
			}
			RequireRange (section, directoryOffset, ResourceDirectoryHeaderSize);

			int namedEntries = ReadUInt16 (section, directoryOffset + 12);
			int idEntries = ReadUInt16 (section, directoryOffset + 14);
			int entryOffset = directoryOffset + ResourceDirectoryHeaderSize;

			for (int i = 0; i < namedEntries + idEntries; i++, entryOffset += ResourceDirectoryEntrySize) {
				RequireRange (section, entryOffset, ResourceDirectoryEntrySize);
				uint offsetToData = ReadUInt32 (section, entryOffset + 4);

				if ((offsetToData & HighBit) != 0) {
					CollectDataEntryOffsets (section, originalRva, (int) (offsetToData & ~HighBit), depth + 1, offsets, visited);
					continue;
				}

				int dataEntryOffset = (int) offsetToData;
				RequireRange (section, dataEntryOffset, ResourceDataEntrySize);
				RequireDataWithinCopiedSection (section, originalRva, dataEntryOffset);
				offsets.Add (dataEntryOffset);
			}
		}

		/// <summary>
		/// Validates that an <c>IMAGE_RESOURCE_DATA_ENTRY</c>'s data - its absolute RVA plus its
		/// size - lies entirely within the resource section this class copied. Without this
		/// check a corrupt or crafted data entry could point past the copied bytes and would only
		/// be caught (if at all) once something tried to actually read the relocated resource.
		/// </summary>
		static void RequireDataWithinCopiedSection (byte [] section, int originalRva, int dataEntryOffset)
		{
			uint dataRva = ReadUInt32 (section, dataEntryOffset);
			uint dataSize = ReadUInt32 (section, dataEntryOffset + 4);

			long relativeOffset = (long) dataRva - originalRva;
			if (relativeOffset < 0 || relativeOffset + dataSize > section.Length) {
				throw new JniRewriteException ($"A Win32 resource data entry references data (RVA 0x{dataRva:X}, {dataSize} byte(s)) outside of the copied resource section.");
			}
		}

		static void RequireRange (byte [] section, int offset, int length)
		{
			if (offset < 0 || length < 0 || offset > section.Length - length) {
				throw new JniRewriteException ("The Win32 resource directory references data outside of the resource section.");
			}
		}

		static ushort ReadUInt16 (byte [] data, int offset) => (ushort) (data [offset] | (data [offset + 1] << 8));

		static uint ReadUInt32 (byte [] data, int offset)
			=> (uint) (data [offset] | (data [offset + 1] << 8) | (data [offset + 2] << 16) | (data [offset + 3] << 24));

		static void WriteUInt32 (byte [] data, int offset, uint value)
		{
			data [offset] = (byte) value;
			data [offset + 1] = (byte) (value >> 8);
			data [offset + 2] = (byte) (value >> 16);
			data [offset + 3] = (byte) (value >> 24);
		}
	}
}
