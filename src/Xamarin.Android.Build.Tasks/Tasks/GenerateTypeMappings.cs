using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

// Note: If/When this is converted to an incremental task, every build still needs to set:
// NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent
public class GenerateTypeMappings : AndroidTask
{
	public override string TaskPrefix => "GTM";

	[Required]
	public string AndroidRuntime { get; set; } = "";

	public bool Debug { get; set; }

	public bool EnableMarshalMethods { get;set; }

	[Output]
	public ITaskItem [] GeneratedBinaryTypeMaps { get; set; } = [];

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	public bool SkipJniAddNativeMethodRegistrationAttributeScan { get; set; }

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	// This property is temporary and is used to ensure that the new "linker step"
	// JLO scanning produces the same results as the old process. It will be removed
	// once the process is complete.
	public bool RunCheckedBuild { get; set; }

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public string TypemapImplementation { get; set; } = "llvm-ir";

	[Required]
	public string TypemapOutputDirectory { get; set; } = "";

	AndroidRuntime androidRuntime;

	public override bool RunTask ()
	{
		var useMarshalMethods = !Debug && EnableMarshalMethods;

		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);
		if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
			// NativeAOT typemaps are generated in `Microsoft.Android.Sdk.ILLink.TypeMappingStep`
			Log.LogDebugMessage ("Skipping type maps for NativeAOT.");
			return !Log.HasLoggedErrors;
		}

		// If using marshal methods, we cannot use the .typemap.xml files currently because
		// the type token ids were changed by the marshal method rewriter after we wrote the .xml files.
		//if (!useMarshalMethods)
			GenerateAllTypeMappings ();

		// Generate typemaps from the native code generator state (produced by the marshal method rewriter)
		//if (RunCheckedBuild || useMarshalMethods)
		//	GenerateAllTypeMappingsFromNativeState (useMarshalMethods);

		return !Log.HasLoggedErrors;
	}

	void GenerateAllTypeMappings ()
	{
		var allAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: true);

		foreach (var set in allAssembliesPerArch)
			GenerateTypeMap (set.Key, set.Value.Values.ToList ());
	}

	void GenerateTypeMap (AndroidTargetArch arch, List<ITaskItem> assemblies)
	{
		Log.LogDebugMessage ($"Generating type maps for architecture '{arch}'");

		var state = TypeMapObjectsFileAdapter.Create (arch, assemblies, Log);

		// An error was already logged to Log.LogError
		if (state is null)
			return;

		if (TypemapImplementation != "llvm-ir") {
			Log.LogDebugMessage ($"TypemapImplementation='{TypemapImplementation}' will write an empty native typemap.");
			state.XmlFiles.Clear ();
		}

		var tmg = new TypeMapGenerator (Log, state, androidRuntime);
		tmg.Generate (Debug, SkipJniAddNativeMethodRegistrationAttributeScan, TypemapOutputDirectory);

		// Set for use by <GeneratePackageManagerJava/> task later
		NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent = state.JniAddNativeMethodRegistrationAttributePresent;

		AddOutputTypeMaps (tmg, state.TargetArch);
	}

	void GenerateAllTypeMappingsFromNativeState (bool useMarshalMethods)
	{
		// Retrieve the stored NativeCodeGenState
		var nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		NativeCodeGenState? templateCodeGenState = null;

		foreach (var kvp in nativeCodeGenStates) {
			NativeCodeGenState state = kvp.Value;
			templateCodeGenState = state;
			GenerateTypeMapFromNativeState (state, useMarshalMethods);
		}

		if (templateCodeGenState is null)
			throw new InvalidOperationException ($"Internal error: no native code generator state defined");

		// Set for use by <GenerateNativeApplicationConfigSources/> task later
		if (useMarshalMethods)
			NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent = templateCodeGenState.JniAddNativeMethodRegistrationAttributePresent;
	}

	void GenerateTypeMapFromNativeState (NativeCodeGenState state, bool useMarshalMethods)
	{
		if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
			// NativeAOT typemaps are generated in `Microsoft.Android.Sdk.ILLink.TypeMappingStep`
			Log.LogDebugMessage ("Skipping type maps for NativeAOT.");
			return;
		}
		Log.LogDebugMessage ($"Generating type maps from native state for architecture '{state.TargetArch}' (RunCheckedBuild = {RunCheckedBuild})");

		if (TypemapImplementation != "llvm-ir") {
			Log.LogDebugMessage ($"TypemapImplementation='{TypemapImplementation}' will write an empty native typemap.");
			state = new NativeCodeGenState (state.TargetArch, new TypeDefinitionCache (), state.Resolver, [], [], state.Classifier);
		}

		var tmg = new TypeMapGenerator (Log, new NativeCodeGenStateAdapter (state), androidRuntime) { RunCheckedBuild = RunCheckedBuild && !useMarshalMethods };
		tmg.Generate (Debug, SkipJniAddNativeMethodRegistrationAttributeScan, TypemapOutputDirectory);

		AddOutputTypeMaps (tmg, state.TargetArch);
	}

	void AddOutputTypeMaps (TypeMapGenerator tmg, AndroidTargetArch arch)
	{
		string abi = MonoAndroidHelper.ArchToAbi (arch);
		var items = new List<ITaskItem> ();

		foreach (string file in tmg.GeneratedBinaryTypeMaps) {
			var item = new TaskItem (file);
			string fileName = Path.GetFileName (file);
			item.SetMetadata ("DestinationSubPath", $"{abi}/{fileName}");
			item.SetMetadata ("DestinationSubDirectory", $"{abi}/");
			item.SetMetadata ("Abi", abi);
			items.Add (item);
		}

		GeneratedBinaryTypeMaps = GeneratedBinaryTypeMaps.Concat (items).ToArray ();
	}
}
