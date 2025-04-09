#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Scans an assembly for JLOs that need to be in the typemap and writes them to an XML file.
/// </summary>
public class FindTypeMapObjectsStep : BaseStep
{
	public bool Debug { get; set; }

	public bool ErrorOnCustomJavaObject { get; set; }

	public TaskLoggingHelper Log { get; set; }

	public FindTypeMapObjectsStep (TaskLoggingHelper log) => Log = log;

	public bool ProcessAssembly (AssemblyDefinition assembly, string destinationTypeMapXml)
	{
		var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

		if (action == AssemblyAction.Delete)
			return false;

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
				return false;
			}
		} else {
			var genState = TypeMapCecilAdapter.GetReleaseGenerationState (types, Context, out var foundJniNativeRegistration);
			xml.ModuleReleaseData = genState.TempModules.SingleOrDefault ().Value;

			if (xml.ModuleReleaseData == null) {
				Log.LogDebugMessage ($"No Java types found in '{assembly.Name.Name}'");
				return false;
			}
		}

		xml.Export (destinationTypeMapXml);

		Log.LogDebugMessage ($"Wrote '{destinationTypeMapXml}', {xml.JavaToManagedDebugEntries.Count} JavaToManagedDebugEntries, {xml.ManagedToJavaDebugEntries.Count} ManagedToJavaDebugEntries, FoundJniNativeRegistration: {xml.FoundJniNativeRegistration}");

		return true;
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
