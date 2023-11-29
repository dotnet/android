using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

sealed class ArchitectureMarshalMethods
{
	public readonly IDictionary<string, IList<MarshalMethodEntry>> Methods;
	public readonly ICollection<AssemblyDefinition> Assemblies;

	public ArchitectureMarshalMethods (IDictionary<string, IList<MarshalMethodEntry>> marshalMethods, ICollection<AssemblyDefinition> assemblies)
	{
		Methods = marshalMethods;
		Assemblies = assemblies;
	}

	public ArchitectureMarshalMethods ()
	{
		Methods = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.OrdinalIgnoreCase);
		Assemblies = new HashSet<AssemblyDefinition> ();
	}
}

sealed class MarshalMethodsMirrorHelperState
{
	public readonly AndroidTargetArch TemplateArch;
	public readonly Dictionary<string, ITaskItem> TemplateArchAssemblies;
	public readonly MarshalMethodsClassifier Classifier;

	public AndroidTargetArch CurrentArch                        { get; set; } = AndroidTargetArch.None;
	public XAAssemblyResolverNew? CurrentArchResolver           { get; set; }
	public Dictionary<string, ITaskItem>? CurrentArchAssemblies { get; set; }

	public MarshalMethodsMirrorHelperState (AndroidTargetArch classifiedArch, Dictionary<string, ITaskItem> classifiedArchAssemblies, MarshalMethodsClassifier classifier)
	{
		TemplateArch = classifiedArch;
		TemplateArchAssemblies = classifiedArchAssemblies;
		Classifier = classifier;
	}
}

/// <summary>
/// <para>
/// Classifier contains types from assemblies that were in the architecture passed to the type scanner, so we can take a small shortcut in their case and use the types
/// as they are.  For the other architectures we will have to look up types by name, using the scanned arch types as template, because the
/// assemblies are in different locations and may differ as far as type and method identifiers are concerned.  They **have to**, however, contain all
/// the same types and methods.  We'll error out if we find any discrepancies.
/// </para>
/// <para>
/// This class performs the task of "mirroring" the template architecture assemblies in other architectures.  Performance might be worse, but we can't avoid it.
/// </para>
/// </summary>
class MarshalMethodsMirrorHelper
{
	readonly MarshalMethodsMirrorHelperState state;
	readonly TaskLoggingHelper log;

	public MarshalMethodsMirrorHelper (MarshalMethodsMirrorHelperState state, TaskLoggingHelper log)
	{
		this.state = state;
		this.log = log;
	}

	public ArchitectureMarshalMethods Reflect ()
	{
		if (state.CurrentArch == state.TemplateArch) {
			log.LogDebugMessage ($"Not reflecting marshal methods for architecture '{state.CurrentArch}' since it's the template architecture");
			return new ArchitectureMarshalMethods (state.Classifier.MarshalMethods, state.Classifier.Assemblies);
		}

		var ret = new ArchitectureMarshalMethods ();
		var assemblyCache = new Dictionary<string, AssemblyDefinition> (StringComparer.OrdinalIgnoreCase);
		var typeCache = new TypeDefinitionCache ();

		log.LogDebugMessage ($"Reflecting marshal methods for architecture {state.CurrentArch}");
		foreach (var kvp in state.Classifier.MarshalMethods) {
			foreach (MarshalMethodEntry templateMethod in kvp.Value) {
				ReflectMethod (templateMethod, ret, assemblyCache, typeCache);
			}
		}

		if (ret.Assemblies.Count != state.TemplateArchAssemblies.Count) {
			throw new InvalidOperationException ($"Internal error: expected to found {state.TemplateArchAssemblies.Count} assemblies for architecture '{state.CurrentArch}', but found {ret.Assemblies.Count} instead");
		}

		foreach (AssemblyDefinition templateAssembly in state.Classifier.Assemblies) {
			bool found = false;

			foreach (AssemblyDefinition assembly in ret.Assemblies) {
				if (String.Compare (templateAssembly.FullName, assembly.FullName, StringComparison.Ordinal) != 0) {
					continue;
				}
				found = true;
				break;
			}

			if (!found) {
				throw new InvalidOperationException ($"Internal error: assembly '{templateAssembly.FullName}' not found in assembly set for architecture '{state.CurrentArch}'");
			}
		}

		return ret;
	}

	void ReflectMethod (MarshalMethodEntry templateMethod, ArchitectureMarshalMethods archMethods, Dictionary<string, AssemblyDefinition> assemblyCache, TypeDefinitionCache typeCache)
	{
		TypeDefinition matchingType = GetMatchingType (templateMethod.NativeCallback, archMethods, assemblyCache, typeCache);
		MethodDefinition nativeCallback = FindMatchingMethod (templateMethod.NativeCallback, matchingType, "native callback");

		MarshalMethodEntry? archMethod = null;
		if (templateMethod.IsSpecial) {
			// All we need is the native callback in this case
			archMethod = new MarshalMethodEntry (
				matchingType,
				nativeCallback,
				templateMethod.JniTypeName,
				templateMethod.JniMethodName,
				templateMethod.JniMethodSignature
			);

			AddMethod (archMethod);
			return;
		}

		// This marshal method must have **all** the associated methods present
		matchingType = GetMatchingType (templateMethod.Connector, archMethods, assemblyCache, typeCache);
		MethodDefinition connector = FindMatchingMethod (templateMethod.Connector, matchingType, "connector");

		matchingType = GetMatchingType (templateMethod.RegisteredMethod, archMethods, assemblyCache, typeCache);
		MethodDefinition registered = FindMatchingMethod (templateMethod.RegisteredMethod, matchingType, "registered");

		matchingType = GetMatchingType (templateMethod.ImplementedMethod, archMethods, assemblyCache, typeCache);
		MethodDefinition implemented = FindMatchingMethod (templateMethod.ImplementedMethod, matchingType, "implemented");

		TypeDefinition? fieldMatchingType = GetMatchingType (templateMethod.CallbackField, archMethods, assemblyCache, typeCache);
		FieldDefinition? callbackField = null;
		if (fieldMatchingType != null) {// callback field is optional
			callbackField = FindMatchingField (templateMethod.CallbackField, fieldMatchingType, "callback");
		}

		archMethod = new MarshalMethodEntry (
			matchingType,
			nativeCallback,
			connector,
			registered,
			implemented,
			callbackField,
			templateMethod.JniTypeName,
			templateMethod.JniMethodName,
			templateMethod.JniMethodSignature,
			templateMethod.NeedsBlittableWorkaround
		);
		AddMethod (archMethod);

		void AddMethod (MarshalMethodEntry method)
		{
			string methodKey = method.GetStoreMethodKey (typeCache);
			if (!archMethods.Methods.TryGetValue (methodKey, out IList<MarshalMethodEntry> methodList)) {
				methodList = new List<MarshalMethodEntry> ();
				archMethods.Methods.Add (methodKey, methodList);
			}

			methodList.Add (method);
		}
	}

	TypeDefinition GetMatchingType (MethodDefinition? templateMethod, ArchitectureMarshalMethods archMethods, Dictionary<string, AssemblyDefinition> assemblyCache, TypeDefinitionCache typeCache)
	{
		if (templateMethod == null) {
			throw new ArgumentNullException (nameof (templateMethod));
		}

		return GetMatchingType (templateMethod.DeclaringType, archMethods, assemblyCache, typeCache);
	}

	TypeDefinition? GetMatchingType (FieldDefinition? templateField, ArchitectureMarshalMethods archMethods, Dictionary<string, AssemblyDefinition> assemblyCache, TypeDefinitionCache typeCache)
	{
		if (templateField == null) {
			return null;
		}

		return GetMatchingType (templateField.DeclaringType, archMethods, assemblyCache, typeCache);
	}

	TypeDefinition GetMatchingType (TypeDefinition templateDeclaringType, ArchitectureMarshalMethods archMethods, Dictionary<string, AssemblyDefinition> assemblyCache, TypeDefinitionCache typeCache)
	{
		string? assemblyName = templateDeclaringType.Module?.Assembly?.Name?.Name;
		if (String.IsNullOrEmpty (assemblyName)) {
			throw new InvalidOperationException ($"Unable to obtain assembly name");
		}
		assemblyName = $"{assemblyName}.dll";

		if (!assemblyCache.TryGetValue (assemblyName, out AssemblyDefinition assembly)) {
			assembly = LoadAssembly (assemblyName, assemblyCache);
			assemblyCache.Add (assemblyName, assembly);
		}

		if (!archMethods.Assemblies.Contains (assembly)) {
			archMethods.Assemblies.Add (assembly);
		}

		string templateTypeName = templateDeclaringType.FullName;
		log.LogDebugMessage ($"  looking for type '{templateTypeName}' ('{templateDeclaringType.Name}')");

		TypeDefinition? matchingType = typeCache.Resolve (templateDeclaringType);
		if (matchingType == null) {
			throw new InvalidOperationException ($"Unable to find type '{templateTypeName}'");
		}

		if (matchingType == null) {
			throw new InvalidOperationException ($"Unable to locate type '{templateDeclaringType.FullName}' in assembly '{assembly.FullName}'");
		}
		log.LogDebugMessage ("     type found");

		return matchingType;
	}

	MethodDefinition FindMatchingMethod (MethodDefinition? templateMethod, TypeDefinition type, string methodDescription)
	{
		if (templateMethod == null) {
			throw new ArgumentNullException (nameof (templateMethod));
		}

		string templateMethodName = templateMethod.FullName;
		log.LogDebugMessage ($"  looking for method '{templateMethodName}'");
		foreach (MethodDefinition method in type.Methods) {
			if (String.Compare (method.FullName, templateMethodName, StringComparison.Ordinal) != 0) {
				continue;
			}

			log.LogDebugMessage ("    found");
			return method;
		}

		throw new InvalidOperationException ($"Unable to locate {methodDescription} method '{templateMethod.FullName}' in '{type.FullName}'");
	}

	FieldDefinition? FindMatchingField (FieldDefinition? templateField, TypeDefinition type, string fieldDescription)
	{
		if (templateField == null) {
			return null;
		}

		string templateFieldName = templateField.FullName;
		log.LogDebugMessage ($"  looking for field '{templateFieldName}'");
		foreach (FieldDefinition field in type.Fields) {
			if (String.Compare (field.FullName, templateFieldName, StringComparison.Ordinal) != 0) {
				continue;
			}

			log.LogDebugMessage ("    found");
			return field;
		}

		return null;
	}

	AssemblyDefinition LoadAssembly (string assemblyName, Dictionary<string, AssemblyDefinition> cache)
	{
		if (state.CurrentArchResolver == null) {
			throw new InvalidOperationException ($"Internal error: resolver for architecture '{state.CurrentArch}' not set");
		}

		if (state.CurrentArchResolver.TargetArch != state.CurrentArch) {
			throw new InvalidOperationException ($"Internal error: resolver should target architecture '{state.CurrentArch}', but it targets '{state.CurrentArchResolver.TargetArch}' instead");
		}

		if (!state.CurrentArchAssemblies.TryGetValue (assemblyName, out ITaskItem assemblyItem)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assemblyName}' not found for architecture '{state.CurrentArch}'");
		}

		AssemblyDefinition? assembly = state.CurrentArchResolver.Resolve (assemblyName);
		if (assembly == null) {
			throw new InvalidOperationException ($"Internal error: assembly '{assemblyName}' cannot be resolved for architecture '{state.CurrentArch}'");
		}

		return assembly;
	}

	void ReflectType (AndroidTargetArch arch, string fullTypeName, IList<MarshalMethodEntry> templateMethods, IDictionary<string, ITaskItem> archAssemblies, ArchitectureMarshalMethods archMarshalMethods)
	{
		log.LogDebugMessage ($"  Marshal methods in: {fullTypeName}:");
		string[] parts = fullTypeName.Split (',');
		if (parts.Length != 2) {
			throw new InvalidOperationException ($"Internal error: invalid full type name '{fullTypeName}'");
		}

		string typeName = parts[0].Trim ();
		string assemblyName = parts[1].Trim ();
		if (!archAssemblies.TryGetValue (assemblyName, out ITaskItem assemblyItem)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assemblyName}' not found for architecture '{arch}'");
		}

		foreach (MarshalMethodEntry mme in templateMethods) {
			log.LogDebugMessage ($"    {mme.DeclaringType.FullName}.{mme.NativeCallback}");
		}
	}
}
