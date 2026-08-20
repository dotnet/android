using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Android.Build.Tasks
{
	public enum ZipEntryCompressionMethod : ushort
	{
		Store = 0,
		Deflate = 8,
	}

	public readonly struct ZipEntryMetadata
	{
		public string FullName { get; }
		public uint Crc32 { get; }
		public long CompressedSize { get; }
		public long UncompressedSize { get; }
		public ZipEntryCompressionMethod CompressionMethod { get; }

		public ZipEntryMetadata (string fullName, uint crc32, long compressedSize, long uncompressedSize, ZipEntryCompressionMethod compressionMethod)
		{
			FullName = fullName;
			Crc32 = crc32;
			CompressedSize = compressedSize;
			UncompressedSize = uncompressedSize;
			CompressionMethod = compressionMethod;
		}
	}

	public static class ZipArchiveMetadataReader
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
		const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
		const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
		const ushort Zip64ExtraFieldId = 0x0001;
		const int EndOfCentralDirectoryMinimumSize = 22;
		const int Zip64EndOfCentralDirectoryLocatorSize = 20;
		const int CentralDirectoryFileHeaderSize = 46;
		const int EndOfCentralDirectorySearchWindow = ushort.MaxValue + EndOfCentralDirectoryMinimumSize;
		static readonly Encoding Cp437 = CreateCp437Encoding ();

		public static IReadOnlyDictionary<string, ZipEntryMetadata> Read (string archivePath)
		{
			if (archivePath == null)
				throw new ArgumentNullException (nameof (archivePath));

			using var stream = File.OpenRead (archivePath);
			return Read (stream);
		}

		public static IReadOnlyDictionary<string, ZipEntryMetadata> Read (Stream stream)
		{
			var entries = ReadEntries (stream);
			var metadata = new Dictionary<string, ZipEntryMetadata> (entries.Count, StringComparer.Ordinal);
			foreach (var entry in entries) {
				metadata [entry.FullName] = entry;
			}
			return metadata;
		}

		public static IReadOnlyList<ZipEntryMetadata> ReadEntries (string archivePath)
		{
			if (archivePath == null)
				throw new ArgumentNullException (nameof (archivePath));

			using var stream = File.OpenRead (archivePath);
			return ReadEntries (stream);
		}

		public static IReadOnlyList<ZipEntryMetadata> ReadEntries (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (!stream.CanSeek)
				throw new NotSupportedException ("ZIP metadata requires a seekable stream.");

			long originalPosition = stream.Position;
			try {
				return ReadEntriesCore (stream);
			} finally {
				stream.Seek (originalPosition, SeekOrigin.Begin);
			}
		}

		static IReadOnlyList<ZipEntryMetadata> ReadEntriesCore (Stream stream)
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
			var entries = new List<ZipEntryMetadata> ((int) entryCount);
			long centralDirectoryEnd = centralDirectoryStart + centralDirectoryLength;
			while ((ulong) entries.Count < entryCount) {
				if (stream.Position > centralDirectoryEnd - CentralDirectoryFileHeaderSize)
					throw new InvalidDataException ("ZIP central directory contains fewer entries than expected.");
				if (reader.ReadUInt32 () != CentralDirectoryFileHeaderSignature)
					throw new InvalidDataException ("Invalid ZIP central directory header.");

				reader.ReadUInt16 (); // version made by
				reader.ReadUInt16 (); // version needed to extract
				ushort flags = reader.ReadUInt16 ();
				ushort compressionMethod = reader.ReadUInt16 ();
				reader.ReadUInt16 (); // last mod file time
				reader.ReadUInt16 (); // last mod file date
				uint crc32 = reader.ReadUInt32 ();
				uint compressedSize = reader.ReadUInt32 ();
				uint uncompressedSize = reader.ReadUInt32 ();
				ushort fileNameLength = reader.ReadUInt16 ();
				ushort extraFieldLength = reader.ReadUInt16 ();
				ushort fileCommentLength = reader.ReadUInt16 ();
				ushort diskNumberStart = reader.ReadUInt16 ();
				reader.ReadUInt16 (); // internal file attributes
				reader.ReadUInt32 (); // external file attributes
				uint localHeaderOffset = reader.ReadUInt32 ();

				var fileNameBytes = reader.ReadBytes (fileNameLength);
				if (fileNameBytes.Length != fileNameLength)
					throw new InvalidDataException ("ZIP central directory entry name exceeds the available data.");
				var encoding = (flags & (1 << 11)) != 0 ? Encoding.UTF8 : Cp437;
				var fullName = encoding.GetString (fileNameBytes);

				if (stream.Position > centralDirectoryEnd - extraFieldLength - fileCommentLength)
					throw new InvalidDataException ("ZIP central directory entry exceeds the available data.");

				var extraField = reader.ReadBytes (extraFieldLength);
				if (extraField.Length != extraFieldLength)
					throw new InvalidDataException ("ZIP central directory extra field exceeds the available data.");
				ReadZip64Sizes (
					extraField,
					compressedSize,
					uncompressedSize,
					localHeaderOffset,
					diskNumberStart,
					out ulong zip64CompressedSize,
					out ulong zip64UncompressedSize
				);
				stream.Seek (fileCommentLength, SeekOrigin.Current);

				if (zip64CompressedSize > long.MaxValue || zip64UncompressedSize > long.MaxValue)
					throw new InvalidDataException ("ZIP entry is too large.");
				entries.Add (new ZipEntryMetadata (
					fullName,
					crc32,
					(long) zip64CompressedSize,
					(long) zip64UncompressedSize,
					(ZipEntryCompressionMethod) compressionMethod
				));
			}

			return entries;
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

		static void ReadZip64Sizes (
			byte [] extraField,
			uint compressedSize,
			uint uncompressedSize,
			uint localHeaderOffset,
			ushort diskNumberStart,
			out ulong actualCompressedSize,
			out ulong actualUncompressedSize)
		{
			actualCompressedSize = compressedSize;
			actualUncompressedSize = uncompressedSize;
			bool needsCompressedSize = compressedSize == uint.MaxValue;
			bool needsUncompressedSize = uncompressedSize == uint.MaxValue;
			bool needsLocalHeaderOffset = localHeaderOffset == uint.MaxValue;
			bool needsDiskNumberStart = diskNumberStart == ushort.MaxValue;
			if (!needsCompressedSize && !needsUncompressedSize)
				return;

			int offset = 0;
			while (offset <= extraField.Length - 4) {
				ushort headerId = ReadUInt16 (extraField, offset);
				ushort dataSize = ReadUInt16 (extraField, offset + 2);
				offset += 4;
				if (dataSize > extraField.Length - offset)
					throw new InvalidDataException ("ZIP central directory extra field is truncated.");

				if (headerId == Zip64ExtraFieldId) {
					int end = offset + dataSize;
					if (needsUncompressedSize)
						actualUncompressedSize = ReadZip64UInt64 (extraField, ref offset, end);
					if (needsCompressedSize)
						actualCompressedSize = ReadZip64UInt64 (extraField, ref offset, end);
					if (needsLocalHeaderOffset)
						ReadZip64UInt64 (extraField, ref offset, end);
					if (needsDiskNumberStart)
						ReadZip64UInt32 (extraField, ref offset, end);
					return;
				}

				offset += dataSize;
			}

			throw new InvalidDataException ("ZIP64 entry size is missing from the central directory extra field.");
		}

		static ulong ReadZip64UInt64 (byte [] buffer, ref int offset, int end)
		{
			if (offset > end - sizeof (long))
				throw new InvalidDataException ("ZIP64 central directory extra field is truncated.");

			ulong value =
				buffer [offset] |
				((ulong) buffer [offset + 1] << 8) |
				((ulong) buffer [offset + 2] << 16) |
				((ulong) buffer [offset + 3] << 24) |
				((ulong) buffer [offset + 4] << 32) |
				((ulong) buffer [offset + 5] << 40) |
				((ulong) buffer [offset + 6] << 48) |
				((ulong) buffer [offset + 7] << 56);
			offset += sizeof (long);
			return value;
		}

		static uint ReadZip64UInt32 (byte [] buffer, ref int offset, int end)
		{
			if (offset > end - sizeof (int))
				throw new InvalidDataException ("ZIP64 central directory extra field is truncated.");

			uint value =
				buffer [offset] |
				((uint) buffer [offset + 1] << 8) |
				((uint) buffer [offset + 2] << 16) |
				((uint) buffer [offset + 3] << 24);
			offset += sizeof (int);
			return value;
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
