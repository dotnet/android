using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class ELFEmbeddingHelper
{
	public sealed class EmbedItem
	{
		public readonly string SymbolName;
		public readonly string BaseFileName;

		public EmbedItem (string symbolName, string baseFileName)
		{
			SymbolName = symbolName;
			BaseFileName = baseFileName;
		}
	}

	public static class KnownEmbedItems
	{
		public static readonly EmbedItem RuntimeConfig = new ("embedded_runtime_config", "runtime_config");
		public static readonly EmbedItem AssemblyStore = new ("embedded_assembly_store", "assembly_store");
	}

	sealed class LlvmMcTargetConfig
	{
		public readonly string TargetArch;
		public readonly string TripleArch;
		public readonly string TripleApiPrefix;
		public readonly string AssemblerDirectivePrefix;
		public readonly string SizeType;
		public readonly uint WordSize;

		public LlvmMcTargetConfig (string targetArch, string tripleArch, string tripleApiPrefix, string assemblerDirectivePrefix, string sizeType, uint wordSize)
		{
			TargetArch = targetArch;
			TripleArch = tripleArch;
			TripleApiPrefix = tripleApiPrefix;
			AssemblerDirectivePrefix = assemblerDirectivePrefix;
			SizeType = sizeType;
			WordSize = wordSize;
		}
	}

	static readonly Dictionary<AndroidTargetArch, LlvmMcTargetConfig> llvmMcConfigs = new () {
		{ AndroidTargetArch.Arm64,  new ("aarch64", "aarch64", "android",     "@", ".xword", 8) },
		{ AndroidTargetArch.Arm,    new ("arm",     "armv7a",  "androideabi", "%", ".long",  4) },
		{ AndroidTargetArch.X86_64, new ("x86-64",  "x86_64",  "android",     "@", ".quad",  8) },
		{ AndroidTargetArch.X86,    new ("x86",     "i686",    "android",     "@", ".long",  4) },
	};

	static readonly Encoding asmFileEncoding = new UTF8Encoding (false);

	public static List<ITaskItem> EmbedBinary (
		TaskLoggingHelper log,
		ICollection<string> supportedAbis,
		string androidBinUtilsDirectory,
		string? inputFile,
		EmbedItem embedItem,
		string outputDirectory,
		bool missingContentOK)
	{
		var ret = new List<ITaskItem> ();
		if (supportedAbis.Count < 1) {
			log.LogDebugMessage ("ELFEmbeddingHelper: at least one target ABI must be specified. Probably a DTB build, skipping generation.");
			return ret;
		}

		string llvmMcPath = GetLlvmMcPath (androidBinUtilsDirectory);
		foreach (string abi in supportedAbis) {
			EmbedBinary (
				log,
				ret,
				llvmMcPath,
				abi,
				inputFile,
				outputDirectory,
				embedItem,
				missingContentOK
			);
		}

		return ret;
	}

	public static List<ITaskItem> EmbedBinary (
		TaskLoggingHelper log,
		string abi,
		string androidBinUtilsDirectory,
		string? inputFile,
		EmbedItem embedItem,
		string outputDirectory,
		bool missingContentOK)
	{
		var ret = new List<ITaskItem> ();
		if (String.IsNullOrEmpty (abi)) {
			log.LogDebugMessage ("ELFEmbeddingHelper: ABI must be specified. Probably a DTB build, skipping generation.");
			return ret;
		}

		EmbedBinary (
			log,
			ret,
			GetLlvmMcPath (androidBinUtilsDirectory),
			abi,
			inputFile,
			outputDirectory,
			embedItem,
			missingContentOK
		);
		return ret;
	}

	static void EmbedBinary (
		TaskLoggingHelper log,
		List<ITaskItem> resultItems,
		string llvmMcPath,
		string abi,
		string inputFile,
		string outputDirectory,
		EmbedItem embedItem,
		bool missingContentOK)
	{
		string outputFile = Path.Combine (outputDirectory, $"embed_{embedItem.BaseFileName}.{abi.ToLowerInvariant ()}.o");
		DoEmbed (log, MonoAndroidHelper.AbiToTargetArch (abi), llvmMcPath, inputFile, outputFile, embedItem, missingContentOK);
		if (!File.Exists (outputFile)) {
			return;
		}

		var taskItem = new TaskItem (outputFile);
		taskItem.SetMetadata ("Abi", abi);
		taskItem.SetMetadata ("RuntimeIdentifier", MonoAndroidHelper.AbiToRid (abi));
		resultItems.Add (taskItem);
	}

	static void DoEmbed (
		TaskLoggingHelper log,
		AndroidTargetArch arch,
		string llvmMcPath,
		string? inputFile,
		string outputFile,
		EmbedItem item,
		bool missingContentOK)
	{
		if (!llvmMcConfigs.TryGetValue (arch, out LlvmMcTargetConfig cfg)) {
			throw new NotSupportedException ($"Internal error: unsupported target arch '{arch}'");
		}

		bool haveInputFile = !String.IsNullOrEmpty (inputFile);
		if (!haveInputFile) {
			if (!missingContentOK) {
				throw new InvalidOperationException ("Internal error: input file must be specified");
			}
		} else {
			inputFile = Path.GetFullPath (inputFile);
		}
		outputFile = Path.GetFullPath (outputFile);

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

		string asmInputFile = Path.ChangeExtension (outputFile, ".s");

		using var fs = File.Open (asmInputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
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

		var args = new List<string> {
			$"--arch={cfg.TargetArch}",
			"--assemble",
			"--filetype=obj",
			"-g",
			$"--triple={cfg.TripleArch}-linux-{cfg.TripleApiPrefix}{XABuildConfig.AndroidMinimumDotNetApiLevel}",
			"-o", MonoAndroidHelper.QuoteFileNameArgument (outputFile),
			MonoAndroidHelper.QuoteFileNameArgument (asmInputFile),
		};

		// int ret = MonoAndroidHelper.RunProcess (llvmMcPath, String.Join (" ", args), log);
		// File.Copy (asmInputFile, $"/tmp/{Path.GetFileName (asmInputFile)}", true);
		// File.Copy (outputFile, $"/tmp/{Path.GetFileName (outputFile)}", true);
		// if (ret != 0) {
		// 	return;
		// }
	}

	static string GetLlvmMcPath (string androidBinUtilsDirectory) => MonoAndroidHelper.GetLlvmMcPath (androidBinUtilsDirectory);
}
