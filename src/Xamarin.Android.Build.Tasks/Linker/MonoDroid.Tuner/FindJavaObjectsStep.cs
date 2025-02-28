using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Microsoft.Android.Build.Tasks;

using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.Utilities;

namespace MonoDroid.Tuner;

public class FindJavaObjectsStep : BaseStep
{
	public string ApplicationJavaClass { get; set; }

	public bool Debug { get; set; }

	public bool ErrorOnCustomJavaObject { get; set; }

	public bool UseMarshalMethods { get; set; }

	public List<string> UserAssemblies { get; set; } = [];

	public JavaPeerStyle CodeGenerationTarget { get; set; }

	public TaskLoggingHelper Log { get; set; }

	// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
	// be scanned for Java types.
	static readonly HashSet<string> SpecialAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
		"Java.Interop",
		"Mono.Android",
		"Mono.Android.Runtime",
	};

	public bool ProcessAssembly (AssemblyDefinition assembly, string destination, bool hasMonoAndroidReference)
	{
		var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

		if (action == AssemblyAction.Delete)
			return false;

		var destinationJLOXml = destination;

		// See if we should process this assembly
		//if (!ShouldProcessAssembly (assembly, hasMonoAndroidReference)) {
		//	// We need to write an empty file for incremental builds
		//	WriteEmptyXmlFile (destinationJLOXml);
		//	return false;
		//}

		var types = ScanForJavaTypes (assembly);
		var initial_count = types.Count;

		// Filter out Java types we don't care about
		types = types.Where (t => !t.IsInterface && !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();

		LogMessage ($"{assembly.Name.Name} - Found {initial_count} Java types, filtered to {types.Count}");
		ExportAsCallableWrappers (destinationJLOXml, types);
		//ExportAsJavaTypeSystem (destination + "2", types);

		return true;
	}

	bool ShouldProcessAssembly (AssemblyDefinition assembly, bool hasMonoAndroidReference)
	{
		// Don't bother scanning the assembly if it doesn't have a Java.Lang.Object reference
		if (!hasMonoAndroidReference && !SpecialAssemblies.Contains (assembly.Name.Name) && !assembly.MainModule.HasTypeReference ("Java.Lang.Object") && !assembly.MainModule.AssemblyReferences.Any (r => r.Name == "Mono.Android" || r.Name == "Java.Interop")) {
			LogMessage ($"Skipping assembly '{assembly.Name.Name}' because it doesn't reference Java.Lang.Object");
			return false;
		}

		// If we don't need JLOs from non-user assemblies, skip scanning them
		var shouldSkipNonUserAssemblies = (Debug || !UseMarshalMethods) && CodeGenerationTarget == JavaPeerStyle.XAJavaInterop1;

		if (shouldSkipNonUserAssemblies && !UserAssemblies.Contains (assembly.Name.Name)) {
			LogMessage ($"Skipping assembly '{assembly.Name.Name}' because it is not a user assembly and we don't need JLOs from non-user assemblies");
			return false;
		}

		return true;
	}

	void ExportAsCallableWrappers (string destination, List<TypeDefinition> types)
	{
		var wrappers = ConvertToCallableWrappers (types);
		XmlExporter.Export (destination, wrappers, true);
	}

	void LogMessage (string message)
	{
		Console.WriteLine (message);
	}

	public static void WriteEmptyXmlFile (string destination)
	{
		XmlExporter.Export (destination, [], false);
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

	List<CallableWrapperType> ConvertToCallableWrappers (List<TypeDefinition> types)
	{
		var wrappers = new List<CallableWrapperType> ();

		var reader_options = new CallableWrapperReaderOptions {
			DefaultApplicationJavaClass = ApplicationJavaClass,
			DefaultGenerateOnCreateOverrides = false, // this was used only when targetting Android API <= 10, which is no longer supported
			DefaultMonoRuntimeInitialization = "mono.MonoPackageManager.LoadApplication (context);",
		};

		if (UseMarshalMethods) {
			Log.LogDebugMessage ("Using MarshalMethodsClassifier");
			reader_options.MethodClassifier = MakeClassifier ();
		}

		foreach (var type in types) {
			var wrapper = CecilImporter.CreateType (type, Context, reader_options);
			wrappers.Add (wrapper);
		}

		return wrappers;
	}

	MarshalMethodsClassifier MakeClassifier () => new MarshalMethodsClassifier (Context, Context.Resolver, Log);
}
