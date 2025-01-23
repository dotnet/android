using System;
using System.Collections.Generic;
using System.Buffers;
using System.IO;

using K4os.Compression.LZ4;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class AssemblyCompression
	{
		public enum CompressionResult
		{
			Success,
			InputTooBig,
			EncodingFailed,
		}

		public sealed class AssemblyData
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

		static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;

		static CompressionResult Compress (AssemblyData data, string outputFilePath)
		{
			if (data == null)
				throw new ArgumentNullException (nameof (data));

			var outputDirectory = Path.GetDirectoryName (outputFilePath);

			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			Directory.CreateDirectory (outputDirectory);

			var fi = new FileInfo (data.SourcePath);
			if (!fi.Exists)
				throw new InvalidOperationException ($"File '{data.SourcePath}' does not exist");
			// if ((ulong)fi.Length > InputAssemblySizeLimit) {
			// 	return CompressionResult.InputTooBig;
			// }

			data.DestinationPath = outputFilePath;
				//Path.Combine (outputDirectory, $"{Path.GetFileName (data.SourcePath)}.lz4");
			data.SourceSize = checked((uint)fi.Length);

			int bytesRead;
			byte[] sourceBytes = null;
			byte[] destBytes = null;
			try {
				int fileSize = checked((int)fi.Length);
				sourceBytes = bytePool.Rent (fileSize);
				using (var fs = File.Open (data.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					bytesRead = fs.Read (sourceBytes, 0, fileSize);
				}

				destBytes = bytePool.Rent (LZ4Codec.MaximumOutputSize (bytesRead));
				int encodedLength = LZ4Codec.Encode (sourceBytes, 0, bytesRead, destBytes, 0, destBytes.Length, LZ4Level.L12_MAX);
				if (encodedLength < 0)
					return CompressionResult.EncodingFailed;

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
				if (sourceBytes != null)
					bytePool.Return (sourceBytes);
				if (destBytes != null)
					bytePool.Return (destBytes);
			}

			return CompressionResult.Success;
		}

		public static bool TryCompress (TaskLoggingHelper log, string sourceAssembly, string destinationAssembly, uint descriptorIndex)
		{
			AssemblyData compressedAssembly = new AssemblyData (sourceAssembly, descriptorIndex);
			CompressionResult result = Compress (compressedAssembly, destinationAssembly);

			if (result != CompressionResult.Success) {
				switch (result) {
					case CompressionResult.EncodingFailed:
						log.LogMessage ($"Failed to compress {sourceAssembly}");
						break;
					case CompressionResult.InputTooBig:
						log.LogMessage ($"Input assembly {sourceAssembly} exceeds maximum input size");
						break;
					default:
						log.LogMessage ($"Unknown error compressing {sourceAssembly}");
						break;
				}

				return false;
			}

			return true;
		}

		// Gets the descriptor index for the specified assembly from the compressed assembly info
		public static bool TryGetDescriptorIndex (TaskLoggingHelper log, ITaskItem assembly, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> compressedAssembliesInfo, out uint descriptorIndex)
		{
			descriptorIndex = 0;

			var key = CompressedAssemblyInfo.GetDictionaryKey (assembly);
			var arch = MonoAndroidHelper.GetTargetArch (assembly);

			if (!compressedAssembliesInfo.TryGetValue (arch, out Dictionary<string, CompressedAssemblyInfo> assembliesInfo)) {
				throw new InvalidOperationException ($"Internal error: compression assembly info for architecture {arch} not available");
			}

			if (!assembliesInfo.TryGetValue (key, out CompressedAssemblyInfo info) || info == null) {
				log.LogDebugMessage ($"Assembly missing from {nameof (CompressedAssemblyInfo)}: {key}");
				return false;
			}

			descriptorIndex = info.DescriptorIndex;

			return true;
		}

		// Gets the output path for the compressed assembly
		public static string GetCompressedAssemblyOutputPath (ITaskItem assembly, string compressedOutputDir)
		{
			var assemblyOutputDir = GetCompressedAssemblyOutputDirectory (assembly, compressedOutputDir);
			return Path.Combine (assemblyOutputDir, $"{Path.GetFileName (assembly.ItemSpec)}.lz4");
		}

		static string GetCompressedAssemblyOutputDirectory (ITaskItem assembly, string compressedOutputDir)
		{
			string assemblyOutputDir;
			string subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
			string abi = MonoAndroidHelper.GetAssemblyAbi (assembly);

			if (!string.IsNullOrEmpty (subDirectory) && !(subDirectory.EndsWith ($"{abi}/", StringComparison.Ordinal) || subDirectory.EndsWith ($"{abi}\\", StringComparison.Ordinal))) {
				assemblyOutputDir = Path.Combine (compressedOutputDir, abi, subDirectory);
			} else {
				assemblyOutputDir = Path.Combine (compressedOutputDir, abi);
			}

			return assemblyOutputDir;
		}
	}
}
