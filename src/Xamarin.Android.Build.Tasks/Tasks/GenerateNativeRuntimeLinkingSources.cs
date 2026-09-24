#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates P/Invoke preservation code for native runtime linking.
/// </summary>
/// <remarks>
/// The generated LLVM IR (.ll) is compiled with the other application native sources.
/// </remarks>
public class GenerateNativeRuntimeLinkingSources : AndroidTask
{
	/// <summary>
	/// Gets the task prefix used for logging and error messages.
	/// </summary>
	public override string TaskPrefix => "GNM";

	/// <summary>
	/// Gets or sets the Mono runtime components to include in the build.
	/// Used for P/Invoke preservation.
	/// </summary>
	public ITaskItem[] MonoComponents { get; set; } = [];

	/// <summary>
	/// Gets or sets the output directory for environment files.
	/// Generated LLVM IR files are written to this directory.
	/// </summary>
	[Required]
	public string EnvironmentOutputDirectory { get; set; } = "";

	/// <summary>
	/// Gets or sets the resolved assemblies to process.
	/// These assemblies are scanned for P/Invokes when native linking is enabled.
	/// </summary>
	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of supported Android ABIs to generate code for.
	/// Common values include arm64-v8a, armeabi-v7a, x86_64, and x86.
	/// </summary>
	[Required]
	public string [] SupportedAbis { get; set; } = [];

	/// <summary>
	/// Generates the P/Invoke preservation sources for all supported Android ABIs.
	/// </summary>
	/// <returns>
	/// true if the task completed successfully; false if errors occurred during processing.
	/// </returns>
	/// <remarks>
	/// Scans framework assemblies for P/Invokes and emits LLVM IR for each ABI.
	/// </remarks>
	public override bool RunTask ()
	{
		foreach (var abi in SupportedAbis)
			Generate (abi);

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Generates a native LLVM IR P/Invoke preservation source for an ABI.
	/// </summary>
	/// <param name="abi">The target Android ABI to generate code for (e.g., "arm64-v8a").</param>
	/// <remarks>
	/// Writes <c>pinvoke_preserve.{abi}.ll</c>.
	/// </remarks>
	void Generate (string abi)
	{
		// Setup target information and file paths
		var targetAbi = abi.ToLowerInvariant ();
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		var pinvokePreserveLlFilePath = Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}.ll");

		var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, targetArch, ScanPInvokes (targetArch), MonoComponents);
		LLVMIR.LlvmIrModule pinvokePreserveModule = pinvokePreserveGen.Construct ();
		using var pinvokePreserveWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
		bool fileFullyWritten = false;
		try {
			pinvokePreserveGen.Generate (pinvokePreserveModule, targetArch, pinvokePreserveWriter, pinvokePreserveLlFilePath);
			pinvokePreserveWriter.Flush ();
			Files.CopyIfStreamChanged (pinvokePreserveWriter.BaseStream, pinvokePreserveLlFilePath);
			fileFullyWritten = true;
		} finally {
			if (!fileFullyWritten) {
				MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{pinvokePreserveLlFilePath}'", pinvokePreserveWriter.BaseStream);
			}
		}
	}

	List<PinvokeScanner.PinvokeEntryInfo> ScanPInvokes (AndroidTargetArch arch)
	{
		// Generated trimmable type map assemblies are attached only to the first ABI.
		var assemblies = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: false);
		if (!assemblies.TryGetValue (arch, out var archAssemblies)) {
			throw new InvalidOperationException ($"No resolved assemblies for architecture '{arch}'.");
		}
		var frameworkAssemblies = archAssemblies.Values
			.Where (assembly => bool.TryParse (assembly.GetMetadata ("FrameworkAssembly"), out bool isFramework) && isFramework)
			.ToList ();
		using var resolver = MonoAndroidHelper.MakeResolver (Log, arch, archAssemblies, loadDebugSymbols: false);
		return new PinvokeScanner (Log).Scan (arch, resolver, frameworkAssemblies);
	}

}
