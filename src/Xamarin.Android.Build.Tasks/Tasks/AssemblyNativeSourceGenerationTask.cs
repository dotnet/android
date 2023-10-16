using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

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

	internal sealed class GeneratedSource
	{
		public readonly string FilePath;
		public readonly AndroidTargetArch TargetArch;
		public readonly object? State;

		public GeneratedSource (string filePath, AndroidTargetArch targetArch, object? state = null)
		{
			FilePath = filePath;
			TargetArch = targetArch;
			State = state;
		}
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

	protected static bool ShouldSkipCompression (ITaskItem item)
	{
		string val = item.GetMetadata (DSOMetadata.AndroidSkipCompression);
		if (String.IsNullOrEmpty (val)) {
			return false;
		}

		if (!Boolean.TryParse (val, out bool skipCompression)) {
			throw new InvalidOperationException ($"Internal error: unable to parse '{val}' as a boolean value, in item '{item.ItemSpec}', from the '{DSOMetadata.AndroidSkipCompression}' metadata");
		}

		return skipCompression;
	}

	protected CompressionResult Compress (ITaskItem assembly)
	{
		if (ShouldSkipCompression (assembly)) {
			return new CompressionResult {
				Compressed = false,
				CompressedSize = 0,
				OutputFile = assembly.ItemSpec,
				InputFileInfo = new FileInfo (assembly.ItemSpec)
			};
		}

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

	internal List<GeneratedSource> GenerateSources (ICollection<string> supportedAbis, LLVMIR.LlvmIrComposer generator, LLVMIR.LlvmIrModule module, string baseFileName, object? sourceState = null)
	{
		if (String.IsNullOrEmpty (baseFileName)) {
			throw new ArgumentException ("must not be null or empty", nameof (baseFileName));
		}

		var generatedSources = new List<GeneratedSource> ();
		foreach (string abi in supportedAbis) {
			string targetAbi = abi.ToLowerInvariant ();
			string outputAsmFilePath = Path.Combine (SourcesOutputDirectory, $"{baseFileName}.{targetAbi}.ll");

			using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();
			AndroidTargetArch targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
			try {
				generator.Generate (module, targetArch, sw, outputAsmFilePath);
			} catch {
				throw;
			} finally {
				sw.Flush ();
			}

			if (Files.CopyIfStreamChanged (sw.BaseStream, outputAsmFilePath)) {
				Log.LogDebugMessage ($"File {outputAsmFilePath} was (re)generated");
			}
			generatedSources.Add (new GeneratedSource (outputAsmFilePath, targetArch, sourceState));
		}

		return generatedSources;
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
