using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Android.Build.Tasks
{
	static class ZipArchiveMetadataReader
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
		const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
		const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
		const int EndOfCentralDirectoryMinimumSize = 22;
		const int Zip64EndOfCentralDirectoryLocatorSize = 20;
		const int CentralDirectoryFileHeaderSize = 46;
		const int EndOfCentralDirectorySearchWindow = ushort.MaxValue + EndOfCentralDirectoryMinimumSize;
		static readonly Encoding Cp437 = CreateCp437Encoding ();

		public static void AppendHashInput (Stream stream, StringBuilder hashInput)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (hashInput == null)
				throw new ArgumentNullException (nameof (hashInput));
			if (!stream.CanSeek)
				throw new NotSupportedException ("ZIP metadata requires a seekable stream.");

			long originalPosition = stream.Position;
			try {
				AppendHashInputCore (stream, hashInput);
			} finally {
				stream.Seek (originalPosition, SeekOrigin.Begin);
			}
		}

		static void AppendHashInputCore (Stream stream, StringBuilder hashInput)
		{
			long endOfCentralDirectoryOffset = FindEndOfCentralDirectory (stream);
			using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
			stream.Seek (endOfCentralDirectoryOffset + 4, SeekOrigin.Begin);
			ushort diskNumber = reader.ReadUInt16 ();
			ushort centralDirectoryDisk = reader.ReadUInt16 ();
			ulong entriesOnDisk = reader.ReadUInt16 ();
			ulong entryCount = reader.ReadUInt16 ();
			ulong centralDirectorySize = reader.ReadUInt32 ();
			ulong centralDirectoryOffset = reader.ReadUInt32 ();

			bool requiresZip64 =
				entriesOnDisk == ushort.MaxValue ||
				entryCount == ushort.MaxValue ||
				centralDirectorySize == uint.MaxValue ||
				centralDirectoryOffset == uint.MaxValue;
			if (requiresZip64 && TryReadZip64EndOfCentralDirectory (stream, reader, endOfCentralDirectoryOffset, out var zip64)) {
				entriesOnDisk = zip64.EntriesOnDisk;
				entryCount = zip64.EntryCount;
				centralDirectorySize = zip64.CentralDirectorySize;
				centralDirectoryOffset = zip64.CentralDirectoryOffset;
			} else if (centralDirectorySize == uint.MaxValue || centralDirectoryOffset == uint.MaxValue) {
				throw new InvalidDataException ("ZIP64 end of central directory record is missing.");
			}

			if (diskNumber != 0 || centralDirectoryDisk != 0 || entriesOnDisk != entryCount)
				throw new NotSupportedException ("Multi-disk ZIP archives are not supported.");
			if (entryCount > int.MaxValue)
				throw new InvalidDataException ("ZIP archive contains too many entries.");
			if (centralDirectoryOffset > long.MaxValue || centralDirectorySize > long.MaxValue)
				throw new InvalidDataException ("ZIP central directory is too large.");

			long centralDirectoryStart = (long) centralDirectoryOffset;
			long centralDirectoryLength = (long) centralDirectorySize;
			if (centralDirectoryStart > stream.Length || centralDirectoryLength > stream.Length - centralDirectoryStart)
				throw new InvalidDataException ("ZIP central directory exceeds the available data.");

			stream.Seek (centralDirectoryStart, SeekOrigin.Begin);
			long centralDirectoryEnd = centralDirectoryStart + centralDirectoryLength;
			ulong entriesRead = 0;
			while (entriesRead < entryCount) {
				if (stream.Position > centralDirectoryEnd - CentralDirectoryFileHeaderSize)
					throw new InvalidDataException ("ZIP central directory contains fewer entries than expected.");
				if (reader.ReadUInt32 () != CentralDirectoryFileHeaderSignature)
					throw new InvalidDataException ("Invalid ZIP central directory header.");

				reader.ReadUInt16 (); // version made by
				reader.ReadUInt16 (); // version needed to extract
				ushort flags = reader.ReadUInt16 ();
				reader.ReadUInt16 (); // compression method
				reader.ReadUInt16 (); // last mod file time
				reader.ReadUInt16 (); // last mod file date
				uint crc32 = reader.ReadUInt32 ();
				reader.ReadUInt32 (); // compressed size
				reader.ReadUInt32 (); // uncompressed size
				ushort fileNameLength = reader.ReadUInt16 ();
				ushort extraFieldLength = reader.ReadUInt16 ();
				ushort fileCommentLength = reader.ReadUInt16 ();
				reader.ReadUInt16 (); // disk number start
				reader.ReadUInt16 (); // internal file attributes
				reader.ReadUInt32 (); // external file attributes
				reader.ReadUInt32 (); // relative offset of local header

				var fileNameBytes = reader.ReadBytes (fileNameLength);
				if (fileNameBytes.Length != fileNameLength)
					throw new InvalidDataException ("ZIP central directory entry name exceeds the available data.");
				if (stream.Position > centralDirectoryEnd - extraFieldLength - fileCommentLength)
					throw new InvalidDataException ("ZIP central directory entry exceeds the available data.");
				var encoding = (flags & (1 << 11)) != 0 ? Encoding.UTF8 : Cp437;
				hashInput.AppendFormat (CultureInfo.InvariantCulture, "{0}{1}", encoding.GetString (fileNameBytes), crc32);
				stream.Seek (extraFieldLength + fileCommentLength, SeekOrigin.Current);
				entriesRead++;
			}
		}

		static bool TryReadZip64EndOfCentralDirectory (Stream stream, BinaryReader reader, long endOfCentralDirectoryOffset, out Zip64DirectoryInfo info)
		{
			info = default;
			long locatorOffset = endOfCentralDirectoryOffset - Zip64EndOfCentralDirectoryLocatorSize;
			if (locatorOffset < 0)
				return false;

			stream.Seek (locatorOffset, SeekOrigin.Begin);
			if (reader.ReadUInt32 () != Zip64EndOfCentralDirectoryLocatorSignature)
				return false;

			uint zip64DirectoryDisk = reader.ReadUInt32 ();
			ulong zip64DirectoryOffset = reader.ReadUInt64 ();
			uint diskCount = reader.ReadUInt32 ();
			if (zip64DirectoryDisk != 0 || diskCount != 1)
				throw new NotSupportedException ("Multi-disk ZIP archives are not supported.");
			if (locatorOffset < 56 || zip64DirectoryOffset > long.MaxValue || zip64DirectoryOffset > (ulong) (locatorOffset - 56))
				throw new InvalidDataException ("ZIP64 end of central directory record exceeds the available data.");

			stream.Seek ((long) zip64DirectoryOffset, SeekOrigin.Begin);
			if (reader.ReadUInt32 () != Zip64EndOfCentralDirectorySignature)
				throw new InvalidDataException ("Invalid ZIP64 end of central directory record.");

			ulong recordSize = reader.ReadUInt64 ();
			if (recordSize < 44 || recordSize > (ulong) (locatorOffset - stream.Position))
				throw new InvalidDataException ("ZIP64 end of central directory record exceeds the available data.");

			reader.ReadUInt16 (); // version made by
			reader.ReadUInt16 (); // version needed to extract
			uint diskNumber = reader.ReadUInt32 ();
			uint centralDirectoryDisk = reader.ReadUInt32 ();
			ulong entriesOnDisk = reader.ReadUInt64 ();
			ulong entryCount = reader.ReadUInt64 ();
			ulong centralDirectorySize = reader.ReadUInt64 ();
			ulong centralDirectoryOffset = reader.ReadUInt64 ();
			if (diskNumber != 0 || centralDirectoryDisk != 0 || entriesOnDisk != entryCount)
				throw new NotSupportedException ("Multi-disk ZIP archives are not supported.");

			info = new Zip64DirectoryInfo (entriesOnDisk, entryCount, centralDirectorySize, centralDirectoryOffset);
			return true;
		}

		static long FindEndOfCentralDirectory (Stream stream)
		{
			long searchLength = Math.Min (stream.Length, EndOfCentralDirectorySearchWindow);
			var buffer = new byte [searchLength];
			stream.Seek (-searchLength, SeekOrigin.End);
			ReadExactly (stream, buffer, 0, buffer.Length);

			for (int index = buffer.Length - EndOfCentralDirectoryMinimumSize; index >= 0; index--) {
				if (
					buffer [index] == 0x50 &&
					buffer [index + 1] == 0x4b &&
					buffer [index + 2] == 0x05 &&
					buffer [index + 3] == 0x06 &&
					index + EndOfCentralDirectoryMinimumSize + ReadUInt16 (buffer, index + 20) == buffer.Length
				) {
					return stream.Length - searchLength + index;
				}
			}

			throw new InvalidDataException ("Could not locate the ZIP end of central directory record.");
		}

		static ushort ReadUInt16 (byte [] buffer, int offset)
		{
			return (ushort) (buffer [offset] | (buffer [offset + 1] << 8));
		}

		static void ReadExactly (Stream stream, byte [] buffer, int offset, int count)
		{
			while (count > 0) {
				int bytesRead = stream.Read (buffer, offset, count);
				if (bytesRead == 0)
					throw new EndOfStreamException ();

				offset += bytesRead;
				count -= bytesRead;
			}
		}

		static Encoding CreateCp437Encoding ()
		{
			Encoding.RegisterProvider (CodePagesEncodingProvider.Instance);
			return Encoding.GetEncoding (437);
		}

		readonly struct Zip64DirectoryInfo
		{
			public ulong EntriesOnDisk { get; }
			public ulong EntryCount { get; }
			public ulong CentralDirectorySize { get; }
			public ulong CentralDirectoryOffset { get; }

			public Zip64DirectoryInfo (ulong entriesOnDisk, ulong entryCount, ulong centralDirectorySize, ulong centralDirectoryOffset)
			{
				EntriesOnDisk = entriesOnDisk;
				EntryCount = entryCount;
				CentralDirectorySize = centralDirectorySize;
				CentralDirectoryOffset = centralDirectoryOffset;
			}
		}
	}
}
