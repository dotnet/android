using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class ELFEmbeddingHelper
{
	public sealed class EmbedItem
	{
		public readonly string SymbolName;
		public readonly string BaseFileName;
		public readonly NativeAssemblerItemsHelper.KnownMode NativeAssemblerMode;

		public EmbedItem (string symbolName, string baseFileName, NativeAssemblerItemsHelper.KnownMode nativeAssemblerMode)
		{
			SymbolName = symbolName;
			BaseFileName = baseFileName;
			NativeAssemblerMode = nativeAssemblerMode;
		}
	}

	public static class KnownEmbedItems
	{
		public static readonly EmbedItem RuntimeConfig = new ("embedded_runtime_config", "runtime_config", NativeAssemblerItemsHelper.KnownMode.EmbeddedRuntimeConfig);
		public static readonly EmbedItem AssemblyStore = new ("embedded_assembly_store", "assembly_store", NativeAssemblerItemsHelper.KnownMode.EmbeddedAssemblyStore);
	}

	static readonly Encoding asmFileEncoding = new UTF8Encoding (false);

	public static void EmbedBinary (
		TaskLoggingHelper log,
		ICollection<string> supportedAbis,
		string androidBinUtilsDirectory,
		string? inputFile,
		EmbedItem embedItem,
		string outputDirectory,
		bool missingContentOK)
	{
		if (supportedAbis.Count < 1) {
			log.LogDebugMessage ("ELFEmbeddingHelper: at least one target ABI must be specified. Probably a DTB build, skipping generation.");
			return;
		}

		foreach (string abi in supportedAbis) {
			DoEmbed (
				log,
				MonoAndroidHelper.AbiToTargetArch (abi),
				inputFile, outputDirectory,
				embedItem,
				missingContentOK
			);
		}
	}

	public static void EmbedBinary (
		TaskLoggingHelper log,
		string abi,
		string androidBinUtilsDirectory,
		string? inputFile,
		EmbedItem embedItem,
		string outputDirectory,
		bool missingContentOK)
	{
		if (String.IsNullOrEmpty (abi)) {
			log.LogDebugMessage ("ELFEmbeddingHelper: ABI must be specified. Probably a DTB build, skipping generation.");
			return;
		}

		DoEmbed (
			log,
			MonoAndroidHelper.AbiToTargetArch (abi),
			inputFile,
			outputDirectory,
			embedItem,
			missingContentOK
		);
	}

	static void DoEmbed (
		TaskLoggingHelper log,
		AndroidTargetArch arch,
		string? inputFile,
		string outputDirectory,
		EmbedItem item,
		bool missingContentOK)
	{
		NativeAssemblerCompilation.LlvmMcTargetConfig cfg = NativeAssemblerCompilation.GetLlvmMcConfig (arch);

		bool haveInputFile = !String.IsNullOrEmpty (inputFile);
		if (!haveInputFile) {
			if (!missingContentOK) {
				throw new InvalidOperationException ("Internal error: input file must be specified");
			}
		} else {
			inputFile = Path.GetFullPath (inputFile);
		}

		long inputFileSize = 0;
		string? sanitizedInputFilePath = null;

		if (haveInputFile) {
			var fi = new FileInfo (inputFile);
			if (fi.Exists) {
				inputFileSize = fi.Length;
				sanitizedInputFilePath = inputFile.Replace ("\\", "\\\\");
			} else if (!missingContentOK) {
				throw new InvalidOperationException ($"Internal error: input file '{inputFile}' does not exist");
			}
		}

		string asmSourceFile = NativeAssemblerItemsHelper.GetSourcePath (log, item.NativeAssemblerMode, outputDirectory, arch);

		using var fs = File.Open (asmSourceFile, FileMode.Create, FileAccess.Write, FileShare.Read);
		using var sw = new StreamWriter (fs, asmFileEncoding);

		string symbolName = item.SymbolName;
		sw.WriteLine ($".section .rodata,\"a\",{cfg.AssemblerDirectivePrefix}progbits");
		sw.WriteLine (".p2align 3, 0x00"); // Put the data at the 4k boundary
		sw.WriteLine ();
		sw.WriteLine ($".global {symbolName}");
		sw.WriteLine ($".type {symbolName},{cfg.AssemblerDirectivePrefix}object");
		sw.WriteLine ($"{symbolName}:");

		if (!String.IsNullOrEmpty (sanitizedInputFilePath)) {
			sw.WriteLine ($"\t.incbin \"{sanitizedInputFilePath}\"");
		}
		sw.WriteLine ($"\t.size {symbolName}, {inputFileSize}");
		sw.WriteLine ();

		symbolName += "_size";
		sw.WriteLine ($".global {symbolName}");
		sw.WriteLine ($"{symbolName}:");
		sw.WriteLine ($"\t{cfg.SizeType}\t{inputFileSize}");
		sw.WriteLine ($"\t.size {symbolName}, {cfg.WordSize}");

		sw.Flush ();
		sw.Close ();
	}
}
