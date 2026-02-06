using System;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	public class StripEmbeddedLibrariesStep : IAssemblyModifierPipelineStep
	{
		public TaskLoggingHelper Log { get; }

		public StripEmbeddedLibrariesStep (TaskLoggingHelper log)
		{
			Log = log;
		}

		public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
		{
			if (context.IsFrameworkAssembly)
				return;

			bool assembly_modified = false;
			foreach (var mod in assembly.Modules) {
				foreach (var r in mod.Resources.ToArray ()) {
					if (ShouldStripResource (r)) {
						Log.LogDebugMessage ($"    Stripped {r.Name} from {assembly.Name.Name}.dll");
						mod.Resources.Remove (r);
						assembly_modified = true;
					}
				}
			}
			if (assembly_modified) {
				context.IsAssemblyModified = true;
			}
		}

		bool ShouldStripResource (Resource r)
		{
			if (!(r is EmbeddedResource))
				return false;
			// embedded jars
			if (r.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase))
				return true;
			// embedded AndroidNativeLibrary archive
			if (r.Name == "__AndroidNativeLibraries__.zip")
				return true;
			// embedded AndroidResourceLibrary archive
			if (r.Name == "__AndroidLibraryProjects__.zip")
				return true;
			// embedded AndroidEnvironment item
			if (r.Name.StartsWith ("__AndroidEnvironment__", StringComparison.Ordinal))
				return true;
			return false;
		}
	}
}
