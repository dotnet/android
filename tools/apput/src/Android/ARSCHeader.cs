using System;
using System.IO;

namespace ApplicationUtility;

class ARSCHeader
{
	// This is the minimal size such a header must have. There might be other header data too!
	const long MinimumSize = 2 + 2 + 4;

	readonly long start;
	readonly uint size;
	readonly ushort type;
	readonly ushort headerSize;
	readonly bool unknownType;

	public AndroidManifestChunkType Type    => unknownType ? AndroidManifestChunkType.Null : (AndroidManifestChunkType)type;
	public ushort TypeRaw    => type;
	public ushort HeaderSize => headerSize;
	public uint Size         => size;
	public long End          => start + (long)size;

	public ARSCHeader (Stream data, AndroidManifestChunkType? expectedType = null)
	{
		start = data.Position;
		if (data.Length < start + MinimumSize) {
			throw new InvalidDataException ($"Input data not large enough. Offset: {start}");
		}

		// Data in AXML is little-endian, which is fortuitous as that's the only format BinaryReader understands.
		using BinaryReader reader = Utilities.GetReaderAndRewindStream (data, rewindStream: false);

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

		unknownType = !Enum.IsDefined (typeof(AndroidManifestChunkType), type);

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
