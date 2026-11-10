// The ZIP validation in this file is derived from SharpZipLib 1.3.3 (https://github.com/icsharpcode/SharpZipLib),
// licensed under the MIT license. See THIRD-PARTY-NOTICES.TXT.
// Copyright © 2000-2018 SharpZipLib Contributors

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	/// <summary>
	/// Minimal read-only ZIP reader used by the Android SDK installer. It reads the per-entry metadata that
	/// <see cref="System.IO.Compression.ZipArchiveEntry"/> does not expose on netstandard2.0 (host system,
	/// external attributes and extended timestamps), applies the same central directory and local header
	/// validation SharpZipLib 1.3.3 applied when extracting an entry (<c>ZipFile.GetInputStream</c>), and
	/// inflates the validated entry data with <see cref="DeflateStream"/>.
	/// </summary>
	sealed class ZipCentralDirectory
	{
		const uint EndOfCentralDirectorySignature = 0x06054b50;
		const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
		const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
		const uint CentralHeaderSignature = 0x02014b50;
		const uint LocalHeaderSignature = 0x04034b50;
		const int EndOfCentralDirectorySize = 22;
		const int Zip64LocatorSize = 20;
		const int Zip64EndOfCentralDirectorySize = 56;
		const int CentralHeaderSize = 46;
		const int LocalHeaderSize = 30;
		const int MaxCommentSize = 0xffff;

		const int Zip64ExtraFieldId = 0x0001;
		const int ExtendedTimestampExtraFieldId = 0x5455;

		// Newest "version needed to extract" SharpZipLib 1.3.3 accepted (ZipConstants.VersionMadeBy).
		const int MaxVersionNeededToExtract = 51;
		const int VersionZip64 = 45;

		const int CompressionMethodStored = 0;
		const int CompressionMethodDeflated = 8;
		const int CompressionMethodBZip2 = 12;

		const int EncryptedFlag = 0x0001;
		const int DescriptorFlag = 0x0008;
		const int PatchedFlag = 0x0020;
		const int StrongEncryptionFlag = 0x0040;
		const int UnicodeTextFlag = 0x0800;
		const int EnhancedCompressFlag = 0x1000;
		const int HeaderMaskedFlag = 0x2000;

		const int HostSystemMsdos = 0;
		const int HostSystemWindowsNT = 10;
		const int DosVolumeLabelAttribute = 0x08;
		const int DosDirectoryAttribute = 0x10;

		static readonly int systemDefaultCodePage = GetSystemDefaultCodePage ();

		public sealed class Entry
		{
			public string Name { get; set; }
			public int VersionMadeBy { get; set; }
			public int Flags { get; set; }
			public int CompressionMethod { get; set; }
			public int ExternalFileAttributes { get; set; }
			public ulong Size { get; set; }
			public ulong CompressedSize { get; set; }
			public long LocalHeaderOffset { get; set; }
			public DateTime DateTime { get; set; }

			public int HostSystem => (VersionMadeBy >> 8) & 0xff;

			public bool IsDOSEntry => HostSystem == HostSystemMsdos || HostSystem == HostSystemWindowsNT;

			public bool IsDirectory =>
				(Name.Length > 0 && (Name [Name.Length - 1] == '/' || Name [Name.Length - 1] == '\\')) ||
				HasDosAttributes (DosDirectoryAttribute);

			public bool IsFile => !IsDirectory && !HasDosAttributes (DosVolumeLabelAttribute);

			public bool IsEncrypted => (Flags & EncryptedFlag) != 0;

			bool HasDosAttributes (int attributes) => IsDOSEntry && (ExternalFileAttributes & attributes) == attributes;
		}

		public static Entry [] Read (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (!stream.CanSeek)
				throw new ArgumentException ("Stream must be seekable.", nameof (stream));

			long eocdPosition = FindEndOfCentralDirectory (stream);
			byte [] eocd = ReadExactly (stream, eocdPosition, EndOfCentralDirectorySize);

			int thisDiskNumber = ReadUInt16 (eocd, 4);
			int startCentralDirectoryDisk = ReadUInt16 (eocd, 6);
			ulong entryCount = ReadUInt16 (eocd, 8);
			ulong totalEntryCount = ReadUInt16 (eocd, 10);
			ulong centralDirectorySize = ReadUInt32 (eocd, 12);
			ulong centralDirectoryOffset = ReadUInt32 (eocd, 16);
			bool requireZip64 = thisDiskNumber == 0xffff || startCentralDirectoryDisk == 0xffff ||
				entryCount == 0xffff || totalEntryCount == 0xffff ||
				centralDirectorySize == 0xffffffff || centralDirectoryOffset == 0xffffffff;

			// Like SharpZipLib, always honour a ZIP64 end of central directory locator when one is present.
			bool isZip64 = false;
			if (eocdPosition >= Zip64LocatorSize) {
				byte [] locator = ReadExactly (stream, eocdPosition - Zip64LocatorSize, Zip64LocatorSize);
				if (ReadUInt32 (locator, 0) == Zip64EndOfCentralDirectoryLocatorSignature) {
					isZip64 = true;
					long zip64EocdPosition = CheckedOffset (ReadUInt64 (locator, 8), stream.Length);
					byte [] zip64Eocd = ReadExactly (stream, zip64EocdPosition, Zip64EndOfCentralDirectorySize);
					if (ReadUInt32 (zip64Eocd, 0) != Zip64EndOfCentralDirectorySignature)
						throw new InvalidDataException ($"Invalid Zip64 Central directory signature at {zip64EocdPosition:X}.");
					entryCount = ReadUInt64 (zip64Eocd, 24);
					centralDirectorySize = ReadUInt64 (zip64Eocd, 40);
					centralDirectoryOffset = ReadUInt64 (zip64Eocd, 48);
				}
			}
			if (requireZip64 && !isZip64)
				throw new InvalidDataException ("Cannot find Zip64 locator.");

			long firstEntryOffset = 0;
			if (!isZip64 && centralDirectorySize <= (ulong) eocdPosition && centralDirectoryOffset < (ulong) eocdPosition - centralDirectorySize) {
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
				int versionNeeded = ReadUInt16 (centralDirectory, position + 6);
				int flags = ReadUInt16 (centralDirectory, position + 8);
				int method = ReadUInt16 (centralDirectory, position + 10);
				uint dosTime = ReadUInt32 (centralDirectory, position + 12);
				ulong compressedSize = ReadUInt32 (centralDirectory, position + 20);
				ulong size = ReadUInt32 (centralDirectory, position + 24);
				int nameLength = ReadUInt16 (centralDirectory, position + 28);
				int extraLength = ReadUInt16 (centralDirectory, position + 30);
				int commentLength = ReadUInt16 (centralDirectory, position + 32);
				uint externalAttributes = ReadUInt32 (centralDirectory, position + 38);
				ulong localHeaderOffset = ReadUInt32 (centralDirectory, position + 42);
				position += CentralHeaderSize;

				if (versionNeeded != 0 && versionNeeded < 10)
					throw new InvalidDataException ($"Invalid version needed to extract ({versionNeeded}).");

				EnsureAvailable (centralDirectory, position, nameLength + extraLength + commentLength);
				string name = GetEncoding (flags).GetString (centralDirectory, position, nameLength);
				position += nameLength;

				DateTime dateTime = FromDosTime (dosTime);
				ProcessExtraData (centralDirectory, position, extraLength, versionNeeded, ref size, ref compressedSize, ref localHeaderOffset, ref dateTime);
				position += extraLength + commentLength;

				entries [i] = new Entry {
					Name = name,
					VersionMadeBy = versionMadeBy,
					Flags = flags,
					CompressionMethod = method,
					ExternalFileAttributes = unchecked ((int) externalAttributes),
					Size = size,
					CompressedSize = compressedSize,
					LocalHeaderOffset = CheckedOffset ((ulong) firstEntryOffset + localHeaderOffset, stream.Length),
					DateTime = dateTime,
				};
			}

			return entries;
		}

		/// <summary>
		/// Validates the local header of <paramref name="entry"/> and returns a stream with its uncompressed data.
		/// The returned stream reads from <paramref name="stream"/>, which must not be used until it is disposed.
		/// </summary>
		public static Stream OpenEntry (Stream stream, Entry entry)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			long dataOffset = ValidateLocalHeader (stream, entry);

			// SharpZipLib extracted encrypted entries only when a password was supplied, which the installer never does.
			if (entry.IsEncrypted)
				throw new InvalidDataException ($"Entry '{entry.Name}' is encrypted and cannot be extracted.");

			long compressedSize = entry.CompressedSize > (ulong) long.MaxValue
				? throw new InvalidDataException ($"Entry '{entry.Name}' has an invalid compressed size.")
				: (long) entry.CompressedSize;

			switch (entry.CompressionMethod) {
				case CompressionMethodStored:
					return new EntryDataStream (new SubReadStream (stream, dataOffset, compressedSize, failWhenExhausted: false), entry);
				case CompressionMethodDeflated:
					// The inflater only asks for more input when the deflate stream has not ended yet, so running out of
					// compressed data is an error (SharpZipLib's "Unexpected EOF").
					var compressed = new SubReadStream (stream, dataOffset, compressedSize, failWhenExhausted: true);
					return new EntryDataStream (new DeflateStream (compressed, CompressionMode.Decompress), entry);
				default:
					throw new NotSupportedException ($"Entry '{entry.Name}' uses unsupported compression method {entry.CompressionMethod}.");
			}
		}

		// Mirrors SharpZipLib 1.3.3 ZipFile.TestLocalHeader (entry, HeaderTest.Extract), which ZipFile.GetInputStream runs
		// before extracting every entry. Returns the absolute offset of the entry data.
		static long ValidateLocalHeader (Stream stream, Entry entry)
		{
			byte [] header = ReadExactly (stream, entry.LocalHeaderOffset, LocalHeaderSize);
			uint signature = ReadUInt32 (header, 0);
			if (signature != LocalHeaderSignature)
				throw new InvalidDataException ($"Wrong local header signature at 0x{entry.LocalHeaderOffset:x}, expected 0x{LocalHeaderSignature:x8}, actual 0x{signature:x8}.");

			int extractVersion = ReadUInt16 (header, 4) & 0x00ff;
			int localFlags = ReadUInt16 (header, 6);
			long compressedSize = ReadUInt32 (header, 18);
			long size = ReadUInt32 (header, 22);
			int nameLength = ReadUInt16 (header, 26);
			int extraLength = ReadUInt16 (header, 28);

			byte [] nameAndExtra = ReadExactly (stream, entry.LocalHeaderOffset + LocalHeaderSize, nameLength + extraLength);
			long entrySize = unchecked ((long) entry.Size);
			long entryCompressedSize = unchecked ((long) entry.CompressedSize);

			if (TryFindExtraField (nameAndExtra, nameLength, extraLength, Zip64ExtraFieldId, out int zip64Start, out int zip64Length)) {
				int zip64End = zip64Start + zip64Length;
				EnsureExtraFieldAvailable (zip64Start, 8, zip64End);
				size = unchecked ((long) ReadUInt64 (nameAndExtra, zip64Start));
				EnsureExtraFieldAvailable (zip64Start + 8, 8, zip64End);
				compressedSize = unchecked ((long) ReadUInt64 (nameAndExtra, zip64Start + 8));

				if ((localFlags & DescriptorFlag) != 0) {
					if (size != -1 && size != entrySize)
						throw new InvalidDataException ("Size invalid for descriptor.");
					if (compressedSize != -1 && compressedSize != entryCompressedSize)
						throw new InvalidDataException ("Compressed size invalid for descriptor.");
				}
			} else if (extractVersion >= VersionZip64 && ((uint) size == uint.MaxValue || (uint) compressedSize == uint.MaxValue)) {
				throw new InvalidDataException ("Required Zip64 extended information missing.");
			}

			if (entry.IsFile) {
				if (entry.CompressionMethod != CompressionMethodStored &&
						entry.CompressionMethod != CompressionMethodDeflated &&
						entry.CompressionMethod != CompressionMethodBZip2)
					throw new NotSupportedException ("Compression method not supported.");
				if (extractVersion > MaxVersionNeededToExtract || (extractVersion > 20 && extractVersion < VersionZip64))
					throw new NotSupportedException ($"Version required to extract this entry not supported ({extractVersion}).");
				if ((localFlags & (PatchedFlag | StrongEncryptionFlag | EnhancedCompressFlag | HeaderMaskedFlag)) != 0)
					throw new NotSupportedException ("The library does not support the zip version required to extract this entry.");
			}

			// Sizes can be verified only if they are known in the local header; the central header always has them.
			if ((localFlags & DescriptorFlag) == 0 || ((size > 0 || compressedSize > 0) && entrySize > 0)) {
				if (size != 0 && size != entrySize)
					throw new InvalidDataException ($"Size mismatch between central header({entrySize}) and local header({size}).");
				if (compressedSize != 0 && compressedSize != entryCompressedSize && compressedSize != 0xffffffff && compressedSize != -1)
					throw new InvalidDataException ($"Compressed size mismatch between central header({entryCompressedSize}) and local header({compressedSize}).");
			}

			return entry.LocalHeaderOffset + LocalHeaderSize + nameLength + extraLength;
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

		// Mirrors the central directory extra data handling of SharpZipLib 1.3.3 (ZIP64 sizes and offset, and
		// the extended Unix timestamp; NTFS timestamps are intentionally ignored like Info-ZIP does).
		static void ProcessExtraData (byte [] data, int start, int length, int versionNeeded, ref ulong size, ref ulong compressedSize, ref ulong localHeaderOffset, ref DateTime dateTime)
		{
			if (TryFindExtraField (data, start, length, Zip64ExtraFieldId, out int zip64Start, out int zip64Length)) {
				if (zip64Length < 4)
					throw new InvalidDataException ("Extra data extended Zip64 information length is invalid.");
				int zip64Position = zip64Start;
				int zip64End = zip64Start + zip64Length;
				if (size == uint.MaxValue) {
					EnsureExtraFieldAvailable (zip64Position, 8, zip64End);
					size = ReadUInt64 (data, zip64Position);
					zip64Position += 8;
				}
				if (compressedSize == uint.MaxValue) {
					EnsureExtraFieldAvailable (zip64Position, 8, zip64End);
					compressedSize = ReadUInt64 (data, zip64Position);
					zip64Position += 8;
				}
				if (localHeaderOffset == uint.MaxValue) {
					EnsureExtraFieldAvailable (zip64Position, 8, zip64End);
					localHeaderOffset = ReadUInt64 (data, zip64Position);
				}
			} else if ((versionNeeded & 0xff) >= VersionZip64 && (size == uint.MaxValue || compressedSize == uint.MaxValue)) {
				throw new InvalidDataException ("Zip64 Extended information required but is missing.");
			}

			if (TryFindExtraField (data, start, length, ExtendedTimestampExtraFieldId, out int timeStart, out int timeLength))
				dateTime = ReadExtendedTimestamp (data, timeStart, timeLength) ?? dateTime;
		}

		// Mirrors SharpZipLib 1.3.3 ExtendedUnixData.SetData, including its failures on truncated fields.
		static DateTime? ReadExtendedTimestamp (byte [] data, int start, int length)
		{
			const int ModificationTimeFlag = 0x01;
			const int AccessTimeFlag = 0x02;
			const int CreateTimeFlag = 0x04;

			int end = start + length;
			// An empty field reads the flags byte as -1, i.e. with every flag set.
			int flags = length > 0 ? data [start] : -1;
			int position = start + 1;
			DateTime? modificationTime = null;
			if ((flags & ModificationTimeFlag) != 0) {
				modificationTime = new DateTime (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan (0, 0, 0, ReadTimestampSeconds (data, position, end), 0);
				position += 4;
				// Central-header version is truncated after modification time
				if (length <= 5)
					return modificationTime;
			}
			if ((flags & AccessTimeFlag) != 0) {
				ReadTimestampSeconds (data, position, end);
				position += 4;
			}
			if ((flags & CreateTimeFlag) != 0)
				ReadTimestampSeconds (data, position, end);
			return (flags & ModificationTimeFlag) != 0 ? modificationTime : null;
		}

		static int ReadTimestampSeconds (byte [] data, int position, int end)
		{
			if (position + 4 > end)
				throw new EndOfStreamException ("Data underflow reading extended timestamp.");
			return unchecked ((int) ReadUInt32 (data, position));
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

		static void EnsureExtraFieldAvailable (int position, int count, int end)
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

		/// <summary>Read-only view of <c>length</c> bytes of a shared archive stream starting at <c>start</c>.</summary>
		sealed class SubReadStream : Stream
		{
			readonly Stream baseStream;
			readonly long end;
			readonly bool failWhenExhausted;
			long position;

			public SubReadStream (Stream baseStream, long start, long length, bool failWhenExhausted)
			{
				if (start > baseStream.Length || length > baseStream.Length - start)
					throw new EndOfStreamException ("Unexpected end of ZIP archive.");
				this.baseStream = baseStream;
				this.failWhenExhausted = failWhenExhausted;
				position = start;
				end = start + length;
			}

			public override int Read (byte [] buffer, int offset, int count)
			{
				long remaining = end - position;
				if (remaining <= 0) {
					if (failWhenExhausted && count > 0)
						throw new InvalidDataException ("Unexpected end of compressed ZIP entry data.");
					return 0;
				}
				if (count > remaining)
					count = (int) remaining;
				if (baseStream.Position != position)
					baseStream.Seek (position, SeekOrigin.Begin);
				int read = baseStream.Read (buffer, offset, count);
				if (read <= 0 && count > 0)
					throw new EndOfStreamException ("Unexpected end of ZIP archive.");
				position += read;
				return read;
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException ();
			public override long Position {
				get => throw new NotSupportedException ();
				set => throw new NotSupportedException ();
			}
			public override void Flush () { }
			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
			public override void SetLength (long value) => throw new NotSupportedException ();
			public override void Write (byte [] buffer, int offset, int count) => throw new NotSupportedException ();
		}

		/// <summary>Uncompressed entry data; fails if it does not match the size recorded in the central directory.</summary>
		sealed class EntryDataStream : Stream
		{
			readonly Stream data;
			readonly Entry entry;
			ulong total;

			public EntryDataStream (Stream data, Entry entry)
			{
				this.data = data;
				this.entry = entry;
			}

			public override int Read (byte [] buffer, int offset, int count)
			{
				int read = data.Read (buffer, offset, count);
				total += (ulong) read;
				if (total > entry.Size || (read == 0 && count > 0 && total != entry.Size))
					throw new InvalidDataException ($"Entry '{entry.Name}' does not match the size recorded in the ZIP central directory.");
				return read;
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing)
					data.Dispose ();
				base.Dispose (disposing);
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException ();
			public override long Position {
				get => throw new NotSupportedException ();
				set => throw new NotSupportedException ();
			}
			public override void Flush () { }
			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
			public override void SetLength (long value) => throw new NotSupportedException ();
			public override void Write (byte [] buffer, int offset, int count) => throw new NotSupportedException ();
		}
	}
}
