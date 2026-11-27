#nullable enable
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Produces a data-only assembly-store shared library with a loadable payload section
/// and an exported <c>_assembly_store</c> dynamic symbol, without an assembler or linker.
/// </summary>
static class DlopenAssemblyStoreGenerator
{
	public const string PayloadStartSymbol = AssemblyStoreElfWriter.PayloadSymbol;
	const string WrappedSubDirectory = "wrapped-assembly-store";

	public static string WrapIt (TaskLoggingHelper log, string baseOutputDirectory, AndroidTargetArch targetArch, string payloadFilePath, string outputFileName)
	{
		string outputDir = Path.Combine (baseOutputDirectory, MonoAndroidHelper.ArchToRid (targetArch), WrappedSubDirectory);
		Directory.CreateDirectory (outputDir);

		string outputFile = Path.Combine (outputDir, outputFileName);
		log.LogDebugMessage ($"[{targetArch}] Wrapping '{payloadFilePath}' into loadable-symbol shared library '{outputFile}'");
		using var payload = File.OpenRead (payloadFilePath);
		using var output = File.Create (outputFile);
		AssemblyStoreElfWriter.Write (payload, output, targetArch, outputFileName);

		return outputFile;
	}
}
