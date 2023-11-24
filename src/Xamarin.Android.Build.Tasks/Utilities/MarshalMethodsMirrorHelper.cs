using System;
using System.Collections.Generic;

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
	MarshalMethodsClassifier classifier;
	AndroidTargetArch templateArch;
	Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> allAssembliesPerArch;
	TaskLoggingHelper log;

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
		var marshalMethods = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.Ordinal);
		var assemblyDefinitions = new List<AssemblyDefinition> ();

		Console.WriteLine ();
		Console.WriteLine ($"Reflecting marshal methods for architecture {arch}");
		foreach (var kvp in classifier.MarshalMethods) {
			Console.WriteLine ($"Marshal methods in: {kvp.Key}:");
			foreach (MarshalMethodEntry mme in kvp.Value) {
				Console.WriteLine ($"  {mme.DeclaringType.FullName}.{mme.NativeCallback}");
			}
		}

		return new ArchitectureMarshalMethods (marshalMethods, assemblyDefinitions);
	}
}
