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
/// MSBuild task that generates native LLVM assembly source files containing marshal methods and
/// optional P/Invoke preservation code. This task creates the final native code that bridges
/// between .NET and Java/JNI, using the marshal method classifications and rewritten assemblies
/// from previous pipeline stages.
/// </summary>
/// <remarks>
/// This task is responsible for the final code generation phase of the marshal methods pipeline:
/// 
/// 1. **Marshal Methods Generation**: Creates LLVM IR code for native marshal methods that can
///    be called directly from Java/JNI without dynamic registration overhead.
/// 
/// 2. **P/Invoke Preservation** (when EnableNativeRuntimeLinking=true): Generates additional 
///    code to preserve P/Invoke methods that would otherwise be removed by native linking.
/// 
/// 3. **Runtime-Specific Code**: Adapts the generated code for the target runtime (MonoVM or CoreCLR),
///    handling differences in runtime linking and method resolution.
/// 
/// 4. **Architecture Support**: Generates separate code files for each supported Android ABI
///    (arm64-v8a, armeabi-v7a, x86_64, x86).
/// 
/// The task generates LLVM IR (.ll) files that are later compiled to native assembly by the
/// Android NDK toolchain. Even when marshal methods are disabled, empty files are generated
/// to maintain build consistency.
/// </remarks>
public class GenerateNativeMarshalMethodSources : AndroidTask
{
	/// <summary>
	/// Gets the task prefix used for logging and error messages.
	/// </summary>
	public override string TaskPrefix => "GNM";

	/// <summary>
	/// Gets or sets whether to generate managed marshal methods lookup tables.
	/// When enabled, creates runtime data structures for efficient marshal method resolution.
	/// </summary>
	public bool EnableManagedMarshalMethodsLookup { get; set; }

	/// <summary>
	/// Gets or sets whether marshal methods generation is enabled.
	/// When false, generates empty placeholder files to maintain build consistency.
	/// </summary>
	public bool EnableMarshalMethods { get; set; }

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
	/// Gets or sets the intermediate output directory path.
	/// Used to retrieve native code generation state from previous pipeline stages.
	/// </summary>
	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	/// <summary>
	/// Gets or sets the resolved assemblies to process for marshal method generation.
	/// These assemblies have been processed by previous pipeline stages.
	/// </summary>
	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	/// <summary>
	/// Gets or sets the target Android runtime (MonoVM or CoreCLR).
	/// Determines which runtime-specific code generator to use.
	/// </summary>
	[Required]
        public string AndroidRuntime { get; set; } = "";

	/// <summary>
	/// Gets or sets the satellite assemblies containing localized resources.
	/// These are included in assembly counting and naming for native code generation.
	/// </summary>
	public ITaskItem [] SatelliteAssemblies { get; set; } = [];

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
	/// 1. Parse the Android runtime type (MonoVM or CoreCLR)
	/// 2. Retrieve native code generation state from previous pipeline stages (if marshal methods enabled)
	/// 3. Generate LLVM IR files for each supported ABI
	/// 4. Handle both marshal methods and P/Invoke preservation code as needed
	/// 
	/// The native code generation state is removed from the cache after retrieval to ensure
	/// it's not accidentally reused by subsequent build tasks.
	/// </remarks>
	public override bool RunTask ()
	{
		NativeCodeGenStateCollection? nativeCodeGenStates = null;
		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		// Retrieve native code generation state only if we need it
		if (EnableMarshalMethods || EnableNativeRuntimeLinking) {
			// Retrieve the stored NativeCodeGenStateCollection (and remove it from the cache)
			nativeCodeGenStates = BuildEngine4.UnregisterTaskObjectAssemblyLocal<NativeCodeGenStateCollection> (
				MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
				RegisteredTaskObjectLifetime.Build
			);
		}

		// Generate native code for each supported ABI
		foreach (var abi in SupportedAbis)
			Generate (nativeCodeGenStates, abi);

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Generates native LLVM IR source files for a specific Android ABI.
	/// Creates both marshal methods and optional P/Invoke preservation code.
	/// </summary>
	/// <param name="nativeCodeGenStates">
	/// Collection of native code generation states from previous pipeline stages.
	/// May be null if marshal methods are disabled.
	/// </param>
	/// <param name="abi">The target Android ABI to generate code for (e.g., "arm64-v8a").</param>
	/// <remarks>
	/// This method handles the complete code generation workflow:
	/// 
	/// 1. **Setup**: Determines target architecture, file paths, and assembly information
	/// 2. **Generator Creation**: Creates runtime-specific code generators (MonoVM or CoreCLR)
	/// 3. **P/Invoke Preservation** (optional): Generates code to preserve P/Invoke methods
	/// 4. **Marshal Methods**: Generates the main marshal methods LLVM IR code
	/// 5. **File Output**: Writes generated code to disk with proper error handling
	/// 
	/// The generated files are:
	/// - `marshal_methods.{abi}.ll`: Main marshal methods LLVM IR
	/// - `pinvoke_preserve.{abi}.ll`: P/Invoke preservation code (when native linking enabled)
	/// 
	/// Both generators construct an LLVM IR module and then generate the actual code,
	/// with proper stream management and error recovery in case of partial writes.
	/// </remarks>
	void Generate (NativeCodeGenStateCollection? nativeCodeGenStates, string abi)
	{
		// Setup target information and file paths
		var targetAbi = abi.ToLowerInvariant ();
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		var marshalMethodsBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"marshal_methods.{targetAbi}");
		var pinvokePreserveBaseAsmFilePath = EnableNativeRuntimeLinking ? Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}") : null;
		var marshalMethodsLlFilePath = $"{marshalMethodsBaseAsmFilePath}.ll";
		var pinvokePreserveLlFilePath = pinvokePreserveBaseAsmFilePath != null ? $"{pinvokePreserveBaseAsmFilePath}.ll" : null;
		var (assemblyCount, uniqueAssemblyNames) = GetAssemblyCountAndUniqueNames ();

		// Create the appropriate runtime-specific generator
		MarshalMethodsNativeAssemblyGenerator marshalMethodsAsmGen = androidRuntime switch {
			Tasks.AndroidRuntime.MonoVM => MakeMonoGenerator (),
			Tasks.AndroidRuntime.CoreCLR => MakeCoreCLRGenerator (),
			_ => throw new NotSupportedException ($"Internal error: unsupported runtime type '{androidRuntime}'")
		};

		// Generate P/Invoke preservation code if native linking is enabled
		bool fileFullyWritten;
		if (EnableNativeRuntimeLinking) {
			var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, EnsureCodeGenState (nativeCodeGenStates, targetArch), MonoComponents);
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

		/// <summary>
		/// Creates a MonoVM-specific marshal methods generator.
		/// Handles both enabled and disabled marshal methods scenarios.
		/// </summary>
		/// <returns>A configured MonoVM marshal methods generator.</returns>
		MarshalMethodsNativeAssemblyGenerator MakeMonoGenerator ()
		{
			if (EnableMarshalMethods) {
				return new MarshalMethodsNativeAssemblyGeneratorMonoVM (
					Log,
					assemblyCount,
					uniqueAssemblyNames,
					EnsureCodeGenState (nativeCodeGenStates, targetArch),
					EnableManagedMarshalMethodsLookup
				);
			}

			// Generate empty/minimal code when marshal methods are disabled
			return new MarshalMethodsNativeAssemblyGeneratorMonoVM (
				Log,
				targetArch,
				assemblyCount,
				uniqueAssemblyNames
			);
		}

		/// <summary>
		/// Creates a CoreCLR-specific marshal methods generator.
		/// Handles both enabled and disabled marshal methods scenarios.
		/// </summary>
		/// <returns>A configured CoreCLR marshal methods generator.</returns>
		MarshalMethodsNativeAssemblyGenerator MakeCoreCLRGenerator ()
		{
			if (EnableMarshalMethods) {
				return new MarshalMethodsNativeAssemblyGeneratorCoreCLR (
					Log,
					uniqueAssemblyNames,
					EnsureCodeGenState (nativeCodeGenStates, targetArch),
					EnableManagedMarshalMethodsLookup
				);
			}

			// Generate empty/minimal code when marshal methods are disabled
			return new MarshalMethodsNativeAssemblyGeneratorCoreCLR (
				Log,
				targetArch,
				uniqueAssemblyNames
			);
		}
	}

	/// <summary>
	/// Counts the total number of assemblies and collects unique assembly names
	/// from both resolved assemblies and satellite assemblies.
	/// </summary>
	/// <returns>
	/// A tuple containing:
	/// - assemblyCount: The total number of unique assemblies across all architectures
	/// - uniqueAssemblyNames: A set of unique assembly names including culture information
	/// </returns>
	/// <remarks>
	/// This method processes both main assemblies and satellite assemblies (for localization).
	/// For satellite assemblies, the culture name is prepended to create unique identifiers
	/// (e.g., "en-US/MyApp.resources.dll"). This information is used by the native code
	/// generators to create appropriate lookup structures and assembly metadata.
	/// </remarks>
	(int assemblyCount, HashSet<string> uniqueAssemblyNames) GetAssemblyCountAndUniqueNames ()
	{
		var assemblyCount = 0;
		var archAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var uniqueAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		// Process both main assemblies and satellite assemblies
		foreach (var assembly in SatelliteAssemblies.Concat (ResolvedAssemblies)) {
			var culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
			var fileName = Path.GetFileName (assembly.ItemSpec);
			string assemblyName;

			// Include culture information for satellite assemblies
			if (culture.IsNullOrEmpty ()) {
				assemblyName = fileName;
			} else {
				assemblyName = $"{culture}/{fileName}";
			}

			// Track all unique assembly names across architectures
			uniqueAssemblyNames.Add (assemblyName);

			// Count unique assemblies per architecture to avoid duplicates
			if (!archAssemblyNames.Contains (assemblyName)) {
				assemblyCount++;
				archAssemblyNames.Add (assemblyName);
			}
		}

		return (assemblyCount, uniqueAssemblyNames);
	}

	/// <summary>
	/// Retrieves the native code generation state for a specific target architecture.
	/// Validates that the required state exists and throws an exception if missing.
	/// </summary>
	/// <param name="nativeCodeGenStates">
	/// The collection of native code generation states from previous pipeline stages.
	/// </param>
	/// <param name="targetArch">The target architecture to retrieve state for.</param>
	/// <returns>The native code generation state for the specified architecture.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the state collection is null or doesn't contain state for the target architecture.
	/// </exception>
	/// <remarks>
	/// This method ensures that the required native code generation state is available
	/// before attempting to generate marshal methods code. The state contains marshal method
	/// classifications, assembly information, and other data needed for code generation.
	/// </remarks>
	NativeCodeGenStateObject EnsureCodeGenState (NativeCodeGenStateCollection? nativeCodeGenStates, AndroidTargetArch targetArch)
	{
		if (nativeCodeGenStates is null || !nativeCodeGenStates.States.TryGetValue (targetArch, out NativeCodeGenStateObject? state)) {
			throw new InvalidOperationException ($"Internal error: missing native code generation state for architecture '{targetArch}'");
		}

		return state;
	}
}
