using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Android.Tasks;

// ExternalTypeMapObjectNode emits NativeFormat group and key hashtables, with blob-relative
// offsets and UTF-8 strings. Type references are fixup indices, not native pointers.
sealed class NativeAotTypeMapReader (byte [] data)
{
	static readonly UTF8Encoding Utf8 = new UTF8Encoding (false, true);

	public HashSet<uint> ReadGroupTypeIndices ()
	{
		var indices = new HashSet<uint> ();
		foreach (int groupOffset in ReadHashtable (0)) {
			int offset = groupOffset;
			indices.Add (ReadUnsigned (ref offset));
		}
		return indices;
	}

	public void ReadKeys (ISet<string> keys, ISet<uint> javaGroups)
	{
		var groups = new HashSet<int> ();
		var entries = new HashSet<int> ();
		foreach (int groupOffset in ReadHashtable (0)) {
			if (!groups.Add (groupOffset)) {
				continue;
			}
			int offset = groupOffset;
			uint groupTypeIndex = ReadUnsigned (ref offset);
			if (!javaGroups.Contains (groupTypeIndex)) {
				continue;
			}
			if (ReadUnsigned (ref offset) != 1) {
				throw new BadImageFormatException ("The NativeAOT object contains an invalid external type map.");
			}

			foreach (int entryOffset in ReadHashtable (offset)) {
				if (!entries.Add (entryOffset)) {
					continue;
				}
				int entry = entryOffset;
				uint length = ReadUnsigned (ref entry);
				RequireRange (entry, length);
				string key = Utf8.GetString (data, entry, (int) length);
				entry += (int) length;
				ReadUnsigned (ref entry); // Target type's common-fixup index.

				key = TypeMapKey.NormalizeAliasKey (key);
				if (!TypeMapClassName.TryGetClassName (key, out string? className)) {
					throw new BadImageFormatException ($"Invalid NativeAOT type map class name '{key}'.");
				}
				if (className != null) {
					keys.Add (className);
				}
			}
		}
	}

	IEnumerable<int> ReadHashtable (int offset)
	{
		byte header = ReadByte (ref offset);
		int indexSize = header & 3;
		int bucketShift = header >> 2;
		if (indexSize == 3 || bucketShift >= 31) {
			throw new BadImageFormatException ("Invalid NativeFormat hashtable header.");
		}

		int bucketCount = 1 << bucketShift;
		int indexWidth = 1 << indexSize;
		long indexBytes = ((long) bucketCount + 1) * indexWidth;
		RequireRange (offset, indexBytes);
		int baseOffset = offset;
		uint start = ReadIndex (baseOffset, indexWidth);
		if (start < indexBytes) {
			throw new BadImageFormatException ("NativeFormat bucket entries overlap the index table.");
		}

		for (int bucket = 0; bucket < bucketCount; bucket++) {
			uint end = ReadIndex (baseOffset + (bucket + 1) * indexWidth, indexWidth);
			if (end < start) {
				throw new BadImageFormatException ("NativeFormat bucket offsets are not ordered.");
			}
			RequireRange (baseOffset, end);
			int position = baseOffset + (int) start;
			int limit = baseOffset + (int) end;
			while (position < limit) {
				ReadByte (ref position); // Low hash byte; enumeration does not need the hash.
				int relativeTo = position;
				int relativeOffset = ReadSigned (ref position);
				if (position > limit) {
					throw new BadImageFormatException ("A NativeFormat entry extends past its bucket.");
				}
				long target = (long) relativeTo + relativeOffset;
				RequireRange (target, 1);
				yield return (int) target;
			}
			start = end;
		}
	}

	uint ReadIndex (int offset, int width)
	{
		RequireRange (offset, width);
		return width switch {
			1 => data [offset],
			2 => BinaryPrimitives.ReadUInt16LittleEndian (data.AsSpan (offset, 2)),
			_ => BinaryPrimitives.ReadUInt32LittleEndian (data.AsSpan (offset, 4)),
		};
	}

	uint ReadUnsigned (ref int offset) => ReadInteger (ref offset, out _);

	int ReadSigned (ref int offset)
	{
		uint value = ReadInteger (ref offset, out int bits);
		int shift = 32 - bits;
		return unchecked ((int) (value << shift)) >> shift;
	}

	uint ReadInteger (ref int offset, out int bits)
	{
		byte first = ReadByte (ref offset);
		int extraBytes = 0;
		while ((first & (1 << extraBytes)) != 0 && extraBytes < 5) {
			extraBytes++;
		}
		if (extraBytes == 5) {
			throw new BadImageFormatException ("Invalid NativeFormat integer encoding.");
		}
		RequireRange (offset, extraBytes);
		if (extraBytes == 4) {
			uint value = BinaryPrimitives.ReadUInt32LittleEndian (data.AsSpan (offset, 4));
			offset += 4;
			bits = 32;
			return value;
		}

		uint encoded = first;
		for (int i = 1; i <= extraBytes; i++) {
			encoded |= (uint) data [offset++] << (8 * i);
		}
		bits = 7 * (extraBytes + 1);
		return encoded >> (extraBytes + 1);
	}

	byte ReadByte (ref int offset)
	{
		RequireRange (offset, 1);
		return data [offset++];
	}

	void RequireRange (long offset, long length)
	{
		if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset) {
			throw new BadImageFormatException ("A NativeFormat value extends outside the external type map blob.");
		}
	}
}
