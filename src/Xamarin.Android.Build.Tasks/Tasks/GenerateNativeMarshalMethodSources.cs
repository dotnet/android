#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that generates P/Invoke preservation LLVM IR source files when native
/// runtime linking is enabled.  Creates <c>pinvoke_preserve.{abi}.ll</c> files for each
/// supported ABI.
/// </summary>
/// <remarks>
/// Marshal method <c>.ll</c> generation is handled entirely by the inner build's
/// <see cref="RewriteMarshalMethods"/> task.  This task only handles P/Invoke preservation:
/// when <see cref="EnableNativeRuntimeLinking"/> is true, it generates additional LLVM IR
/// code that prevents the native linker from removing required P/Invoke entry points.
/// </remarks>
public class GenerateNativeMarshalMethodSources : AndroidTask
{
	/// <summary>
	/// Gets the task prefix used for logging and error messages.
	/// </summary>
	public override string TaskPrefix => "GNM";

	/// <summary>
	/// Gets or sets whether native runtime linking is enabled.
	/// When true, generates P/Invoke preservation code to prevent
	/// native linker from removing required methods.
	/// </summary>
	public bool EnableNativeRuntimeLinking { get; set; }

	/// <summary>
	/// Gets or sets the Mono runtime components to include in the build.
	/// Used for P/Invoke preservation when native linking is enabled.
	/// </summary>
	public ITaskItem[] MonoComponents { get; set; } = [];

	/// <summary>
	/// Gets or sets the output directory for generated files.
	/// P/Invoke preservation LLVM IR files are written to this directory.
	/// </summary>
	[Required]
	public string EnvironmentOutputDirectory { get; set; } = "";

	/// <summary>
	/// Gets or sets the intermediate output directory path.
	/// Used to retrieve native code generation state from previous pipeline stages.
	/// </summary>
	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	/// <summary>
	/// Gets or sets the list of supported Android ABIs to generate code for.
	/// Common values include arm64-v8a, armeabi-v7a, x86_64, and x86.
	/// </summary>
	[Required]
	public string [] SupportedAbis { get; set; } = [];

	/// <summary>
	/// Executes the P/Invoke preservation source generation task.
	/// </summary>
	/// <returns>
	/// true if the task completed successfully; false if errors occurred during processing.
	/// </returns>
	/// <remarks>
	/// The execution flow is:
	///
	/// 1. Unregister (clean up) the native code generation state from the build engine cache
	/// 2. If native runtime linking is enabled, generate P/Invoke preservation LLVM IR for each ABI
	///
	/// The native code generation state is always unregistered to prevent accidental reuse,
	/// even if P/Invoke preservation is not needed.
	/// </remarks>
	public override bool RunTask ()
	{
		// Always unregister the NativeCodeGenStateCollection to clean up.
		// P/Invoke preservation needs the state when native runtime linking is enabled.
		var nativeCodeGenStates = BuildEngine4.UnregisterTaskObjectAssemblyLocal<NativeCodeGenStateCollection> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		foreach (var abi in SupportedAbis)
			Generate (nativeCodeGenStates, abi);

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Generates P/Invoke preservation LLVM IR source files for a specific Android ABI.
	/// </summary>
	/// <param name="nativeCodeGenStates">
	/// Collection of native code generation states from previous pipeline stages.
	/// Required when native runtime linking is enabled.
	/// </param>
	/// <param name="abi">The target Android ABI to generate code for (e.g., "arm64-v8a").</param>
	/// <remarks>
	/// When <see cref="EnableNativeRuntimeLinking"/> is false, this method returns immediately.
	/// Otherwise it generates <c>pinvoke_preserve.{abi}.ll</c> containing references to
	/// P/Invoke entry points that must survive native linking.
	/// </remarks>
	void Generate (NativeCodeGenStateCollection? nativeCodeGenStates, string abi)
	{
		if (!EnableNativeRuntimeLinking) {
			return;
		}

		// Generate P/Invoke preservation code
		var targetAbi = abi.ToLowerInvariant ();
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		var pinvokePreserveLlFilePath = Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}.ll");

		var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, EnsureCodeGenState (nativeCodeGenStates, targetArch), MonoComponents);
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

	/// <summary>
	/// Retrieves the native code generation state for a specific target architecture.
	/// Validates that the required state exists and throws an exception if missing.
	/// </summary>
	NativeCodeGenStateObject EnsureCodeGenState (NativeCodeGenStateCollection? nativeCodeGenStates, AndroidTargetArch targetArch)
	{
		if (nativeCodeGenStates is null || !nativeCodeGenStates.States.TryGetValue (targetArch, out NativeCodeGenStateObject? state)) {
			throw new InvalidOperationException ($"Internal error: missing native code generation state for architecture '{targetArch}'");
		}

		return state;
	}
}
