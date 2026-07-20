using System;
using System.IO;

#if NET11_0_OR_GREATER
using System.Buffers;
using K4os.Compression.LZ4;
using ZstandardDecoder = System.IO.Compression.ZstandardDecoder;
#endif // NET11_0_OR_GREATER

namespace Xamarin.Android.AssemblyStore;

public enum AssemblyCompressionFormat
{
	Lz4,
	Zstandard,
}

public static class AssemblyCompression
{
	const uint Lz4Magic = 0x5A4C4158; // 'XALZ', little-endian
	const uint ZstandardMagic = 0x535A4158; // 'XAZS', little-endian

#if NET11_0_OR_GREATER
	const int HeaderSize = 3 * sizeof (uint);
	const uint MaximumUncompressedAssemblySize = 512 * 1024 * 1024;

	static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;

	public static bool TryDecompress (Stream input, Stream output, out AssemblyCompressionFormat format)
	{
		ArgumentNullException.ThrowIfNull (input);
		ArgumentNullException.ThrowIfNull (output);

		if (!input.CanRead || !input.CanSeek) {
			throw new ArgumentException ("Input stream must be readable and seekable", nameof (input));
		}
		if (!output.CanWrite) {
			throw new ArgumentException ("Output stream must be writable", nameof (output));
		}

		long start = input.Position;
		if (!TryReadFormat (input, out format)) {
			return false;
		}

		if (input.Length - start < HeaderSize) {
			throw new InvalidDataException ($"Truncated {format} assembly header");
		}

		using var reader = new BinaryReader (input, System.Text.Encoding.UTF8, leaveOpen: true);
		reader.ReadUInt32 (); // descriptor index
		uint uncompressedLength = reader.ReadUInt32 ();
		if (uncompressedLength > MaximumUncompressedAssemblySize) {
			throw new InvalidDataException ($"{format} assembly expands to an unsupported size of {uncompressedLength} bytes (maximum {MaximumUncompressedAssemblySize} bytes)");
		}

		long compressedLength = input.Length - input.Position;
		if (compressedLength > Int32.MaxValue) {
			throw new InvalidDataException ($"{format} assembly contains an unsupported compressed size of {compressedLength} bytes");
		}

		byte[]? compressedBytes = null;
		byte[]? assemblyBytes = null;
		try {
			compressedBytes = bytePool.Rent ((int)compressedLength);
			ReadFully (reader, compressedBytes, (int)compressedLength);

			assemblyBytes = bytePool.Rent ((int)uncompressedLength);
			int decoded = format switch {
				AssemblyCompressionFormat.Lz4 => LZ4Codec.Decode (
					compressedBytes,
					0,
					(int)compressedLength,
					assemblyBytes,
					0,
					(int)uncompressedLength
				),
				AssemblyCompressionFormat.Zstandard => ZstandardDecoder.TryDecompress (
					compressedBytes.AsSpan (0, (int)compressedLength),
					assemblyBytes.AsSpan (0, (int)uncompressedLength),
					out int bytesWritten
				) ? bytesWritten : -1,
				_ => throw new InvalidOperationException ($"Unsupported compression format '{format}'"),
			};

			if (decoded != (int)uncompressedLength) {
				throw new InvalidDataException ($"Failed to decompress {format} assembly data (decoded {decoded} of {uncompressedLength} bytes)");
			}

			output.Write (assemblyBytes, 0, decoded);
			output.Flush ();
			return true;
		} finally {
			if (compressedBytes != null) {
				bytePool.Return (compressedBytes);
			}
			if (assemblyBytes != null) {
				bytePool.Return (assemblyBytes);
			}
		}
	}

	static void ReadFully (BinaryReader reader, byte[] destination, int count)
	{
		int totalRead = 0;
		while (totalRead < count) {
			int read = reader.Read (destination, totalRead, count - totalRead);
			if (read == 0) {
				throw new EndOfStreamException ("Unexpected end of compressed assembly data");
			}
			totalRead += read;
		}
	}
#endif // NET11_0_OR_GREATER

	internal static bool IsCompressed (Stream input)
	{
		ArgumentNullException.ThrowIfNull (input);

		if (!input.CanRead || !input.CanSeek) {
			throw new ArgumentException ("Input stream must be readable and seekable", nameof (input));
		}

		long start = input.Position;
		bool compressed = TryReadFormat (input, out _);
		input.Seek (start, SeekOrigin.Begin);
		return compressed;
	}

	static bool TryReadFormat (Stream input, out AssemblyCompressionFormat format)
	{
		long start = input.Position;
		if (input.Length - start < sizeof (uint)) {
			format = default;
			return false;
		}

		using var reader = new BinaryReader (input, System.Text.Encoding.UTF8, leaveOpen: true);
		uint magic = reader.ReadUInt32 ();
		switch (magic) {
			case Lz4Magic:
				format = AssemblyCompressionFormat.Lz4;
				return true;
			case ZstandardMagic:
				format = AssemblyCompressionFormat.Zstandard;
				return true;
			default:
				input.Seek (start, SeekOrigin.Begin);
				format = default;
				return false;
		}
	}
}
