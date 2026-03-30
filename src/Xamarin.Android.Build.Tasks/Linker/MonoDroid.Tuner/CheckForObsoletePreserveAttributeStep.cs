#nullable enable

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Post-trimming step that warns when an assembly references the obsolete
/// Android.Runtime.PreserveAttribute. Runs as part of PostTrimmingPipeline
/// so the assemblies are already loaded by Mono.Cecil and the check is free.
/// </summary>
class CheckForObsoletePreserveAttributeStep : IAssemblyModifierPipelineStep
{
	readonly TaskLoggingHelper log;

	public CheckForObsoletePreserveAttributeStep (TaskLoggingHelper log)
	{
		this.log = log;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		if (HasObsoletePreserveAttribute (assembly)) {
			log.LogCodedWarning ("IL6001", $"Assembly '{assembly.Name.Name}' contains reference to obsolete attribute 'Android.Runtime.PreserveAttribute'. Members with this attribute may be trimmed. Please use System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute instead");
		}
	}

	static bool HasObsoletePreserveAttribute (AssemblyDefinition assembly)
	{
		foreach (var module in assembly.Modules) {
			foreach (var typeRef in module.GetTypeReferences ()) {
				if (typeRef.Namespace == "Android.Runtime" && typeRef.Name == "PreserveAttribute") {
					return true;
				}
			}
		}
		return false;
	}
}
