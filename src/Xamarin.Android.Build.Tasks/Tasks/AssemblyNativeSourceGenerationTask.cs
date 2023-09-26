using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public abstract class AssemblyNativeSourceGenerationTask : AndroidTask
{
	protected sealed class CompressionResult
	{
		public bool Compressed;
		public uint CompressedSize;
		public string OutputFile;
		public FileInfo InputFileInfo;
	}

	[Required]
	public string SourcesOutputDirectory { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	[Required]
	public string CompressedAssembliesOutputDirectory { get; set; }

	AssemblyCompression? assemblyCompressor = null;

	public override bool RunTask ()
	{
		if (EnableCompression) {
			assemblyCompressor = new AssemblyCompression (Log, CompressedAssembliesOutputDirectory);
			Log.LogDebugMessage ("Assembly compression ENABLED");
		} else {
			Log.LogDebugMessage ("Assembly compression DISABLED");
		}

		Generate ();
		return !Log.HasLoggedErrors;
	}

	protected CompressionResult Compress (ITaskItem assembly)
	{
		return Compress (assembly.ItemSpec, assembly.GetMetadata ("DestinationSubDirectory"));
	}

	protected CompressionResult Compress (string assemblyPath, string? destinationSubdirectory = null)
	{
		FileInfo fi = new (assemblyPath);

		bool compressed;
		string outputFile;
		uint compressedSize = 0;

		if (assemblyCompressor != null) {
			(outputFile, compressed) = assemblyCompressor.CompressAssembly (assemblyPath, fi, destinationSubdirectory);

			if (!compressed) {
				compressedSize = 0;
			} else {
				var cfi = new FileInfo (outputFile);
				compressedSize = (uint)cfi.Length;
			}
		} else {
			outputFile = assemblyPath;
			compressed = false;
			compressedSize = 0;
		}

		Log.LogDebugMessage ($"    will include from: {outputFile} (compressed? {compressed}; compressedSize == {compressedSize}");
		return new CompressionResult {
			Compressed = compressed,
			CompressedSize = compressedSize,
			OutputFile = outputFile,
			InputFileInfo = fi,
		};
	}

	internal void GenerateSources (ICollection<string> supportedAbis, LLVMIR.LlvmIrComposer generator, LLVMIR.LlvmIrModule module, string baseFileName)
	{
		if (String.IsNullOrEmpty (baseFileName)) {
			throw new ArgumentException ("must not be null or empty", nameof (baseFileName));
		}

		foreach (string abi in supportedAbis) {
			string targetAbi = abi.ToLowerInvariant ();
			string outputAsmFilePath = Path.Combine (SourcesOutputDirectory, $"{baseFileName}.{targetAbi}.ll");

			using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();
			try {
				generator.Generate (module, GeneratePackageManagerJava.GetAndroidTargetArchForAbi (abi), sw, outputAsmFilePath);
			} catch {
				throw;
			} finally {
				sw.Flush ();
			}

			if (Files.CopyIfStreamChanged (sw.BaseStream, outputAsmFilePath)) {
				Log.LogDebugMessage ($"File {outputAsmFilePath} was (re)generated");
			}
		}
	}

	protected string GetAssemblyName (ITaskItem assembly)
	{
		if (!MonoAndroidHelper.IsSatelliteAssembly (assembly)) {
			return Path.GetFileName (assembly.ItemSpec);
		}

		// It's a satellite assembly, %(DestinationSubDirectory) is the culture prefix
		string? destinationSubDir = assembly.GetMetadata ("DestinationSubDirectory");
		if (String.IsNullOrEmpty (destinationSubDir)) {
			throw new InvalidOperationException ($"Satellite assembly '{assembly.ItemSpec}' has no culture metadata item");
		}

		string ret = $"{destinationSubDir}{Path.GetFileName (assembly.ItemSpec)}";
		if (!assembly.ItemSpec.EndsWith (ret, StringComparison.OrdinalIgnoreCase)) {
			throw new InvalidOperationException ($"Invalid metadata in satellite assembly '{assembly.ItemSpec}', culture metadata ('{destinationSubDir}') doesn't match file path");
		}

		return ret;
	}

	internal DSOAssemblyInfo MakeAssemblyInfo (ITaskItem assembly, string inputFile, long fileLength, uint compressedSize)
	{
		return new DSOAssemblyInfo (GetAssemblyName (assembly), inputFile, (uint)fileLength, compressedSize);
	}

	protected abstract void Generate ();
}
