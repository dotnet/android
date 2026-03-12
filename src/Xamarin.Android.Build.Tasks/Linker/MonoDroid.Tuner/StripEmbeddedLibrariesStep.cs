#nullable enable

using System;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

/// <summary>
/// An IAssemblyModifierPipelineStep that strips embedded Android resources (.jar,
/// __AndroidNativeLibraries__.zip, __AndroidLibraryProjects__.zip, __AndroidEnvironment__)
/// from assemblies in non-trimmed builds. This step is added to LinkAssembliesNoShrink's pipeline.
/// </summary>
public class StripEmbeddedLibrariesStep : IAssemblyModifierPipelineStep
{
	public TaskLoggingHelper Log { get; set; }

	public StripEmbeddedLibrariesStep (TaskLoggingHelper log)
	{
		Log = log;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		// Skip framework assemblies -- they do not have embedded Android resources
		if (context.IsFrameworkAssembly)
			return;

		foreach (var module in assembly.Modules) {
			foreach (var resource in module.Resources.ToArray ()) {
				if (StripEmbeddedLibraries.ShouldStripResource (resource)) {
					Log.LogDebugMessage ($"  Stripped {resource.Name} from {assembly.Name.Name}.dll");
					module.Resources.Remove (resource);
					context.IsAssemblyModified = true;
				}
			}
		}
	}
}
