#nullable enable

using System;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

class StripEmbeddedLibrariesStep : IAssemblyModifierPipelineStep
{
	readonly TaskLoggingHelper log;

	public StripEmbeddedLibrariesStep (TaskLoggingHelper log)
	{
		this.log = log;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		if (MonoAndroidHelper.IsFrameworkAssembly (assembly))
			return;
		context.IsAssemblyModified |= StripEmbeddedLibraries (assembly, log);
	}

	internal static bool StripEmbeddedLibraries (AssemblyDefinition assembly, TaskLoggingHelper log)
	{
		bool modified = false;
		foreach (var module in assembly.Modules) {
			foreach (var resource in module.Resources.ToArray ()) {
				if (ShouldStripResource (resource)) {
					log.LogDebugMessage ($"  Stripped {resource.Name} from {assembly.Name.Name}.dll");
					module.Resources.Remove (resource);
					modified = true;
				}
			}
		}
		return modified;
	}

	/// <summary>
	/// Determines whether a resource should be stripped from the assembly.
	/// Matches the same criteria as the old ILLink StripEmbeddedLibraries step.
	/// </summary>
	internal static bool ShouldStripResource (Resource resource)
	{
		if (!(resource is EmbeddedResource))
			return false;
		// Embedded jars
		if (resource.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase))
			return true;
		// Embedded AndroidNativeLibrary archive
		if (resource.Name == "__AndroidNativeLibraries__.zip")
			return true;
		// Embedded AndroidResourceLibrary archive
		if (resource.Name == "__AndroidLibraryProjects__.zip")
			return true;
		// Embedded AndroidEnvironment items
		if (resource.Name.StartsWith ("__AndroidEnvironment__", StringComparison.Ordinal))
			return true;
		return false;
	}
}
