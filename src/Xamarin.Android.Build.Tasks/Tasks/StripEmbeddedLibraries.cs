#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

/// <summary>
/// An MSBuild task that strips embedded Android resources (.jar, __AndroidNativeLibraries__.zip,
/// __AndroidLibraryProjects__.zip, __AndroidEnvironment__) from trimmed assemblies.
///
/// This runs in the inner build after ILLink but before ReadyToRun/crossgen2 compilation,
/// so that R2R images are generated from the already-stripped assemblies.
/// </summary>
public class StripEmbeddedLibraries : AndroidTask
{
	public override string TaskPrefix => "SEL";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	public bool Deterministic { get; set; }

	public override bool RunTask ()
	{
		var resolver = new DefaultAssemblyResolver ();
		var searchDirectories = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec) ?? "");
			if (searchDirectories.Add (dir)) {
				resolver.AddSearchDirectory (dir);
			}
		}

		try {
			foreach (var assembly in Assemblies) {
				if (MonoAndroidHelper.IsFrameworkAssembly (assembly)) {
					continue;
				}

				StripAssembly (assembly.ItemSpec, resolver);
			}
		} finally {
			resolver.Dispose ();
		}

		return !Log.HasLoggedErrors;
	}

	void StripAssembly (string assemblyPath, IAssemblyResolver resolver)
	{
		string pdbPath = Path.ChangeExtension (assemblyPath, ".pdb");
		bool havePdb = File.Exists (pdbPath);

		var readerParams = new ReaderParameters {
			ReadSymbols = havePdb,
			ReadWrite = true,
			AssemblyResolver = resolver,
		};

		bool assembly_modified = false;

		using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams)) {
			foreach (var module in assembly.Modules) {
				foreach (var resource in module.Resources.ToArray ()) {
					if (ShouldStripResource (resource)) {
						Log.LogDebugMessage ($"  Stripped {resource.Name} from {assembly.Name.Name}.dll");
						module.Resources.Remove (resource);
						assembly_modified = true;
					}
				}
			}

			if (!assembly_modified) {
				return;
			}

			Log.LogDebugMessage ($"  Writing stripped assembly: {assemblyPath}");
			assembly.Write (new WriterParameters {
				WriteSymbols = havePdb,
				DeterministicMvid = Deterministic,
			});
		}
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
