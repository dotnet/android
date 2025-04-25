using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Creates the native assembly containing LLVM marshal methods. Note an empty file is
/// generated if EnableMarshalMethods = false, so this must be called either way.
/// </summary>
public class GenerateNativeMarshalMethodSources : AndroidTask
{
	public override string TaskPrefix => "GNM";

	public bool EnableManagedMarshalMethodsLookup { get; set; }

	public bool EnableMarshalMethods { get; set; }

	public bool EnableNativeRuntimeLinking { get; set; }

	public ITaskItem[] MonoComponents { get; set; } = [];

	[Required]
	public string EnvironmentOutputDirectory { get; set; } = "";

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
        public string AndroidRuntime { get; set; } = "";

	public ITaskItem [] SatelliteAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	AndroidRuntime androidRuntime;

	public override bool RunTask ()
	{
		NativeCodeGenStateCollection? nativeCodeGenStates = null;
		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		if (EnableMarshalMethods || EnableNativeRuntimeLinking) {
			// Retrieve the stored NativeCodeGenStateCollection (and remove it from the cache)
			nativeCodeGenStates = BuildEngine4.UnregisterTaskObjectAssemblyLocal<NativeCodeGenStateCollection> (
				MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateObjectRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
				RegisteredTaskObjectLifetime.Build
			);
		}

		foreach (var abi in SupportedAbis)
			Generate (nativeCodeGenStates, abi);

		return !Log.HasLoggedErrors;
	}

	void Generate (NativeCodeGenStateCollection? nativeCodeGenStates, string abi)
	{
		var targetAbi = abi.ToLowerInvariant ();
		var targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
		var marshalMethodsBaseAsmFilePath = Path.Combine (EnvironmentOutputDirectory, $"marshal_methods.{targetAbi}");
		var pinvokePreserveBaseAsmFilePath = EnableNativeRuntimeLinking ? Path.Combine (EnvironmentOutputDirectory, $"pinvoke_preserve.{targetAbi}") : null;
		var marshalMethodsLlFilePath = $"{marshalMethodsBaseAsmFilePath}.ll";
		var pinvokePreserveLlFilePath = pinvokePreserveBaseAsmFilePath != null ? $"{pinvokePreserveBaseAsmFilePath}.ll" : null;
		var (assemblyCount, uniqueAssemblyNames) = GetAssemblyCountAndUniqueNames ();

		MarshalMethodsNativeAssemblyGenerator marshalMethodsAsmGen = androidRuntime switch {
			Tasks.AndroidRuntime.MonoVM => MakeMonoGenerator (),
			Tasks.AndroidRuntime.CoreCLR => MakeCoreCLRGenerator (),
			_ => throw new NotSupportedException ($"Internal error: unsupported runtime type '{androidRuntime}'")
		};

		if (EnableNativeRuntimeLinking) {
			var pinvokePreserveGen = new PreservePinvokesNativeAssemblyGenerator (Log, EnsureCodeGenState (nativeCodeGenStates, targetArch), MonoComponents);
			LLVMIR.LlvmIrModule pinvokePreserveModule = pinvokePreserveGen.Construct ();
			using var pinvokePreserveWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
			try {
				pinvokePreserveGen.Generate (pinvokePreserveModule, targetArch, pinvokePreserveWriter, pinvokePreserveLlFilePath!);
			} catch {
				throw;
			} finally {
				pinvokePreserveWriter.Flush ();
				Files.CopyIfStreamChanged (pinvokePreserveWriter.BaseStream, pinvokePreserveLlFilePath!);
			}
		}

		var marshalMethodsModule = marshalMethodsAsmGen.Construct ();
		using var marshalMethodsWriter = MemoryStreamPool.Shared.CreateStreamWriter ();

		try {
			marshalMethodsAsmGen.Generate (marshalMethodsModule, targetArch, marshalMethodsWriter, marshalMethodsLlFilePath);
		} finally {
			marshalMethodsWriter.Flush ();
			Files.CopyIfStreamChanged (marshalMethodsWriter.BaseStream, marshalMethodsLlFilePath);
		}

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

			return new MarshalMethodsNativeAssemblyGeneratorMonoVM (
				Log,
				targetArch,
				assemblyCount,
				uniqueAssemblyNames
			);
		}

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

			return new MarshalMethodsNativeAssemblyGeneratorCoreCLR (
				Log,
				targetArch,
				uniqueAssemblyNames
			);
		}
	}

	(int assemblyCount, HashSet<string> uniqueAssemblyNames) GetAssemblyCountAndUniqueNames ()
	{
		var assemblyCount = 0;
		var archAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var uniqueAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var assembly in SatelliteAssemblies.Concat (ResolvedAssemblies)) {
			var culture = MonoAndroidHelper.GetAssemblyCulture (assembly);
			var fileName = Path.GetFileName (assembly.ItemSpec);
			string assemblyName;

			if (string.IsNullOrEmpty (culture)) {
				assemblyName = fileName;
			} else {
				assemblyName = $"{culture}/{fileName}";
			}

			uniqueAssemblyNames.Add (assemblyName);

			if (!archAssemblyNames.Contains (assemblyName)) {
				assemblyCount++;
				archAssemblyNames.Add (assemblyName);
			}
		}

		return (assemblyCount, uniqueAssemblyNames);
	}

	NativeCodeGenStateObject EnsureCodeGenState (NativeCodeGenStateCollection? nativeCodeGenStates, AndroidTargetArch targetArch)
	{
		if (nativeCodeGenStates is null || !nativeCodeGenStates.States.TryGetValue (targetArch, out NativeCodeGenStateObject? state)) {
			throw new InvalidOperationException ($"Internal error: missing native code generation state for architecture '{targetArch}'");
		}

		return state;
	}
}
