#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateTypeMappings : AndroidTask
{
	public override string TaskPrefix => "GTM";

	[Required]
	public string AndroidRuntime { get; set; } = "";

	public bool Debug { get; set; }

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	public bool SkipJniAddNativeMethodRegistrationAttributeScan { get; set; }

	[Required]
	public string TypemapOutputDirectory { get; set; } = "";

	[Output]
	public ITaskItem [] GeneratedBinaryTypeMaps { get; set; } = [];

	public string TypemapImplementation { get; set; } = "llvm-ir";

	AndroidRuntime androidRuntime;

	public override bool RunTask ()
	{
		androidRuntime = MonoAndroidHelper.ParseAndroidRuntime (AndroidRuntime);

		// Retrieve the stored NativeCodeGenState
		var nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>> (
			MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
			RegisteredTaskObjectLifetime.Build
		);

		NativeCodeGenState? templateCodeGenState = null;

		foreach (var kvp in nativeCodeGenStates) {
			NativeCodeGenState state = kvp.Value;
			templateCodeGenState = state;
			WriteTypeMappings (state);
		}

		if (templateCodeGenState is null)
			throw new InvalidOperationException ($"Internal error: no native code generator state defined");

		// Set for use by <GenerateNativeApplicationConfigSources> task later
		NativeCodeGenState.TemplateJniAddNativeMethodRegistrationAttributePresent = templateCodeGenState.JniAddNativeMethodRegistrationAttributePresent;

		return !Log.HasLoggedErrors;
	}

	void WriteTypeMappings (NativeCodeGenState state)
	{
		if (androidRuntime == Xamarin.Android.Tasks.AndroidRuntime.NativeAOT) {
			// NativeAOT typemaps are generated in `Microsoft.Android.Sdk.ILLink.TypeMappingStep`
			Log.LogDebugMessage ("Skipping type maps for NativeAOT.");
			return;
		}
		Log.LogDebugMessage ($"Generating type maps for architecture '{state.TargetArch}'");

		if (TypemapImplementation != "llvm-ir") {
			Log.LogDebugMessage ($"TypemapImplementation='{TypemapImplementation}' will write an empty native typemap.");
			state = new NativeCodeGenState (state.TargetArch, new TypeDefinitionCache (), state.Resolver, [], [], state.Classifier);
		}

		var tmg = new TypeMapGenerator (Log, state, androidRuntime);
		tmg.Generate (Debug, SkipJniAddNativeMethodRegistrationAttributeScan, TypemapOutputDirectory);

		string abi = MonoAndroidHelper.ArchToAbi (state.TargetArch);
		var items = new List<ITaskItem> ();
		foreach (string file in tmg.GeneratedBinaryTypeMaps) {
			var item = new TaskItem (file);
			string fileName = Path.GetFileName (file);
			item.SetMetadata ("DestinationSubPath", $"{abi}/{fileName}");
			item.SetMetadata ("DestinationSubDirectory", $"{abi}/");
			item.SetMetadata ("Abi", abi);
			items.Add (item);
		}

		GeneratedBinaryTypeMaps = items.ToArray ();
	}
}
