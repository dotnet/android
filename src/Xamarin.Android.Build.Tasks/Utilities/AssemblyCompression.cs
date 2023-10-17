using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

using K4os.Compression.LZ4;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class AssemblyCompression
	{
		enum CompressionResult
		{
			Success,
			InputTooBig,
			EncodingFailed,
		}

		sealed class AssemblyData
		{
			public string SourcePath { get; internal set; }
			public uint DescriptorIndex { get; internal set; }

			public string DestinationPath;
			public uint SourceSize;
			public uint DestinationSize;

			public AssemblyData (string sourcePath, uint descriptorIndex)
			{
				SetData (sourcePath, descriptorIndex);
			}

			public void SetData (string sourcePath, uint descriptorIndex)
			{
				if (String.IsNullOrEmpty (sourcePath))
					throw new ArgumentException ("must not be null or empty", nameof (sourcePath));
				SourcePath = sourcePath;
				DescriptorIndex = descriptorIndex;
			}
		}

		const uint CompressedDataMagic = 0x5A4C4158; // 'XALZ', little-endian

		// TODO: consider making it configurable via an MSBuild property, would be more flexible this way
		//
		// Arbitrary limit of the input assembly size, to clamp down on memory allocation. Our unlinked Mono.Android.dll
		// assembly (the biggest one we have) is currently (May 2020) around 27MB, so let's bump the value to 30MB times
		// two - it should be more than enough for most needs.
		//public const ulong InputAssemblySizeLimit = 60 * 1024 * 1024;

		readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
		readonly TaskLoggingHelper log;
		readonly string compressedOutputDir;

		public AssemblyCompression (TaskLoggingHelper log, string compressedOutputDir)
		{
			this.log = log;
			this.compressedOutputDir = compressedOutputDir;
		}

		// TODO: consider using https://github.com/emmanuel-marty/lz4ultra
		CompressionResult Compress (AssemblyData data, string outputDirectory)
		{
			if (data == null) {
				throw new ArgumentNullException (nameof (data));
			}

			if (String.IsNullOrEmpty (outputDirectory)) {
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));
			}

			Directory.CreateDirectory (outputDirectory);

			var fi = new FileInfo (data.SourcePath);
			if (!fi.Exists)
				throw new InvalidOperationException ($"File '{data.SourcePath}' does not exist");
			// if ((ulong)fi.Length > InputAssemblySizeLimit) {
			// 	return CompressionResult.InputTooBig;
			// }

			data.DestinationPath = Path.Combine (outputDirectory, $"{Path.GetFileName (data.SourcePath)}.lz4");
			data.SourceSize = (uint)fi.Length;

			byte[] sourceBytes = null;
			byte[] destBytes = null;
			try {
				sourceBytes = bytePool.Rent (checked((int)fi.Length));
				using (var fs = File.Open (data.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					fs.Read (sourceBytes, 0, (int)fi.Length);
				}

				destBytes = bytePool.Rent (LZ4Codec.MaximumOutputSize (sourceBytes.Length));
				int encodedLength = LZ4Codec.Encode (sourceBytes, 0, checked((int)fi.Length), destBytes, 0, destBytes.Length, LZ4Level.L09_HC);
				if (encodedLength < 0) {
					return CompressionResult.EncodingFailed;
				}

				data.DestinationSize = (uint)encodedLength;
				using (var fs = File.Open (data.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					using (var bw = new BinaryWriter (fs)) {
						bw.Write (CompressedDataMagic);  // magic
						bw.Write (data.DescriptorIndex); // index into runtime array of descriptors
						bw.Write (checked((uint)fi.Length));      // file size before compression

						bw.Write (destBytes, 0, encodedLength);
						bw.Flush ();
					}
				}
			} finally {
				if (sourceBytes != null) {
					bytePool.Return (sourceBytes);
				}
				if (destBytes != null) {
					bytePool.Return (destBytes);
				}
			}

			return CompressionResult.Success;
		}

		public (string outputPath, bool compressed) CompressAssembly (ITaskItem assembly, FileInfo inputInfo)
		{
			if (Boolean.TryParse (assembly.GetMetadata ("AndroidSkipCompression"), out bool value) && value) {
				log.LogDebugMessage ($"Skipping compression of {assembly.ItemSpec} due to 'AndroidSkipCompression' == 'true' ");
				return (assembly.ItemSpec, false);
			}

			return CompressAssembly (assembly.ItemSpec, inputInfo, assembly.GetMetadata ("DestinationSubDirectory"));
		}

		public (string outputPath, bool compressed) CompressAssembly (string assemblyPath, FileInfo inputInfo, string? subDirectory)
		{
			if (!inputInfo.Exists) {
				throw new InvalidOperationException ($"File '{assemblyPath}' does not exist");
			}

			string assemblyOutputDir;
			if (!String.IsNullOrEmpty (subDirectory)) {
				assemblyOutputDir = Path.Combine (compressedOutputDir, subDirectory);
			} else {
				assemblyOutputDir = compressedOutputDir;
			}
			string outputPath = Path.Combine (assemblyOutputDir, $"{Path.GetFileName (assemblyPath)}.lz4");
			Directory.CreateDirectory (assemblyOutputDir);

			byte[]? sourceBytes = null;
			byte[]? destBytes = null;
			try {
				int inputLength = checked((int)inputInfo.Length);
				sourceBytes = bytePool.Rent (inputLength);
				using (var fs = File.Open (assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					fs.Read (sourceBytes, 0, inputLength);
				}

				destBytes = bytePool.Rent (LZ4Codec.MaximumOutputSize (sourceBytes.Length));
				int encodedLength = LZ4Codec.Encode (sourceBytes, 0, inputLength, destBytes, 0, destBytes.Length, LZ4Level.L09_HC);
				if (encodedLength < 0) {
					log.LogMessage ($"Failed to compress {assemblyPath}");
					return (assemblyPath, false);
				}

				using (var fs = File.Open (outputPath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					using (var bw = new BinaryWriter (fs)) {
						bw.Write (destBytes, 0, encodedLength);
						bw.Flush ();
					}
				}
			} finally {
				if (sourceBytes != null) {
					bytePool.Return (sourceBytes);
				}
				if (destBytes != null) {
					bytePool.Return (destBytes);
				}
			}

			return (outputPath, true);
		}
	}
}
