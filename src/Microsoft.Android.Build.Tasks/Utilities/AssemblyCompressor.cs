#nullable enable
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Compresses assemblies with Zstandard before they are placed in the AssemblyStore.
/// The native runtime decompresses them at assembly load time. The 12-byte header
/// (magic / descriptor index / uncompressed length) is read back by the runtime and by
/// the diagnostic tools.
/// </summary>
static class AssemblyCompressor
{
	const uint CompressedDataMagic = 0x535A4158; // 'XAZS', little-endian

	static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;

	enum CompressionResult
	{
		Success,
		EncodingFailed,
	}

	public static bool TryCompress (TaskLoggingHelper log, string sourceAssembly, string destinationAssembly, uint descriptorIndex, int compressionLevel)
	{
		CompressionResult result = Compress (sourceAssembly, destinationAssembly, descriptorIndex, compressionLevel);

		if (result != CompressionResult.Success) {
			log.LogMessage ($"Failed to compress {sourceAssembly}");
			return false;
		}

		return true;
	}

	public static bool HasDescriptorIndex (string compressedAssembly, uint descriptorIndex)
	{
		using var reader = new BinaryReader (File.OpenRead (compressedAssembly));
		return reader.BaseStream.Length >= 3 * sizeof (uint) &&
			reader.ReadUInt32 () == CompressedDataMagic &&
			reader.ReadUInt32 () == descriptorIndex;
	}

	static CompressionResult Compress (string sourcePath, string outputFilePath, uint descriptorIndex, int compressionLevel)
	{
		var outputDirectory = Path.GetDirectoryName (outputFilePath);
		if (string.IsNullOrEmpty (outputDirectory))
			throw new ArgumentException ("must not be null or empty", nameof (outputFilePath));

		Directory.CreateDirectory (outputDirectory);

		var fi = new FileInfo (sourcePath);
		if (!fi.Exists)
			throw new InvalidOperationException ($"File '{sourcePath}' does not exist");

		int fileSize = checked ((int) fi.Length);

		byte[]? sourceBytes = null;
		byte[]? destBytes = null;
		try {
			sourceBytes = bytePool.Rent (fileSize);
			int bytesRead = 0;
			using (var fs = File.Open (sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				while (bytesRead < fileSize) {
					int read = fs.Read (sourceBytes, bytesRead, fileSize - bytesRead);
					if (read == 0)
						break;

					bytesRead += read;
				}
			}

			if (bytesRead != fileSize)
				return CompressionResult.EncodingFailed;

			long maxOutputSize = ZstandardEncoder.GetMaxCompressedLength (bytesRead);
			if (maxOutputSize <= 0 || maxOutputSize > int.MaxValue)
				return CompressionResult.EncodingFailed;

			destBytes = bytePool.Rent ((int) maxOutputSize);
			if (!ZstandardEncoder.TryCompress (sourceBytes.AsSpan (0, bytesRead), destBytes, out int encodedLength, compressionLevel, 0))
				return CompressionResult.EncodingFailed;

			using (var bw = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				bw.Write (CompressedDataMagic);         // magic
				bw.Write (descriptorIndex);             // index into runtime array of descriptors
				bw.Write (checked ((uint) fi.Length));  // file size before compression
				bw.Write (destBytes, 0, encodedLength);
				bw.Flush ();
				bw.BaseStream.Position = 0;
				Files.CopyIfStreamChanged (bw.BaseStream, outputFilePath);
			}
		} finally {
			if (sourceBytes != null)
				bytePool.Return (sourceBytes);
			if (destBytes != null)
				bytePool.Return (destBytes);
		}

		return CompressionResult.Success;
	}
}
