using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	public class StripEmbeddedLibraries : BaseStep
	{
		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (!Annotations.HasAction (assembly))
				return;
			var action = Annotations.GetAction (assembly);
			if (action == AssemblyAction.Skip || action == AssemblyAction.Delete)
				return;

			var fileName = assembly.Name.Name + ".dll";
			if (MonoAndroidHelper.IsFrameworkAssembly (fileName) &&
					!MonoAndroidHelper.FrameworkEmbeddedJarLookupTargets.Contains (fileName) &&
					!MonoAndroidHelper.FrameworkEmbeddedNativeLibraryAssemblies.Contains (fileName))
				return;

			bool assembly_modified = false;
			foreach (var mod in assembly.Modules) {
				foreach (var r in mod.Resources.ToArray ()) {
					if (ShouldStripResource (r)) {
						Context.LogMessage ($"    Stripped {r.Name} from {fileName}");
						mod.Resources.Remove (r);
						assembly_modified = true;
					}
				}
			}
			if (assembly_modified && action == AssemblyAction.Copy) {
				Annotations.SetAction (assembly, AssemblyAction.Save);
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
