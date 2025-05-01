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
public class FindJavaObjectsStep : BaseStep, IAssemblyModifierPipelineStep
{
	public string ApplicationJavaClass { get; set; } = "";

	public bool ErrorOnCustomJavaObject { get; set; }

	public bool UseMarshalMethods { get; set; }

	public TaskLoggingHelper Log { get; set; }

	public FindJavaObjectsStep (TaskLoggingHelper log) => Log = log;

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		var destinationJLOXml = JavaObjectsXmlFile.GetJavaObjectsXmlFilePath (context.Destination.ItemSpec);
		var scanned = ScanAssembly (assembly, context, destinationJLOXml);

		if (!scanned) {
			// We didn't scan for Java objects, so write an empty .xml file for later steps
			JavaObjectsXmlFile.WriteEmptyFile (destinationJLOXml, Log);
		}
	}

	public bool ScanAssembly (AssemblyDefinition assembly, StepContext context, string destinationJLOXml)
	{
		if (!ShouldScan (context))
			return false;

		var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;

		if (action == AssemblyAction.Delete)
			return false;

		var types = ScanForJavaTypes (assembly);
		var initial_count = types.Count;

		// Filter out Java types we don't care about
		types = types.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t, Context)).ToList ();

		Log.LogDebugMessage ($"{assembly.Name.Name} - Found {initial_count} Java types, filtered to {types.Count}");

		var xml = new JavaObjectsXmlFile ();

		xml.ACWMapEntries.AddRange (types.Select (t => ACWMapEntry.Create (t, Context)));
		xml.JavaCallableWrappers.AddRange (ConvertToCallableWrappers (types.Where (t => !t.IsInterface).ToList ()));

		xml.Export (destinationJLOXml);

		Log.LogDebugMessage ($"Wrote '{destinationJLOXml}', {xml.JavaCallableWrappers.Count} JCWs, {xml.ACWMapEntries.Count} ACWs");

		return true;
	}

	bool ShouldScan (StepContext context)
	{
		if (!context.IsAndroidAssembly)
			return false;

		// When marshal methods or non-JavaPeerStyle.XAJavaInterop1 are in use we do not want to skip non-user assemblies (such as Mono.Android) - we need to generate JCWs for them during
		// application build, unlike in Debug configuration or when marshal methods are disabled, in which case we use JCWs generated during Xamarin.Android
		// build and stored in a jar file.
		var useMarshalMethods = !context.IsDebug && context.EnableMarshalMethods;
		var shouldSkipNonUserAssemblies = !useMarshalMethods && context.CodeGenerationTarget == JavaPeerStyle.XAJavaInterop1;

		if (shouldSkipNonUserAssemblies && !context.IsUserAssembly) {
			Log.LogDebugMessage ($"Skipping assembly '{context.Source.ItemSpec}' because it is not a user assembly and we don't need JLOs from non-user assemblies");
			return false;
		}

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
