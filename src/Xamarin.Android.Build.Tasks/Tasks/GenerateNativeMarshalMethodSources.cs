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
/// Generates an empty native marshal-method compatibility source and optional P/Invoke
/// preservation code. The compatibility source supplies symbols expected by the native host.
/// </summary>
/// <remarks>
/// The generated LLVM IR (.ll) is compiled with the other application native sources.
/// </remarks>
public class GenerateNativeMarshalMethodSources : AndroidTask
{
	/// <summary>
	/// Gets the task prefix used for logging and error messages.
	/// </summary>
	public override string TaskPrefix => "GNM";

	/// <summary>
	/// Gets or sets whether native runtime linking is enabled.
	/// When true, generates additional P/Invoke preservation code to prevent
	/// native linker from removing required methods.
	/// </summary>
	public bool EnableNativeRuntimeLinking { get; set; }

	/// <summary>
	/// Gets or sets the Mono runtime components to include in the build.
	/// Used for P/Invoke preservation when native linking is enabled.
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
	/// Gets or sets the target Android runtime.
	/// </summary>
	[Required]
	public string AndroidRuntime { get; set; } = "";

	/// <summary>
	/// Gets or sets the list of supported Android ABIs to generate code for.
	/// Common values include arm64-v8a, armeabi-v7a, x86_64, and x86.
	/// </summary>
	[Required]
	public string [] SupportedAbis { get; set; } = [];

	// Parsed Android runtime type
	AndroidRuntime androidRuntime;

	/// <summary>
	/// Executes the native marshal method source generation task.
	/// Coordinates the generation of LLVM IR files for all supported Android ABIs.
	/// </summary>
	/// <returns>
	/// true if the task completed successfully; false if errors occurred during processing.
	/// </returns>
	/// <remarks>
	/// The execution flow is:
	/// 
	/// 1. Parse the Android runtime type
	/// 2. Scan framework assemblies for P/Invokes when native linking is enabled
	/// 3. Generate LLVM IR files for each supported ABI
	/// 4. Write the empty marshal-method source
	/// </remarks>
	public override bool RunTask ()
	{
		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		// Generate native code for each supported ABI
		foreach (var abi in SupportedAbis)
			Generate (abi);

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Generates native LLVM IR source files for a specific Android ABI.
	/// Creates the empty marshal-method source and optional P/Invoke preservation code.
	/// </summary>
	/// <param name="abi">The target Android ABI to generate code for (e.g., "arm64-v8a").</param>
	/// <remarks>
	/// This method handles the complete code generation workflow:
	/// 
	/// 1. **Setup**: Determines target architecture, file paths, and assembly information
	/// 2. **Generator Creation**: Creates the empty compatibility source
	/// 3. **P/Invoke Preservation** (optional): Generates code to preserve P/Invoke methods
	/// 4. **Compatibility Stub**: Generates the empty marshal-method LLVM IR
	/// 5. **File Output**: Writes generated code to disk with proper error handling
	/// 
	/// The generated files are:
	/// - `marshal_methods.{abi}.ll`: Empty marshal-method compatibility LLVM IR
	/// - `pinvoke_preserve.{abi}.ll`: P/Invoke preservation code (when native linking enabled)
	/// 
	/// Both generators construct an LLVM IR module and then generate the actual code,
	/// with proper stream management and error recovery in case of partial writes.
	/// </remarks>
	void Generate (string abi)
	{
		// Setup target information and file paths
		var targetAbi = abi.ToLowerInvariant ();
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		var marshalMethodsBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"marshal_methods.{targetAbi}");
		var pinvokePreserveBaseAsmFilePath = EnableNativeRuntimeLinking ? Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}") : null;
		var marshalMethodsLlFilePath = $"{marshalMethodsBaseAsmFilePath}.ll";
		var pinvokePreserveLlFilePath = pinvokePreserveBaseAsmFilePath != null ? $"{pinvokePreserveBaseAsmFilePath}.ll" : null;
		// Generate the native host's empty marshal-method compatibility symbols.
		MarshalMethodsNativeAssemblyGenerator marshalMethodsAsmGen = androidRuntime switch {
			Tasks.AndroidRuntime.CoreCLR => new MarshalMethodsNativeAssemblyGenerator (Log),
			_ => throw new NotSupportedException ($"Internal error: unsupported runtime type '{androidRuntime}'")
		};

		// Generate P/Invoke preservation code if native linking is enabled
		bool fileFullyWritten;
		if (EnableNativeRuntimeLinking) {
			var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, targetArch, ScanPInvokes (targetArch), MonoComponents);
			LLVMIR.LlvmIrModule pinvokePreserveModule = pinvokePreserveGen.Construct ();
			using var pinvokePreserveWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
			fileFullyWritten = false;
			try {
				pinvokePreserveGen.Generate (pinvokePreserveModule, targetArch, pinvokePreserveWriter, pinvokePreserveLlFilePath!);
				pinvokePreserveWriter.Flush ();
				Files.CopyIfStreamChanged (pinvokePreserveWriter.BaseStream, pinvokePreserveLlFilePath!);
				fileFullyWritten = true;
			} finally {
				// Log partial contents for debugging if generation failed
				if (!fileFullyWritten) {
					MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{pinvokePreserveLlFilePath}'", pinvokePreserveWriter.BaseStream);
				}
			}
		}

		// Generate marshal methods code
		var marshalMethodsModule = marshalMethodsAsmGen.Construct ();
		using var marshalMethodsWriter = MemoryStreamPool.Shared.CreateStreamWriter ();

		fileFullyWritten = false;
		try {
			marshalMethodsAsmGen.Generate (marshalMethodsModule, targetArch, marshalMethodsWriter, marshalMethodsLlFilePath);
			marshalMethodsWriter.Flush ();
			Files.CopyIfStreamChanged (marshalMethodsWriter.BaseStream, marshalMethodsLlFilePath);
			fileFullyWritten = true;
		} finally {
			// Log partial contents for debugging if generation failed
			if (!fileFullyWritten) {
				MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{marshalMethodsLlFilePath}'", marshalMethodsWriter.BaseStream);
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
