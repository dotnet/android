using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApplicationUtility;

class AndroidManifestStringBlock
{
	const uint FlagSorted = 1 << 0;
	const uint FlagUTF8   = 1 << 0;

	ARSCHeader header;
	uint stringCount;
	uint stringsOffset;
	uint flags;
	bool isUTF8;
	List<uint> stringOffsets;
	byte[] chars;
	Dictionary<uint, string> stringCache;

	public uint StringCount => stringCount;
	public bool IsUTF8 => isUTF8;

	public AndroidManifestStringBlock (Stream data, ARSCHeader stringPoolHeader)
	{
		header = stringPoolHeader;

		using var reader = Utilities.GetReaderAndRewindStream (data);

		stringCount = reader.ReadUInt32 ();
		uint styleCount = reader.ReadUInt32 ();

		flags = reader.ReadUInt32 ();
		isUTF8 = (flags & FlagUTF8) == FlagUTF8;

		stringsOffset = reader.ReadUInt32 ();
		uint stylesOffset = reader.ReadUInt32 ();

		if (styleCount == 0 && stylesOffset > 0) {
			Log.Info ("Styles Offset given, but styleCount is zero. This is not a problem but could indicate packers.");
		}

		stringOffsets = new List<uint> ();

		for (uint i = 0; i < stringCount; i++) {
			stringOffsets.Add (reader.ReadUInt32 ());
		}

		// We're not interested in styles, skip over their offsets
		for (uint i = 0; i < styleCount; i++) {
			reader.ReadUInt32 ();
		}

		bool haveStyles = stylesOffset != 0 && styleCount != 0;
		uint size = header.Size - stringsOffset;
		if (haveStyles) {
			size = stylesOffset - stringsOffset;
		}

		if (size % 4 != 0) {
			Log.Warning ("Size of strings is not aligned on four bytes.");
		}

		chars = new byte[size];
		reader.Read (chars, 0, (int)size);

		if (haveStyles) {
			size = header.Size - stylesOffset;

			if (size % 4 != 0) {
				Log.Warning ("Size of styles is not aligned on four bytes.");
			}

			// Not interested in them, skip
			for (uint i = 0; i < size / 4; i++) {
				reader.ReadUInt32 ();
			}
		}

		stringCache = new Dictionary<uint, string> ();
	}

	public string? GetString (uint idx)
	{
		if (stringCache.TryGetValue (idx, out string? ret)) {
			return ret;
		}

		if (idx < 0 || idx > stringOffsets.Count || stringOffsets.Count == 0) {
			return null;
		}

		uint offset = stringOffsets[(int)idx];
		if (isUTF8) {
			ret = DecodeUTF8 (offset);
		} else {
			ret = DecodeUTF16 (offset);
		}
		stringCache[idx] = ret;

		return ret;
	}

	string DecodeUTF8 (uint offset)
	{
		// UTF-8 Strings contain two lengths, as they might differ:
		// 1) the string length in characters
		(uint length, uint nbytes) = DecodeLength (offset, sizeOfChar: 1);
		offset += nbytes;

		// 2) the number of bytes the encoded string occupies
		(uint encodedBytes, nbytes) = DecodeLength (offset, sizeOfChar: 1);
		offset += nbytes;

		if (chars[offset + encodedBytes] != 0) {
			throw new InvalidDataException ($"UTF-8 string is not NUL-terminated. Offset: offset");
		}

		return Encoding.UTF8.GetString (chars, (int)offset, (int)encodedBytes);
	}

	string DecodeUTF16 (uint offset)
	{
		(uint length, uint nbytes) = DecodeLength (offset, sizeOfChar: 2);
		offset += nbytes;

		uint encodedBytes = length * 2;
		if (chars[offset + encodedBytes] != 0 && chars[offset + encodedBytes + 1] != 0) {
			throw new InvalidDataException ($"UTF-16 string is not NUL-terminated. Offset: offset");
		}

		return Encoding.Unicode.GetString (chars, (int)offset, (int)encodedBytes);
	}

	(uint length, uint nbytes) DecodeLength (uint offset, uint sizeOfChar)
	{
		uint sizeOfTwoChars = sizeOfChar << 1;
		uint highBit = 0x80u << (8 * ((int)sizeOfChar - 1));
		uint length1, length2;

		// Length is tored as 1 or 2 characters of `sizeofChar` size
		if (sizeOfChar == 1) {
			// UTF-8 encoding, each character is a byte
			length1 = chars[offset];
			length2 = chars[offset + 1];
		} else {
			// UTF-16 encoding, each character is a short
			length1 = (uint)((chars[offset]) | (chars[offset + 1] << 8));
			length2 = (uint)((chars[offset + 2]) | (chars[offset + 3] << 8));
		}

		uint length;
		uint nbytes;
		if ((length1 & highBit) != 0) {
			length = ((length1 & ~highBit) << (8 * (int)sizeOfChar)) | length2;
			nbytes = sizeOfTwoChars;
		} else {
			length = length1;
			nbytes = sizeOfChar;
		}

		// 8 bit strings: maximum of 0x7FFF bytes, http://androidxref.com/9.0.0_r3/xref/frameworks/base/libs/androidfw/ResourceTypes.cpp#692
		// 16 bit strings: maximum of 0x7FFFFFF bytes, http://androidxref.com/9.0.0_r3/xref/frameworks/base/libs/androidfw/ResourceTypes.cpp#670
		if (sizeOfChar == 1) {
			if (length > 0x7fff) {
				throw new InvalidDataException ("UTF-8 string is too long. Offset: {offset}");
			}
		} else {
			if (length > 0x7fffffff) {
				throw new InvalidDataException ("UTF-16 string is too long. Offset: {offset}");
			}
		}

		return (length, nbytes);
	}
}
