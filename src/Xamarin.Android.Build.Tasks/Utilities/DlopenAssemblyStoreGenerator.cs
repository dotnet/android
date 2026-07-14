#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Produces the CoreCLR assembly-store wrapper shared library, whose payload lives in a
/// *loadable* ELF section (SHF_ALLOC, covered by a PT_LOAD segment) and is
/// pointed at by an exported dynamic symbol (<c>_assembly_store</c>).
///
/// This differs from <see cref="DSOWrapperGenerator"/> (used by MonoVM), which injects the
/// payload with <c>llvm-objcopy --add-section</c> into a *non-loadable* section
/// and requires the runtime to locate the store inside the APK (ZIP central
/// directory parsing), mmap it and walk the ELF section headers by hand.
///
/// With this layout the CoreCLR runtime simply
/// <c>dlopen("libassembly-store.so")</c> + <c>dlsym("_assembly_store")</c>
/// and lets the dynamic linker locate + map the payload.
///
/// The wrapper is built with the shipped binutils (<c>llvm-mc</c> to assemble a
/// tiny <c>.incbin</c> stub, <c>ld</c> to link a shared object). No clang is
/// required.
/// </summary>
static class DlopenAssemblyStoreGenerator
{
	public const string PayloadStartSymbol = "_assembly_store";

	// Section name that holds the payload. Must match the name `tools/assembly-store-reader-mk2`
	// (Utils.FindELFPayloadSectionOffsetAndSize) looks for and the one `DSOWrapperGenerator` uses.
	const string PayloadSectionName = "payload";

	// Per-arch max page size, used both for `ld -z max-page-size` and to align the payload so it is
	// page-aligned on the largest page size the ABI can run on: 16k for the 64-bit arches (which may
	// run on 16k-page devices), 4k for the 32-bit arches (Android's 16k-page support is 64-bit-only).
	// Using the per-arch value instead of a hardcoded 16k avoids ~12k of dead padding in each 32-bit
	// wrapper.
	static (string Triple, string ElfArch, int MaxPageSize) GetArchToolInfo (AndroidTargetArch arch) => arch switch {
		AndroidTargetArch.Arm64  => ("aarch64-linux-android",   "aarch64linux",       16384),
		AndroidTargetArch.Arm    => ("armv7-linux-androideabi",  "armelf_linux_eabi",  4096),
		AndroidTargetArch.X86    => ("i686-linux-android",       "elf_i386",           4096),
		AndroidTargetArch.X86_64 => ("x86_64-linux-android",     "elf_x86_64",         16384),
		_ => throw new NotSupportedException ($"Unsupported Android target architecture: {arch}"),
	};

	/// <summary>
	/// Wraps <paramref name="payloadFilePath"/> (the raw assembly-store blob) into a loadable-symbol
	/// shared library and returns the path to the produced .so.
	/// </summary>
	public static string WrapIt (TaskLoggingHelper log, DSOWrapperGenerator.Config config, AndroidTargetArch targetArch, string payloadFilePath, string outputFileName)
	{
		var toolInfo = GetArchToolInfo (targetArch);

		string outputDir = Path.Combine (config.BaseOutputDirectory, MonoAndroidHelper.ArchToRid (targetArch), DSOWrapperGenerator.WrappedDlopenSubDirectory);
		Directory.CreateDirectory (outputDir);

		string outputFile = Path.Combine (outputDir, outputFileName);
		string objFile = Path.Combine (outputDir, outputFileName + ".o");
		string asmFile = Path.Combine (outputDir, outputFileName + ".S");

		log.LogDebugMessage ($"[{targetArch}] Wrapping '{payloadFilePath}' into loadable-symbol shared library '{outputFile}'");

		// The `.incbin` uses an absolute path so we don't depend on the assembler's working directory.
		// The section is named `payload` (no leading dot) to match the name the MonoVM
		// `DSOWrapperGenerator` uses and that `tools/assembly-store-reader-mk2` looks for.
		string incbinPath = payloadFilePath.Replace ("\\", "\\\\").Replace ("\"", "\\\"");
		string asm = $"""
				.section {PayloadSectionName}, "a"
				.balign {toolInfo.MaxPageSize}
				.globl {PayloadStartSymbol}
			{PayloadStartSymbol}:
				.incbin "{incbinPath}"

			""";
		File.WriteAllText (asmFile, asm);

		string llvmMc = Path.Combine (config.AndroidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (config.AndroidBinUtilsDirectory, "llvm-mc"));
		List<string> mcArgs = [
			"--filetype=obj",
			$"-triple={toolInfo.Triple}",
			$"-o {MonoAndroidHelper.QuoteFileNameArgument (objFile)}",
			MonoAndroidHelper.QuoteFileNameArgument (asmFile),
		];

		int ret = MonoAndroidHelper.RunProcess (llvmMc, string.Join (" ", mcArgs), log);
		if (ret != 0) {
			log.LogError ($"Failed to assemble assembly-store wrapper for '{targetArch}' (llvm-mc exit code {ret})");
			return outputFile;
		}

		string ld = Path.Combine (config.AndroidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (config.AndroidBinUtilsDirectory, "ld"));
		List<string> ldArgs = [
			"--shared",
			$"-soname {MonoAndroidHelper.QuoteFileNameArgument (outputFileName)}",
			$"-m {toolInfo.ElfArch}",
			$"-z max-page-size={toolInfo.MaxPageSize}",
			"--build-id=sha1",
			$"--export-dynamic-symbol={PayloadStartSymbol}",
			$"-o {MonoAndroidHelper.QuoteFileNameArgument (outputFile)}",
			MonoAndroidHelper.QuoteFileNameArgument (objFile),
		];

		ret = MonoAndroidHelper.RunProcess (ld, string.Join (" ", ldArgs), log);
		if (ret != 0) {
			log.LogError ($"Failed to link assembly-store wrapper for '{targetArch}' (ld exit code {ret})");
			return outputFile;
		}

		return outputFile;
	}
}
