using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using Xamarin.Android.Utilities;

namespace Xamarin.Android.Debug;

enum ChunkType : ushort
{
	Null              = 0x0000,
	StringPool        = 0x0001,
	Table             = 0x0002,
	Xml               = 0x0003,

	XmlFirstChunk     = 0x0100,
	XmlStartNamespace = 0x0100,
	XmlEndNamespace   = 0x0101,
	XmlStartElement   = 0x0102,
	XmlEndElement     = 0x0103,
	XmlCData          = 0x0104,
	XmlLastChunk      = 0x017f,
	XmlResourceMap    = 0x0180,

	TablePackage      = 0x0200,
	TableType         = 0x0201,
	TableTypeSpec     = 0x0202,
	TableLibrary      = 0x0203,
}

//
// Based on https://github.com/androguard/androguard/tree/832104db3eb5dc3cc66b30883fa8ce8712dfa200/androguard/core/axml
//
class AXMLParser
{
	enum ParsingState
	{
		StartDocument = 0,
		EndDocument   = 1,
		StartTag      = 2,
		EndTag        = 3,
		Text          = 4,
	}

	// Position of fields inside an attribute
	const int ATTRIBUTE_IX_NAMESPACE_URI = 0;
	const int ATTRIBUTE_IX_NAME          = 1;
	const int ATTRIBUTE_IX_VALUE_STRING  = 2;
	const int ATTRIBUTE_IX_VALUE_TYPE    = 3;
	const int ATTRIBUTE_IX_VALUE_DATA    = 4;
	const int ATTRIBUTE_LENGHT           = 5;

	const long MinimumDataSize           = 8;
	const long MaximumDataSize           = (long)UInt32.MaxValue;

	readonly XamarinLoggingHelper log;

	bool axmlTampered;
	Stream data;
	long dataSize;
	ARSCHeader axmlHeader;
	uint fileSize;
	StringBlock stringPool;
	bool valid = true;
	long initialPosition;

	public bool IsValid => valid;

	public AXMLParser (Stream data, XamarinLoggingHelper logger)
	{
		log = logger;

		this.data = data;
		dataSize = data.Length;

		// Minimum is a single ARSCHeader, which would be a strange edge case...
		if (dataSize < MinimumDataSize) {
			throw new InvalidDataException ($"Input data size too small for it to be valid AXML content ({dataSize} < {MinimumDataSize})");
		}

		// This would be even stranger, if an AXML file is larger than 4GB...
		// But this is not possible as the maximum chunk size is a unsigned 4 byte int.
		if (dataSize > MaximumDataSize) {
			throw new InvalidDataException ($"Input data size too large for it to be a valid AXML content ({dataSize} > {MaximumDataSize})");
		}

		try {
			axmlHeader = new ARSCHeader (data);
		} catch (Exception) {
			log.ErrorLine ("Error parsing the first data header");
			throw;
		}

		if (axmlHeader.HeaderSize != 8) {
			throw new InvalidDataException ($"This does not look like AXML data. header size does not equal 8. header size = {axmlHeader.Size}");
		}

		fileSize = axmlHeader.Size;
		if (fileSize > dataSize) {
			throw new InvalidDataException ($"This does not look like AXML data. Declared data size does not match real size: {fileSize} vs {dataSize}");
		}

		if (fileSize < dataSize) {
			axmlTampered = true;
			log.WarningLine ($"Declared data size ({fileSize}) is smaller than total data size ({dataSize}). Was something appended to the file? Trying to parse it anyways.");
		}

		if (axmlHeader.Type != ChunkType.Xml) {
			axmlTampered = true;
			log.WarningLine ($"AXML file has an unusual resource type, trying to parse it anyways. Resource Type: 0x{(ushort)axmlHeader.Type:04x}");
		}

		ARSCHeader stringPoolHeader = new ARSCHeader (data, ChunkType.StringPool);
		if (stringPoolHeader.HeaderSize != 28) {
			throw new InvalidDataException ($"This does not look like an AXML file. String chunk header size does not equal 28. Header size = {stringPoolHeader.Size}");
		}

		stringPool = new StringBlock (logger, data, stringPoolHeader);
		initialPosition = data.Position;
	}

	public XmlDocument? Parse ()
	{
		// Reset position in case we're called more than once, for whatever reason
		data.Seek (initialPosition, SeekOrigin.Begin);
		valid = true;

		XmlDocument ret = new XmlDocument ();
		XmlDeclaration declaration = ret.CreateXmlDeclaration ("1.0", stringPool.IsUTF8 ? "UTF-8" : "UTF-16", null);
		ret.InsertBefore (declaration, ret.DocumentElement);

		using var reader = new BinaryReader (data, Encoding.UTF8, leaveOpen: true);
		ARSCHeader? header;
		while (data.Position < dataSize) {
			header = new ARSCHeader (data);

			// Special chunk: Resource Map. This chunk might follow the string pool.
			if (header.Type == ChunkType.XmlResourceMap) {
				if (!SkipOverResourceMap (header, reader)) {
					valid = false;
					break;
				}
				continue;
			}

			// XML chunks

			// Skip over unknown types
			if (!Enum.IsDefined (typeof(ChunkType), header.TypeRaw)) {
				log.WarningLine ($"Unknown chunk type 0x{header.TypeRaw:x} at offset {data.Position}. Skipping over {header.Size} bytes");
				data.Seek (header.Size, SeekOrigin.Current);
				continue;
			}

			// Check that we read a correct header
			if (header.HeaderSize != 16) {
				log.WarningLine ($"XML chunk header size is not 16. Chunk type {header.Type} (0x{header.TypeRaw:x}), chunk size {header.Size}");
				data.Seek (header.Size, SeekOrigin.Current);
				continue;
			}

			// Line Number of the source file, only used as meta information
			uint lineNumber = reader.ReadUInt32 ();

			// Comment_Index (usually 0xffffffff)
			uint commentIndex = reader.ReadUInt32 ();

			if (commentIndex != 0xffffffff && (header.Type == ChunkType.XmlStartNamespace || header.Type == ChunkType.XmlEndNamespace)) {
				log.WarningLine ($"Unhandled Comment at namespace chunk: {commentIndex}");
			}

			uint prefixIndex;
			uint uriIndex;

			if (header.Type == ChunkType.XmlStartNamespace) {
				prefixIndex = reader.ReadUInt32 ();
				uriIndex = reader.ReadUInt32 ();

				string? prefix = stringPool.GetString (prefixIndex);
				string? uri = stringPool.GetString (uriIndex);

				continue;
			}
		}

		return ret;
	}

	bool SkipOverResourceMap (ARSCHeader header, BinaryReader reader)
	{
		log.DebugLine ("AXML contains a resource map");

		// Check size: < 8 bytes mean that the chunk is not complete
		// Should be aligned to 4 bytes.
		if (header.Size < 8 || (header.Size % 4) != 0) {
			log.ErrorLine ("Invalid chunk size in chunk XML_RESOURCE_MAP");
			return false;
		}

		// Since our main interest is in reading AndroidManifest.xml, we're going to skip over the table
		for (int i = 0; i < (header.Size - header.HeaderSize) / 4; i++) {
			reader.ReadUInt32 ();
		}

		return true;
	}
}

class StringBlock
{
	const uint FlagSorted = 1 << 0;
	const uint FlagUTF8   = 1 << 0;

	XamarinLoggingHelper log;
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

	public StringBlock (XamarinLoggingHelper logger, Stream data, ARSCHeader stringPoolHeader)
	{
		log = logger;
		header = stringPoolHeader;

		using var reader = new BinaryReader (data, Encoding.UTF8, leaveOpen: true);

		stringCount = reader.ReadUInt32 ();
		uint styleCount = reader.ReadUInt32 ();

		flags = reader.ReadUInt32 ();
		isUTF8 = (flags & FlagUTF8) == FlagUTF8;

		stringsOffset = reader.ReadUInt32 ();
		uint stylesOffset = reader.ReadUInt32 ();

		if (styleCount == 0 && stylesOffset > 0) {
			log.InfoLine ("Styles Offset given, but styleCount is zero. This is not a problem but could indicate packers.");
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
			log.WarningLine ("Size of strings is not aligned on four bytes.");
		}

		chars = new byte[size];
		reader.Read (chars, 0, (int)size);

		if (haveStyles) {
			size = header.Size - stylesOffset;

			if (size % 4 != 0) {
				log.WarningLine ("Size of styles is not aligned on four bytes.");
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

class ARSCHeader
{
	// This is the minimal size such a header must have. There might be other header data too!
	const long MinimumSize = 2 + 2 + 4;

	long start;
	uint size;
	ushort type;
	ushort headerSize;
	bool unknownType;

	public ChunkType Type    => unknownType ? ChunkType.Null : (ChunkType)type;
	public ushort TypeRaw    => type;
	public ushort HeaderSize => headerSize;
	public uint Size         => size;
	public long End          => start + (long)size;

	public ARSCHeader (Stream data, ChunkType? expectedType = null)
	{
		start = data.Position;
		if (data.Length < start + MinimumSize) {
			throw new InvalidDataException ($"Input data not large enough. Offset: {start}");
		}

		// Data in AXML is little-endian, which is fortuitous as that's the only format BinaryReader understands.
		using BinaryReader reader = new BinaryReader (data, Encoding.UTF8, leaveOpen: true);

		// ushort: type
		// ushort: header_size
		// uint: size
		type = reader.ReadUInt16 ();
		headerSize = reader.ReadUInt16 ();

		// Total size of the chunk, including the header
		size = reader.ReadUInt32 ();

		if (expectedType != null && type != (ushort)expectedType) {
			throw new InvalidOperationException ($"Header type is not equal to the expected type ({expectedType}): got 0x{type:x}, expected 0x{(ushort)expectedType:x}");
		}

		unknownType = !Enum.IsDefined (typeof(ChunkType), type);

		if (headerSize < MinimumSize) {
			throw new InvalidDataException ($"Declared header size is smaller than required size of {MinimumSize}. Offset: {start}");
		}

		if (size < MinimumSize) {
			throw new InvalidDataException ($"Declared chunk size is smaller than required size of {MinimumSize}. Offset: {start}");
		}

		if (size < headerSize) {
			throw new InvalidDataException ($"Declared chunk size ({size}) is smaller than header size ({headerSize})! Offset: {start}");
		}
	}
}
