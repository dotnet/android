#nullable enable
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Produces the assembly-store wrapper shared library, whose payload lives in a
/// *loadable* ELF section (SHF_ALLOC, covered by a PT_LOAD segment) and is
/// pointed at by an exported dynamic symbol (<c>_assembly_store</c>).
///
/// With this layout the runtime simply
/// <c>dlopen("libassembly-store.so")</c> + <c>dlsym("_assembly_store")</c>
/// and lets the dynamic linker locate + map the payload.
///
/// The ELF wrapper is written directly in managed code, without an assembler or linker.
/// </summary>
static class DlopenAssemblyStoreGenerator
{
	public const string PayloadStartSymbol = AssemblyStoreElfWriter.PayloadSymbol;
	const string WrappedSubDirectory = "wrapped-assembly-store";

	/// <summary>
	/// Wraps <paramref name="payloadFilePath"/> (the raw assembly-store blob) into a loadable-symbol
	/// shared library and returns the path to the produced .so.
	/// </summary>
	public static string WrapIt (TaskLoggingHelper log, string baseOutputDirectory, AndroidTargetArch targetArch, string payloadFilePath, string outputFileName)
	{
		string outputDir = GetArchOutputPath (baseOutputDirectory, targetArch);
		Directory.CreateDirectory (outputDir);

		string outputFile = Path.Combine (outputDir, outputFileName);
		log.LogDebugMessage ($"[{targetArch}] Wrapping '{payloadFilePath}' into loadable-symbol shared library '{outputFile}'");
		using var payload = File.OpenRead (payloadFilePath);
		using var output = File.Create (outputFile);
		AssemblyStoreElfWriter.Write (payload, output, targetArch, outputFileName);

		return outputFile;
	}

	public static IEnumerable<string> GetDirectoriesToCleanUp (string baseOutputDirectory, IEnumerable<string> supportedAbis)
	{
		foreach (string abi in supportedAbis) {
			string outputDir = GetArchOutputPath (baseOutputDirectory, MonoAndroidHelper.AbiToTargetArch (abi));
			if (Directory.Exists (outputDir)) {
				yield return outputDir;
			}
		}
	}

	static string GetArchOutputPath (string baseOutputDirectory, AndroidTargetArch targetArch)
	{
		return Path.Combine (baseOutputDirectory, MonoAndroidHelper.ArchToRid (targetArch), WrappedSubDirectory);
	}
}
