using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

sealed class ArchitectureMarshalMethods
{
	public readonly IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods;
	public readonly ICollection<AssemblyDefinition> Assemblies;

	public ArchitectureMarshalMethods (IDictionary<string, IList<MarshalMethodEntry>> marshalMethods, ICollection<AssemblyDefinition> assemblies)
	{
		MarshalMethods = marshalMethods;
		Assemblies = assemblies;
	}

	public ArchitectureMarshalMethods ()
	{
		MarshalMethods = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.OrdinalIgnoreCase);
		Assemblies = new List<AssemblyDefinition> ();
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
	readonly MarshalMethodsClassifier classifier;
	readonly AndroidTargetArch templateArch;
	readonly Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> allAssembliesPerArch;
	readonly TaskLoggingHelper log;

	public MarshalMethodsMirrorHelper (MarshalMethodsClassifier classifier, AndroidTargetArch templateArch, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> allAssembliesPerArch, TaskLoggingHelper log)
	{
		this.classifier = classifier;
		this.templateArch = templateArch;
		this.allAssembliesPerArch = allAssembliesPerArch;
		this.log = log;
	}

	public IDictionary<AndroidTargetArch, ArchitectureMarshalMethods> Reflect ()
	{
		var ret = new Dictionary<AndroidTargetArch, ArchitectureMarshalMethods> ();

		foreach (var kvp in allAssembliesPerArch) {
			AndroidTargetArch arch = kvp.Key;
			IDictionary<string, ITaskItem> assemblies = kvp.Value;

			if (arch == templateArch) {
				ret.Add (arch, new ArchitectureMarshalMethods (classifier.MarshalMethods, classifier.Assemblies));
				continue;
			}

			ret.Add (arch, Reflect (arch, assemblies));
		}

		return ret;
	}

	ArchitectureMarshalMethods Reflect (AndroidTargetArch arch, IDictionary<string, ITaskItem> archAssemblies)
	{
		var ret = new ArchitectureMarshalMethods ();
		var cache = new Dictionary<string, AssemblyDefinition> (StringComparer.OrdinalIgnoreCase);

		log.LogDebugMessage ($"Reflecting marshal methods for architecture {arch}");
		foreach (var kvp in classifier.MarshalMethods) {
			foreach (MarshalMethodEntry templateMethod in kvp.Value) {
				ReflectMethod (arch, templateMethod, archAssemblies, ret, cache);
			}
		}

		return ret;
	}

	void ReflectMethod (AndroidTargetArch arch, MarshalMethodEntry templateMethod, IDictionary<string, ITaskItem> archAssemblies, ArchitectureMarshalMethods archMarshalMethods, Dictionary<string, AssemblyDefinition> cache)
	{
		string? assemblyName = templateMethod.NativeCallback.DeclaringType.Module?.Assembly?.Name?.Name;
		if (String.IsNullOrEmpty (assemblyName)) {
			throw new InvalidOperationException ($"Unable to obtain assembly name for method {templateMethod}");
		}

		if (!cache.TryGetValue (assemblyName, out AssemblyDefinition assembly)) {
			assembly = LoadAssembly (arch, assemblyName, archAssemblies, cache);
			cache.Add (assemblyName, assembly);
		}

		throw new NotImplementedException ();
	}

	AssemblyDefinition LoadAssembly (AndroidTargetArch arch, string assemblyName, IDictionary<string, ITaskItem> archAssemblies, Dictionary<string, AssemblyDefinition> cache)
	{
		if (!archAssemblies.TryGetValue (assemblyName, out ITaskItem assemblyItem)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assemblyName}' not found for architecture '{arch}'");
		}

		throw new NotImplementedException ();
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
