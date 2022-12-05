using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
	}
}

class StringBlock
{
	const uint FlagSorted = 1 << 0;
	const uint FlagUTF8   = 1 << 0;

	XamarinLoggingHelper log;
	ARSCHeader header;
	uint stringCount;
	uint styleCount;
	uint stringsOffset;
	uint stylesOffset;
	uint flags;
	bool isUTF8;
	List<uint> stringOffsets;
	byte[] chars;

	public StringBlock (XamarinLoggingHelper logger, Stream data, ARSCHeader stringPoolHeader)
	{
		log = logger;
		header = stringPoolHeader;

		using var reader = new BinaryReader (data, Encoding.UTF8, leaveOpen: true);

		stringCount = reader.ReadUInt32 ();
		styleCount = reader.ReadUInt32 ();

		flags = reader.ReadUInt32 ();
		isUTF8 = (flags & FlagUTF8) == FlagUTF8;

		stringsOffset = reader.ReadUInt32 ();
		stylesOffset = reader.ReadUInt32 ();

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

	public ChunkType Type    => (ChunkType)type;
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
		size = reader.ReadUInt32 ();

		if (expectedType != null && type != (ushort)expectedType) {
			throw new InvalidOperationException ($"Header type is not equal to the expected type ({expectedType}): got 0x{type:x}, expected 0x{(ushort)expectedType:x}");
		}

		if (!Enum.IsDefined (typeof(ChunkType), type)) {
			throw new InvalidOperationException ($"Internal error: unsupported chunk type 0x{type:x}");
		}

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
