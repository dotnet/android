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
		const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
		const int EndOfCentralDirectoryMinimumSize = 22;
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
			stream.Seek (endOfCentralDirectoryOffset + 10, SeekOrigin.Begin);

			using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
			ushort entryCount = reader.ReadUInt16 ();
			uint centralDirectorySize = reader.ReadUInt32 ();
			uint centralDirectoryOffset = reader.ReadUInt32 ();

			stream.Seek (centralDirectoryOffset, SeekOrigin.Begin);
			var entries = new List<ZipEntryMetadata> ((int) entryCount);
			long centralDirectoryEnd = centralDirectoryOffset + centralDirectorySize;
			while (stream.Position < centralDirectoryEnd && entries.Count < entryCount) {
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
				reader.ReadUInt16 (); // disk number start
				reader.ReadUInt16 (); // internal file attributes
				reader.ReadUInt32 (); // external file attributes
				reader.ReadUInt32 (); // relative offset of local header

				var fileNameBytes = reader.ReadBytes (fileNameLength);
				var encoding = (flags & (1 << 11)) != 0 ? Encoding.UTF8 : Cp437;
				var fullName = encoding.GetString (fileNameBytes);

				if (stream.Position + extraFieldLength + fileCommentLength > stream.Length)
					throw new InvalidDataException ("ZIP central directory entry exceeds the available data.");

				stream.Seek (extraFieldLength + fileCommentLength, SeekOrigin.Current);
				entries.Add (new ZipEntryMetadata (
					fullName,
					crc32,
					compressedSize,
					uncompressedSize,
					(ZipEntryCompressionMethod) compressionMethod
				));
			}

			return entries;
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
					buffer [index + 3] == 0x06
				) {
					return stream.Length - searchLength + index;
				}
			}

			throw new InvalidDataException ("Could not locate the ZIP end of central directory record.");
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
	}
}
