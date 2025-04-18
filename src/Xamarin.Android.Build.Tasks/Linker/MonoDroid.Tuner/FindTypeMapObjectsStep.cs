#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Scans an assembly for JLOs that need to be in the typemap and writes them to an XML file.
/// </summary>
public class FindTypeMapObjectsStep : BaseStep, IAssemblyModifierPipelineStep
{
	public bool Debug { get; set; }

	public bool ErrorOnCustomJavaObject { get; set; }

	public TaskLoggingHelper Log { get; set; }

	public FindTypeMapObjectsStep (TaskLoggingHelper log) => Log = log;

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		var destinationTypeMapXml = TypeMapObjectsXmlFile.GetTypeMapObjectsXmlFilePath (context.Destination.ItemSpec);

		// We only care about assemblies that can contains JLOs
		if (!context.IsAndroidAssembly) {
			Log.LogDebugMessage ($"Skipping assembly '{assembly.Name.Name}' because it is not an Android assembly");
			TypeMapObjectsXmlFile.WriteEmptyFile (destinationTypeMapXml, Log);
			return;
		}

		var types = ScanForJavaTypes (assembly);

		var xml = new TypeMapObjectsXmlFile {
			AssemblyName = assembly.Name.Name,
		};

		if (Debug) {
			var (javaToManaged, managedToJava) = TypeMapCecilAdapter.GetDebugNativeEntries (types, Context, out var foundJniNativeRegistration);

			xml.JavaToManagedDebugEntries.AddRange (javaToManaged);
			xml.ManagedToJavaDebugEntries.AddRange (managedToJava);
			xml.FoundJniNativeRegistration = foundJniNativeRegistration;

			if (!xml.HasDebugEntries) {
				Log.LogDebugMessage ($"No Java types found in '{assembly.Name.Name}'");
				TypeMapObjectsXmlFile.WriteEmptyFile (destinationTypeMapXml, Log);
				return;
			}
		} else {
			var genState = TypeMapCecilAdapter.GetReleaseGenerationState (types, Context, out var foundJniNativeRegistration);
			xml.ModuleReleaseData = genState.TempModules.SingleOrDefault ().Value;

			if (xml.ModuleReleaseData == null) {
				Log.LogDebugMessage ($"No Java types found in '{assembly.Name.Name}'");
				TypeMapObjectsXmlFile.WriteEmptyFile (destinationTypeMapXml, Log);
				return;
			}
		}

		xml.Export (destinationTypeMapXml);

		Log.LogDebugMessage ($"Wrote '{destinationTypeMapXml}', {xml.JavaToManagedDebugEntries.Count} JavaToManagedDebugEntries, {xml.ManagedToJavaDebugEntries.Count} ManagedToJavaDebugEntries, FoundJniNativeRegistration: {xml.FoundJniNativeRegistration}");
	}

	List<TypeDefinition> ScanForJavaTypes (AssemblyDefinition assembly)
	{
		var types = new List<TypeDefinition> ();

		var scanner = new XAJavaTypeScanner (Xamarin.Android.Tools.AndroidTargetArch.None, Log, Context) {
			ErrorOnCustomJavaObject = ErrorOnCustomJavaObject
		};

		foreach (ModuleDefinition md in assembly.Modules) {
			foreach (TypeDefinition td in md.Types) {
				scanner.AddJavaType (td, types);
			}
		}

		return types;
	}
}
