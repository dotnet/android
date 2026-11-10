using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	// ZipArchiveEntry does not expose extended timestamps or creator-platform attributes.
	static class ZipCentralDirectory
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
		const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
		const uint CentralDirectoryHeaderSignature = 0x02014b50;
		const int EndOfCentralDirectorySize = 22;
		const int Zip64EndOfCentralDirectoryLocatorSize = 20;
		const int CentralDirectoryHeaderSize = 46;
		const int MaxCommentSize = ushort.MaxValue;

		const int HostSystemMsdos = 0;
		const int HostSystemWindowsNT = 10;

		public sealed class Entry
		{
			public int ExternalFileAttributes { get; set; }
			public DateTime? LastWriteTime { get; set; }
			public bool IsDosEntry { get; set; }
			public bool IsUnixSymlink { get; set; }

			public bool IsDirectory (string name) =>
				(name.Length > 0 && (name [name.Length - 1] == '/' || name [name.Length - 1] == '\\')) ||
				(IsDosEntry && (ExternalFileAttributes & 0x10) != 0);
		}

		public static Entry [] Read (Stream stream, int expectedEntryCount)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (!stream.CanRead || !stream.CanSeek)
				throw new ArgumentException ("ZIP archive stream must be readable and seekable.", nameof (stream));

			using (var reader = new BinaryReader (stream, System.Text.Encoding.UTF8, leaveOpen: true)) {
				long endOfCentralDirectoryPosition = FindEndOfCentralDirectory (reader);
				reader.BaseStream.Position = endOfCentralDirectoryPosition;
				byte [] endOfCentralDirectory = ReadExactly (reader, EndOfCentralDirectorySize);

				ushort diskNumber = ReadUInt16 (endOfCentralDirectory, 4);
				ushort directoryDisk = ReadUInt16 (endOfCentralDirectory, 6);
				ushort entriesOnDisk = ReadUInt16 (endOfCentralDirectory, 8);
				ushort entryCount = ReadUInt16 (endOfCentralDirectory, 10);
				uint size32 = ReadUInt32 (endOfCentralDirectory, 12);
				uint offset32 = ReadUInt32 (endOfCentralDirectory, 16);
				ulong centralDirectorySize = size32;
				ulong centralDirectoryOffset = offset32;
				long directoryEndLimit = endOfCentralDirectoryPosition;

				bool hasZip64Locator = false;
				if (endOfCentralDirectoryPosition >= Zip64EndOfCentralDirectoryLocatorSize) {
					stream.Position = endOfCentralDirectoryPosition - Zip64EndOfCentralDirectoryLocatorSize;
					hasZip64Locator = reader.ReadUInt32 () == Zip64EndOfCentralDirectoryLocatorSignature;
				}
				if (hasZip64Locator && (diskNumber == ushort.MaxValue || entryCount == ushort.MaxValue ||
					size32 == uint.MaxValue || offset32 == uint.MaxValue)) {
					ReadZip64CentralDirectoryLocation (reader, endOfCentralDirectoryPosition,
						out centralDirectoryOffset, out centralDirectorySize, out ulong zip64EntryCount, out directoryEndLimit);
					if ((diskNumber != 0 && diskNumber != ushort.MaxValue) ||
						(directoryDisk != 0 && directoryDisk != ushort.MaxValue) ||
						(entriesOnDisk != ushort.MaxValue && entriesOnDisk != zip64EntryCount) ||
						(entryCount != ushort.MaxValue && entryCount != zip64EntryCount) ||
						(size32 != uint.MaxValue && size32 != centralDirectorySize) ||
						(offset32 != uint.MaxValue && offset32 != centralDirectoryOffset) ||
						zip64EntryCount != (ulong) expectedEntryCount)
						throw new InvalidDataException ("ZIP and ZIP64 central directory records do not agree.");
				} else if (diskNumber != 0 || directoryDisk != 0 ||
					entriesOnDisk != entryCount || entryCount != expectedEntryCount ||
					size32 == uint.MaxValue || offset32 == uint.MaxValue) {
					throw new InvalidDataException ("Invalid ZIP central directory record.");
				}

				if (centralDirectoryOffset > (ulong) stream.Length || centralDirectorySize > (ulong) stream.Length - centralDirectoryOffset)
					throw new InvalidDataException ("Invalid ZIP central directory location.");
				long centralDirectoryEnd = checked ((long) (centralDirectoryOffset + centralDirectorySize));
				if (centralDirectoryEnd > directoryEndLimit)
					throw new InvalidDataException ("ZIP central directory overlaps its end record.");

				reader.BaseStream.Position = (long) centralDirectoryOffset;
				var entries = new List<Entry> ();
				while (reader.BaseStream.Position < centralDirectoryEnd) {
					if (centralDirectoryEnd - reader.BaseStream.Position < CentralDirectoryHeaderSize)
						throw new InvalidDataException ("Unexpected end of ZIP central directory.");
					if (entries.Count == expectedEntryCount)
						throw new InvalidDataException ("ZIP central directory contains too many entries.");

					byte [] header = ReadExactly (reader, CentralDirectoryHeaderSize);
					if (ReadUInt32 (header, 0) != CentralDirectoryHeaderSignature)
						throw new InvalidDataException ("Invalid ZIP central directory entry.");

					int versionMadeBy = ReadUInt16 (header, 4);
					int nameLength = ReadUInt16 (header, 28);
					int extraLength = ReadUInt16 (header, 30);
					int commentLength = ReadUInt16 (header, 32);
					int externalFileAttributes = unchecked ((int) ReadUInt32 (header, 38));
					long variableDataLength = (long) nameLength + extraLength + commentLength;
					if (variableDataLength > centralDirectoryEnd - reader.BaseStream.Position)
						throw new InvalidDataException ("Unexpected end of ZIP central directory entry.");

					reader.BaseStream.Seek (nameLength, SeekOrigin.Current);
					byte [] extraData = ReadExactly (reader, extraLength);
					reader.BaseStream.Seek (commentLength, SeekOrigin.Current);

					int hostSystem = (versionMadeBy >> 8) & 0xff;
					entries.Add (new Entry {
						ExternalFileAttributes = externalFileAttributes,
						LastWriteTime = ReadExtendedModificationTime (extraData),
						IsDosEntry = hostSystem == HostSystemMsdos || hostSystem == HostSystemWindowsNT,
						IsUnixSymlink = hostSystem == 3 && ((externalFileAttributes >> 16) & 0xf000) == 0xa000,
					});
				}

				if (reader.BaseStream.Position != centralDirectoryEnd || entries.Count != expectedEntryCount)
					throw new InvalidDataException ("ZIP central directory does not match the archive entries.");

				return entries.ToArray ();
			}
		}

		static long FindEndOfCentralDirectory (BinaryReader reader)
		{
			Stream stream = reader.BaseStream;
			if (stream.Length < EndOfCentralDirectorySize)
				throw new InvalidDataException ("Cannot find ZIP central directory.");

			int searchLength = (int) Math.Min (stream.Length, EndOfCentralDirectorySize + MaxCommentSize);
			long searchStart = stream.Length - searchLength;
			stream.Position = searchStart;
			byte [] tail = ReadExactly (reader, searchLength);
			for (int i = searchLength - EndOfCentralDirectorySize; i >= 0; i--) {
				if (ReadUInt32 (tail, i) == EndOfCentralDirectorySignature) {
					if (i + EndOfCentralDirectorySize + ReadUInt16 (tail, i + 20) != tail.Length)
						throw new InvalidDataException ("Ambiguous ZIP end of central directory record.");
					return searchStart + i;
				}
			}

			throw new InvalidDataException ("Cannot find ZIP central directory.");
		}

		static void ReadZip64CentralDirectoryLocation (BinaryReader reader, long endOfCentralDirectoryPosition,
			out ulong centralDirectoryOffset, out ulong centralDirectorySize, out ulong entryCount, out long recordPosition)
		{
			if (endOfCentralDirectoryPosition < Zip64EndOfCentralDirectoryLocatorSize)
				throw new InvalidDataException ("Cannot find ZIP64 central directory locator.");

			reader.BaseStream.Position = endOfCentralDirectoryPosition - Zip64EndOfCentralDirectoryLocatorSize;
			byte [] locator = ReadExactly (reader, Zip64EndOfCentralDirectoryLocatorSize);
			if (ReadUInt32 (locator, 0) != Zip64EndOfCentralDirectoryLocatorSignature ||
				ReadUInt32 (locator, 4) != 0 || ReadUInt32 (locator, 16) != 1)
				throw new InvalidDataException ("Cannot find ZIP64 central directory locator.");

			ulong zip64EndOfCentralDirectoryOffset = ReadUInt64 (locator, 8);
			long locatorPosition = endOfCentralDirectoryPosition - Zip64EndOfCentralDirectoryLocatorSize;
			if (locatorPosition < 56 || zip64EndOfCentralDirectoryOffset > (ulong) (locatorPosition - 56))
				throw new InvalidDataException ("Invalid ZIP64 central directory location.");
			recordPosition = (long) zip64EndOfCentralDirectoryOffset;
			reader.BaseStream.Position = recordPosition;
			byte [] zip64EndOfCentralDirectory = ReadExactly (reader, 56);
			if (ReadUInt32 (zip64EndOfCentralDirectory, 0) != Zip64EndOfCentralDirectorySignature ||
				ReadUInt64 (zip64EndOfCentralDirectory, 4) != (ulong) (locatorPosition - recordPosition - 12) ||
				ReadUInt32 (zip64EndOfCentralDirectory, 16) != 0 || ReadUInt32 (zip64EndOfCentralDirectory, 20) != 0 ||
				ReadUInt64 (zip64EndOfCentralDirectory, 24) != ReadUInt64 (zip64EndOfCentralDirectory, 32))
				throw new InvalidDataException ("Invalid ZIP64 end of central directory record.");

			entryCount = ReadUInt64 (zip64EndOfCentralDirectory, 32);
			centralDirectorySize = ReadUInt64 (zip64EndOfCentralDirectory, 40);
			centralDirectoryOffset = ReadUInt64 (zip64EndOfCentralDirectory, 48);
		}

		static DateTime? ReadExtendedModificationTime (byte [] extraData)
		{
			int position = 0;
			while (position < extraData.Length) {
				if (extraData.Length - position < 4)
					throw new InvalidDataException ("Invalid ZIP entry extra data.");

				int headerId = ReadUInt16 (extraData, position);
				int dataLength = ReadUInt16 (extraData, position + 2);
				position += 4;
				if (dataLength > extraData.Length - position)
					throw new InvalidDataException ("Invalid ZIP entry extra data length.");

				if (headerId == 0x5455 && dataLength > 0 && (extraData [position] & 0x01) != 0) {
					if (dataLength < 5)
						throw new InvalidDataException ("Invalid extended ZIP timestamp.");
					int seconds = unchecked ((int) ReadUInt32 (extraData, position + 1));
					try {
						return DateTimeOffset.FromUnixTimeSeconds (seconds).UtcDateTime;
					} catch (ArgumentOutOfRangeException ex) {
						throw new InvalidDataException ("Invalid extended ZIP timestamp.", ex);
					}
				}

				position += dataLength;
			}

			return null;
		}

		static byte [] ReadExactly (BinaryReader reader, int count)
		{
			byte [] bytes = reader.ReadBytes (count);
			if (bytes.Length != count)
				throw new InvalidDataException ("Unexpected end of ZIP archive.");
			return bytes;
		}

		static ushort ReadUInt16 (byte [] data, int position) =>
			(ushort) (data [position] | (data [position + 1] << 8));

		static uint ReadUInt32 (byte [] data, int position) =>
			(uint) (data [position] | (data [position + 1] << 8) | (data [position + 2] << 16) | (data [position + 3] << 24));

		static ulong ReadUInt64 (byte [] data, int position) =>
			ReadUInt32 (data, position) | ((ulong) ReadUInt32 (data, position + 4) << 32);
	}
}
