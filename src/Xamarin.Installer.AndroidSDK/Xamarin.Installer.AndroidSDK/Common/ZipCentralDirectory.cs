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

				ulong centralDirectorySize = ReadUInt32 (endOfCentralDirectory, 12);
				ulong centralDirectoryOffset = ReadUInt32 (endOfCentralDirectory, 16);
				if (centralDirectorySize == uint.MaxValue || centralDirectoryOffset == uint.MaxValue)
					ReadZip64CentralDirectoryLocation (reader, endOfCentralDirectoryPosition, out centralDirectoryOffset, out centralDirectorySize);

				if (centralDirectoryOffset > (ulong) stream.Length || centralDirectorySize > (ulong) stream.Length - centralDirectoryOffset)
					throw new InvalidDataException ("Invalid ZIP central directory location.");
				long centralDirectoryEnd = checked ((long) (centralDirectoryOffset + centralDirectorySize));
				if (centralDirectoryEnd > endOfCentralDirectoryPosition)
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
				if (ReadUInt32 (tail, i) == EndOfCentralDirectorySignature &&
					i + EndOfCentralDirectorySize + ReadUInt16 (tail, i + 20) == tail.Length)
					return searchStart + i;
			}

			throw new InvalidDataException ("Cannot find ZIP central directory.");
		}

		static void ReadZip64CentralDirectoryLocation (BinaryReader reader, long endOfCentralDirectoryPosition, out ulong centralDirectoryOffset, out ulong centralDirectorySize)
		{
			if (endOfCentralDirectoryPosition < Zip64EndOfCentralDirectoryLocatorSize)
				throw new InvalidDataException ("Cannot find ZIP64 central directory locator.");

			reader.BaseStream.Position = endOfCentralDirectoryPosition - Zip64EndOfCentralDirectoryLocatorSize;
			byte [] locator = ReadExactly (reader, Zip64EndOfCentralDirectoryLocatorSize);
			if (ReadUInt32 (locator, 0) != Zip64EndOfCentralDirectoryLocatorSignature)
				throw new InvalidDataException ("Cannot find ZIP64 central directory locator.");

			ulong zip64EndOfCentralDirectoryOffset = ReadUInt64 (locator, 8);
			if (reader.BaseStream.Length < 56 || zip64EndOfCentralDirectoryOffset > (ulong) reader.BaseStream.Length - 56)
				throw new InvalidDataException ("Invalid ZIP64 central directory location.");
			reader.BaseStream.Position = (long) zip64EndOfCentralDirectoryOffset;
			byte [] zip64EndOfCentralDirectory = ReadExactly (reader, 56);
			if (ReadUInt32 (zip64EndOfCentralDirectory, 0) != Zip64EndOfCentralDirectorySignature ||
				ReadUInt64 (zip64EndOfCentralDirectory, 4) < 44)
				throw new InvalidDataException ("Invalid ZIP64 end of central directory record.");

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
