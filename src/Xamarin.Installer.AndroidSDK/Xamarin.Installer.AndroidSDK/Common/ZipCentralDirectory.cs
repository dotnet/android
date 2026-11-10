using System;
using System.IO;
using System.Text;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	/// <summary>
	/// Reads the per-entry metadata that <see cref="System.IO.Compression.ZipArchiveEntry"/> does not
	/// expose on netstandard2.0 (host system, external attributes and extended timestamps) directly
	/// from the ZIP central directory. Entries are returned in central directory order, which is the
	/// same order <see cref="System.IO.Compression.ZipArchive.Entries"/> uses.
	/// </summary>
	sealed class ZipCentralDirectory
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
		const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
		const uint CentralHeaderSignature = 0x02014b50;
		const int EndOfCentralDirectorySize = 22;
		const int Zip64LocatorSize = 20;
		const int CentralHeaderSize = 46;
		const int MaxCommentSize = 0xffff;

		const int HostSystemMsdos = 0;
		const int HostSystemWindowsNT = 10;
		const int UnicodeTextFlag = 0x0800;

		static readonly int systemDefaultCodePage = GetSystemDefaultCodePage ();

		public sealed class Entry
		{
			public string Name { get; set; }
			public int VersionMadeBy { get; set; }
			public int ExternalFileAttributes { get; set; }
			public ulong Size { get; set; }
			public ulong CompressedSize { get; set; }
			public DateTime DateTime { get; set; }

			public int HostSystem => (VersionMadeBy >> 8) & 0xff;

			public bool IsDOSEntry => HostSystem == HostSystemMsdos || HostSystem == HostSystemWindowsNT;

			public bool IsDirectory =>
				(Name.Length > 0 && (Name [Name.Length - 1] == '/' || Name [Name.Length - 1] == '\\')) ||
				(IsDOSEntry && (ExternalFileAttributes & 0x10) == 0x10);
		}

		public static Entry [] Read (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (!stream.CanSeek)
				throw new ArgumentException ("Stream must be seekable.", nameof (stream));

			long eocdPosition = FindEndOfCentralDirectory (stream);
			byte [] eocd = ReadExactly (stream, eocdPosition, EndOfCentralDirectorySize);

			ulong entryCount = ReadUInt16 (eocd, 10);
			ulong centralDirectorySize = ReadUInt32 (eocd, 12);
			ulong centralDirectoryOffset = ReadUInt32 (eocd, 16);
			bool isZip64 = entryCount == 0xffff || centralDirectorySize == 0xffffffff || centralDirectoryOffset == 0xffffffff;

			long firstEntryOffset = 0;
			if (isZip64) {
				if (eocdPosition < Zip64LocatorSize)
					throw new InvalidDataException ("Cannot find the ZIP64 end of central directory locator.");
				byte [] locator = ReadExactly (stream, eocdPosition - Zip64LocatorSize, Zip64LocatorSize);
				if (ReadUInt32 (locator, 0) != Zip64EndOfCentralDirectoryLocatorSignature)
					throw new InvalidDataException ("Cannot find the ZIP64 end of central directory locator.");
				long zip64EocdPosition = CheckedOffset (ReadUInt64 (locator, 8), stream.Length);
				byte [] zip64Eocd = ReadExactly (stream, zip64EocdPosition, 56);
				if (ReadUInt32 (zip64Eocd, 0) != Zip64EndOfCentralDirectorySignature)
					throw new InvalidDataException ("Invalid ZIP64 end of central directory record.");
				entryCount = ReadUInt64 (zip64Eocd, 32);
				centralDirectorySize = ReadUInt64 (zip64Eocd, 40);
				centralDirectoryOffset = ReadUInt64 (zip64Eocd, 48);
			} else if (centralDirectorySize <= (ulong) eocdPosition && centralDirectoryOffset < (ulong) eocdPosition - centralDirectorySize) {
				// Self-extracting or otherwise prefixed archive whose offsets were not adjusted.
				firstEntryOffset = eocdPosition - (long) (centralDirectorySize + centralDirectoryOffset);
			}

			if (centralDirectorySize > (ulong) stream.Length || centralDirectoryOffset > (ulong) stream.Length)
				throw new InvalidDataException ("Invalid ZIP central directory location.");
			long centralDirectoryPosition = CheckedOffset ((ulong) firstEntryOffset + centralDirectoryOffset, stream.Length);
			if ((ulong) centralDirectoryPosition + centralDirectorySize > (ulong) stream.Length)
				throw new InvalidDataException ("Invalid ZIP central directory location.");
			// Every central directory header takes at least 46 bytes, which bounds the entry array size.
			if (entryCount > centralDirectorySize / CentralHeaderSize)
				throw new InvalidDataException ("Invalid ZIP central directory entry count.");

			byte [] centralDirectory = ReadExactly (stream, centralDirectoryPosition, checked ((int) centralDirectorySize));
			var entries = new Entry [entryCount];
			int position = 0;
			for (ulong i = 0; i < entryCount; i++) {
				EnsureAvailable (centralDirectory, position, CentralHeaderSize);
				if (ReadUInt32 (centralDirectory, position) != CentralHeaderSignature)
					throw new InvalidDataException ("Wrong Central Directory signature.");

				int versionMadeBy = ReadUInt16 (centralDirectory, position + 4);
				int flags = ReadUInt16 (centralDirectory, position + 8);
				uint dosTime = ReadUInt32 (centralDirectory, position + 12);
				ulong compressedSize = ReadUInt32 (centralDirectory, position + 20);
				ulong size = ReadUInt32 (centralDirectory, position + 24);
				int nameLength = ReadUInt16 (centralDirectory, position + 28);
				int extraLength = ReadUInt16 (centralDirectory, position + 30);
				int commentLength = ReadUInt16 (centralDirectory, position + 32);
				uint externalAttributes = ReadUInt32 (centralDirectory, position + 38);
				position += CentralHeaderSize;

				EnsureAvailable (centralDirectory, position, nameLength + extraLength + commentLength);
				string name = GetEncoding (flags).GetString (centralDirectory, position, nameLength);
				position += nameLength;

				DateTime dateTime = FromDosTime (dosTime);
				ProcessExtraData (centralDirectory, position, extraLength, ref size, ref compressedSize, ref dateTime);
				position += extraLength + commentLength;

				entries [i] = new Entry {
					Name = name,
					VersionMadeBy = versionMadeBy,
					ExternalFileAttributes = unchecked ((int) externalAttributes),
					Size = size,
					CompressedSize = compressedSize,
					DateTime = dateTime,
				};
			}

			return entries;
		}

		static long FindEndOfCentralDirectory (Stream stream)
		{
			if (stream.Length < EndOfCentralDirectorySize)
				throw new InvalidDataException ("Cannot find central directory.");

			int searchLength = (int) Math.Min (stream.Length, EndOfCentralDirectorySize + MaxCommentSize);
			long searchStart = stream.Length - searchLength;
			byte [] tail = ReadExactly (stream, searchStart, searchLength);
			for (int i = searchLength - EndOfCentralDirectorySize; i >= 0; i--) {
				if (ReadUInt32 (tail, i) == EndOfCentralDirectorySignature)
					return searchStart + i;
			}
			throw new InvalidDataException ("Cannot find central directory.");
		}

		// Mirrors the central directory extra data handling of SharpZipLib 1.3.3 (ZIP64 sizes and
		// the extended Unix timestamp; NTFS timestamps are intentionally ignored like Info-ZIP does).
		static void ProcessExtraData (byte [] data, int start, int length, ref ulong size, ref ulong compressedSize, ref DateTime dateTime)
		{
			if (TryFindExtraField (data, start, length, 0x0001, out int zip64Start, out int zip64Length)) {
				if (zip64Length < 4)
					throw new InvalidDataException ("Extra data extended Zip64 information length is invalid.");
				int zip64Position = zip64Start;
				if (size == uint.MaxValue) {
					EnsureFieldAvailable (zip64Position, 8, zip64Start + zip64Length);
					size = ReadUInt64 (data, zip64Position);
					zip64Position += 8;
				}
				if (compressedSize == uint.MaxValue) {
					EnsureFieldAvailable (zip64Position, 8, zip64Start + zip64Length);
					compressedSize = ReadUInt64 (data, zip64Position);
				}
			}

			if (TryFindExtraField (data, start, length, 0x5455, out int timeStart, out int timeLength) && timeLength >= 1) {
				const int ModificationTimeFlag = 0x01;
				if ((data [timeStart] & ModificationTimeFlag) != 0 && timeLength >= 5) {
					int seconds = unchecked ((int) ReadUInt32 (data, timeStart + 1));
					dateTime = new DateTime (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan (0, 0, 0, seconds, 0);
				}
			}
		}

		static bool TryFindExtraField (byte [] data, int start, int length, int headerId, out int valueStart, out int valueLength)
		{
			int end = start + length;
			int index = start;
			while (index < end - 3) {
				int tag = ReadUInt16 (data, index);
				int tagLength = ReadUInt16 (data, index + 2);
				index += 4;
				if (tag == headerId) {
					valueStart = index;
					valueLength = tagLength;
					return index + tagLength <= end;
				}
				index += tagLength;
			}
			valueStart = 0;
			valueLength = 0;
			return false;
		}

		static DateTime FromDosTime (uint dosTime)
		{
			uint sec = Math.Min (59, 2 * (dosTime & 0x1f));
			uint min = Math.Min (59, (dosTime >> 5) & 0x3f);
			uint hrs = Math.Min (23, (dosTime >> 11) & 0x1f);
			uint mon = Math.Max (1, Math.Min (12, (dosTime >> 21) & 0xf));
			uint year = ((dosTime >> 25) & 0x7f) + 1980;
			int day = Math.Max (1, Math.Min (DateTime.DaysInMonth ((int) year, (int) mon), (int) ((dosTime >> 16) & 0x1f)));
			return new DateTime ((int) year, (int) mon, day, (int) hrs, (int) min, (int) sec, DateTimeKind.Unspecified);
		}

		static Encoding GetEncoding (int flags)
		{
			return (flags & UnicodeTextFlag) != 0 ? Encoding.UTF8 : Encoding.GetEncoding (systemDefaultCodePage);
		}

		static int GetSystemDefaultCodePage ()
		{
			const int FallbackCodePage = 437;
			try {
				int platformCodePage = Encoding.GetEncoding (0).CodePage;
				return (platformCodePage == 1 || platformCodePage == 2 || platformCodePage == 3 || platformCodePage == 42) ? FallbackCodePage : platformCodePage;
			} catch {
				return FallbackCodePage;
			}
		}

		static byte [] ReadExactly (Stream stream, long position, int count)
		{
			var buffer = new byte [count];
			stream.Seek (position, SeekOrigin.Begin);
			int offset = 0;
			while (offset < count) {
				int read = stream.Read (buffer, offset, count - offset);
				if (read <= 0)
					throw new EndOfStreamException ("Unexpected end of ZIP archive.");
				offset += read;
			}
			return buffer;
		}

		static long CheckedOffset (ulong offset, long streamLength)
		{
			if (offset > (ulong) streamLength)
				throw new InvalidDataException ("Invalid ZIP offset.");
			return (long) offset;
		}

		static void EnsureAvailable (byte [] data, int position, int count)
		{
			if (position < 0 || count < 0 || position > data.Length - count)
				throw new InvalidDataException ("Unexpected end of ZIP central directory.");
		}

		static void EnsureFieldAvailable (int position, int count, int end)
		{
			if (position + count > end)
				throw new InvalidDataException ("Extra data extended Zip64 information length is invalid.");
		}

		static ushort ReadUInt16 (byte [] data, int position) =>
			(ushort) (data [position] | (data [position + 1] << 8));

		static uint ReadUInt32 (byte [] data, int position) =>
			(uint) (data [position] | (data [position + 1] << 8) | (data [position + 2] << 16) | (data [position + 3] << 24));

		static ulong ReadUInt64 (byte [] data, int position) =>
			ReadUInt32 (data, position) | ((ulong) ReadUInt32 (data, position + 4) << 32);
	}
}
