#nullable enable
using System;
using System.Collections.Generic;
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

namespace MonoDroid.Tuner;

/// <summary>
/// Scans an assembly for JLOs that need JCWs generated and writes them to an XML file.
/// </summary>
public class FindJavaObjectsStep : BaseStep
{
	public string ApplicationJavaClass { get; set; } = "";

	public bool ErrorOnCustomJavaObject { get; set; }

	public bool UseMarshalMethods { get; set; }

	public TaskLoggingHelper Log { get; set; }

	public FindJavaObjectsStep (TaskLoggingHelper log) => Log = log;

	public bool ProcessAssembly (AssemblyDefinition assembly, string destinationJLOXml)
	{
		var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

		if (action == AssemblyAction.Delete)
			return false;

		var types = ScanForJavaTypes (assembly);
		var initial_count = types.Count;

		// Filter out Java types we don't care about
		types = types.Where (t => !t.IsInterface && !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();

		Log.LogDebugMessage ($"{assembly.Name.Name} - Found {initial_count} Java types, filtered to {types.Count}");

		var wrappers = ConvertToCallableWrappers (types);
		XmlExporter.Export (destinationJLOXml, wrappers, true);

		return true;
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
			DefaultMonoRuntimeInitialization = "mono.MonoPackageManager.LoadApplication (context);",
		};

		if (UseMarshalMethods)
			reader_options.MethodClassifier = new MarshalMethodsClassifier (Context, Context.Resolver, Log);

		foreach (var type in types) {
			var wrapper = CecilImporter.CreateType (type, Context, reader_options);
			wrappers.Add (wrapper);
		}

		return wrappers;
	}
}
