using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
		string inputFile,
		EmbedItem embedItem,
		string outputDirectory)
	{
		if (supportedAbis.Count < 1) {
			throw new ArgumentException ("At least one target ABI must be present", nameof (supportedAbis));
		}

		string llvmMcPath = GetLlvmMcPath (androidBinUtilsDirectory);
		var ret = new List<ITaskItem> ();
		foreach (string abi in supportedAbis) {
			EmbedBinary (
				log,
				ret,
				llvmMcPath,
				abi,
				inputFile,
				outputDirectory,
				embedItem
			);
		}

		return ret;
	}

	public static List<ITaskItem> EmbedBinary (
		TaskLoggingHelper log,
		string abi,
		string androidBinUtilsDirectory,
		string inputFile,
		EmbedItem embedItem,
		string outputDirectory)
	{
		if (String.IsNullOrEmpty (abi)) {
			throw new ArgumentException ("Must be a supported ABI name", nameof (abi));
		}

		var ret = new List<ITaskItem> ();
		EmbedBinary (
			log,
			ret,
			GetLlvmMcPath (androidBinUtilsDirectory),
			abi,
			inputFile,
			outputDirectory,
			embedItem
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
		EmbedItem embedItem)
	{
		string outputFile = Path.Combine (outputDirectory, $"embed_{embedItem.BaseFileName}.{abi.ToLowerInvariant ()}.o");
		DoEmbed (log, MonoAndroidHelper.AbiToTargetArch (abi), llvmMcPath, inputFile, outputFile, embedItem);
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
		string inputFile,
		string outputFile,
		EmbedItem item)
	{
		if (!llvmMcConfigs.TryGetValue (arch, out LlvmMcTargetConfig cfg)) {
			throw new NotSupportedException ($"Internal error: unsupported target arch '{arch}'");
		}

		inputFile = Path.GetFullPath (inputFile);
		outputFile = Path.GetFullPath (outputFile);

		var fi = new FileInfo (inputFile);
		long inputFileSize = fi.Length;
		string asmInputFile = Path.ChangeExtension (outputFile, ".s");
		string sanitizedInputFilePath = inputFile.Replace ("\\", "\\\\");

		using var fs = File.Open (asmInputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
		using var sw = new StreamWriter (fs, asmFileEncoding);

		string symbolName = item.SymbolName;
		sw.WriteLine ($".section .rodata,\"a\",{cfg.AssemblerDirectivePrefix}progbits");
		sw.WriteLine (".p2align 3, 0x00"); // Put the data at 4k boundary
		sw.WriteLine ();
		sw.WriteLine ($".global {symbolName}");
		sw.WriteLine ($".type {symbolName},{cfg.AssemblerDirectivePrefix}object");
		sw.WriteLine ($"{symbolName}:");
		sw.WriteLine ($"\t.incbin \"{sanitizedInputFilePath}\"");
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

		int ret = MonoAndroidHelper.RunProcess (llvmMcPath, String.Join (" ", args), log);
		if (ret != 0) {
			return;
		}
	}

	static string GetLlvmMcPath (string androidBinUtilsDirectory) => MonoAndroidHelper.GetLlvmMcPath (androidBinUtilsDirectory);
}
