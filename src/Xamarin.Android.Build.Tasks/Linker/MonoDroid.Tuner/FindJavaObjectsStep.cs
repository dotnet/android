using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.Utilities;

namespace MonoDroid.Tuner;

public class FindJavaObjectsStep : BaseStep
{
	public bool Debug { get; set; }

	public bool ErrorOnCustomJavaObject { get; set; }

	public bool UseMarshalMethods { get; set; }

	public List<string> UserAssemblies { get; set; } = [];

	public JavaPeerStyle CodeGenerationTarget { get; set; }

	// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
	// be scanned for Java types.
	static readonly HashSet<string> SpecialAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
		"Java.Interop",
		"Mono.Android",
		"Mono.Android.Runtime",
	};

	//protected override void ProcessAssembly (AssemblyDefinition assembly)
	//{		
	//	var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

	//	if (action == AssemblyAction.Delete)
	//		return;

	//	// See if we should process this assembly
	//	if (!ShouldProcessAssembly (assembly))
	//		return;

	//	var types = ScanForJavaTypes (assembly);

	//	// Filter out Java types we don't care about
	//	types = types.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();

	//	var wrappers = ConvertToCallableWrappers (types);

	//	XmlExporter.Export (Path.Combine (@"C:\Users\jopobst\Desktop\javatypes", $"{assembly.Name.Name}.jlo.xml"), wrappers);
	//}

	public void ProcessAssembly (AssemblyDefinition assembly, string destination)
	{
		var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

		if (action == AssemblyAction.Delete)
			return;

		var destinationJLOXml = destination;

		// See if we should process this assembly
		if (!ShouldProcessAssembly (assembly)) {
			// We need to write an empty file for incremental builds
			WriteEmptyXmlFile (destinationJLOXml);
			return;
		}

		var types = ScanForJavaTypes (assembly);
		var initial_count = types.Count;

		// Filter out Java types we don't care about
		types = types.Where (t => !t.IsInterface && !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();

		LogMessage ($"{assembly.Name.Name} - Found {initial_count} Java types, filtered to {types.Count}");
		ExportAsCallableWrappers (destinationJLOXml, types);
		//ExportAsJavaTypeSystem (destination + "2", types);
	}

	bool ShouldProcessAssembly (AssemblyDefinition assembly)
	{
		// Don't bother scanning the assembly if it doesn't have a Java.Lang.Object reference
		if (!SpecialAssemblies.Contains (assembly.Name.Name) && !assembly.MainModule.HasTypeReference ("Java.Lang.Object") && !assembly.MainModule.AssemblyReferences.Any (r => r.Name == "Mono.Android" || r.Name == "Java.Interop")) {
			LogMessage ($"Skipping assembly '{assembly.Name.Name}' because it doesn't reference Java.Lang.Object");
			return false;
		}

		// If we don't need JLOs from non-user assemblies, skip scanning them
		var shouldSkipNonUserAssemblies = Debug && !UseMarshalMethods && CodeGenerationTarget == JavaPeerStyle.XAJavaInterop1;

		if (shouldSkipNonUserAssemblies && !UserAssemblies.Contains (assembly.Name.Name)) {
			LogMessage ($"Skipping assembly '{assembly.Name.Name}' because it is not a user assembly and we don't need JLOs from non-user assemblies");
			return false;
		}

		return true;
	}

	void ExportAsCallableWrappers (string destination, List<TypeDefinition> types)
	{
		var wrappers = ConvertToCallableWrappers (types);
		XmlExporter.Export (destination, wrappers);
	}

	//void ExportAsJavaTypeSystem (string destination, List<TypeDefinition> types)
	//{
	//	var collection = new JavaTypeCollection ();
	//	var options = new ApiImporterOptions {
	//		ImportAsReferenceTypes = false,
	//		ImportGeneratedTypes = true,
	//	};

	//	foreach (var type in types) {
	//		if (ManagedApiImporter.ParseType (type, collection, options) is JavaTypeModel model) {
	//			collection.AddType (model);
	//			model.Package.Types.Add (model);
	//		}
	//	}

	//	JavaXmlApiExporter.Save (collection, destination);
	//}

	public static void WriteEmptyXmlFile (string destination)
	{
		XmlExporter.Export (destination, []);
	}

	//public void ProcessAssemblyInternalJavaTypeSystem (AssemblyDefinition assembly)
	//{
	//	var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

	//	if (action == AssemblyAction.Delete)
	//		return;

	//	var collection = new JavaTypeCollection ();
	//	var options = new ApiImporterOptions ();

	//	ManagedApiImporter.Parse (assembly, collection, Context, options);

	//	var output_file = Path.Combine (@"C:\Users\jopobst\Desktop\javatypes", $"{assembly.Name}.javatypes.xml");
	//	File.Delete (output_file);

	//	JavaXmlApiExporter.Save (collection, output_file);
	//}

	List<TypeDefinition> ScanForJavaTypes (AssemblyDefinition assembly)
	{
		var types = new List<TypeDefinition> ();

		var scanner = new XAJavaTypeScanner (Xamarin.Android.Tools.AndroidTargetArch.None, null, Context) {
			ErrorOnCustomJavaObject = ErrorOnCustomJavaObject
		};

		foreach (ModuleDefinition md in assembly.Modules) {
			foreach (TypeDefinition td in md.Types) {
				scanner.AddJavaType (td, types);
			}
		}

		return types;
	}

	//List<TypeDefinition> FilterJavaTypes (List<TypeDefinition> types)
	//{
	//	return types.Where (t => !t.IsInterface && !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();
	//}

	List<CallableWrapperType> ConvertToCallableWrappers (List<TypeDefinition> types)
	{
		var wrappers = new List<CallableWrapperType> ();

		foreach (var type in types) {
			var wrapper = CecilImporter.CreateType (type, Context);
			wrappers.Add (wrapper);
		}

		return wrappers;
	}
}
