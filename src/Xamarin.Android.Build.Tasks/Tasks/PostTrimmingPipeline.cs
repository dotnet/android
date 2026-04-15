#nullable enable

using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

/// <summary>
/// An MSBuild task that runs post-trimming assembly modifications in a single pass.
///
/// This opens each assembly once (via DirectoryAssemblyResolver with ReadWrite) and
/// runs all registered steps on it, then writes modified assemblies in-place. Currently
/// runs CheckForObsoletePreserveAttributeStep, StripEmbeddedLibrariesStep and
/// (optionally) AddKeepAlivesStep.
///
/// Runs in the inner build after ILLink but before ReadyToRun/crossgen2 compilation,
/// so that R2R images are generated from the already-modified assemblies.
/// </summary>
public class PostTrimmingPipeline : AndroidTask
{
	public override string TaskPrefix => "PTP";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	public bool AddKeepAlives { get; set; }

	public bool AndroidLinkResources { get; set; }

	public bool Deterministic { get; set; }

	public override bool RunTask ()
	{
		using var resolver = new DirectoryAssemblyResolver (
			this.CreateTaskLogger (), loadDebugSymbols: true,
			loadReaderParameters: new ReaderParameters { ReadWrite = true });
		var cache = new TypeDefinitionCache ();

		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec) ?? "");
			if (!resolver.SearchDirectories.Contains (dir)) {
				resolver.SearchDirectories.Add (dir);
			}
		}

		// Pre-load all assemblies once so that every step (and the processing loop)
		// operates on the same AssemblyDefinition instances.
		var loadedAssemblies = new List<(ITaskItem item, AssemblyDefinition assembly)> (Assemblies.Length);
		foreach (var item in Assemblies) {
			loadedAssemblies.Add ((item, resolver.GetAssembly (item.ItemSpec)));
		}

		var steps = new List<IAssemblyModifierPipelineStep> ();
		steps.Add (new CheckForObsoletePreserveAttributeStep (Log));
		steps.Add (new StripEmbeddedLibrariesStep (Log));
		if (AndroidLinkResources) {
			var allAssemblies = new List<AssemblyDefinition> (loadedAssemblies.Count);
			foreach (var (_, assembly) in loadedAssemblies) {
				allAssemblies.Add (assembly);
			}
			steps.Add (new RemoveResourceDesignerStep (allAssemblies, (msg) => Log.LogDebugMessage (msg)));
		}

		// FixAbstractMethods — resolve Mono.Android once up front. If resolution fails, log
		// the error and skip running the fix step entirely to avoid later unhandled exceptions.
		AssemblyDefinition? monoAndroidAssembly = null;
		try {
			monoAndroidAssembly = resolver.Resolve (AssemblyNameReference.Parse ("Mono.Android"));
		} catch (AssemblyResolutionException ex) {
			Log.LogErrorFromException (ex, showStackTrace: false);
		}
		if (monoAndroidAssembly != null) {
			steps.Add (new PostTrimmingFixAbstractMethodsStep (cache,
				() => monoAndroidAssembly,
				(msg) => Log.LogDebugMessage (msg),
				(msg) => Log.LogCodedWarning ("XA2000", msg)));
		}

		if (AddKeepAlives) {
			// Memoize the corlib resolution so the attempt (and any error logging) happens at most once,
			// regardless of how many assemblies/methods need KeepAlive injection.
			AssemblyDefinition? corlibAssembly = null;
			bool corlibResolutionAttempted = false;
			steps.Add (new PostTrimmingAddKeepAlivesStep (cache,
				() => {
					if (!corlibResolutionAttempted) {
						corlibResolutionAttempted = true;
						try {
							corlibAssembly = resolver.Resolve (AssemblyNameReference.Parse ("System.Private.CoreLib"));
						} catch (AssemblyResolutionException ex) {
							Log.LogErrorFromException (ex, showStackTrace: false);
						}
					}
					return corlibAssembly;
				},
				(msg) => Log.LogDebugMessage (msg)));
		}

		foreach (var (item, assembly) in loadedAssemblies) {
			var context = new StepContext (item, item);
			foreach (var step in steps) {
				step.ProcessAssembly (assembly, context);
			}
			if (context.IsAssemblyModified) {
				Log.LogDebugMessage ($"  Writing modified assembly: {item.ItemSpec}");
				assembly.Write (new WriterParameters {
					WriteSymbols = assembly.MainModule.HasSymbols,
					DeterministicMvid = Deterministic,
				});
			}
		}

		return !Log.HasLoggedErrors;
	}
}
