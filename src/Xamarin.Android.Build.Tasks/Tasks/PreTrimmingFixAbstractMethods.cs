#nullable enable

using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks;

public class PreTrimmingFixAbstractMethods : AndroidTask
{
	public override string TaskPrefix => "PTA";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	[Required]
	public string TargetName { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	public bool Deterministic { get; set; }

	[Output]
	public ITaskItem [] ModifiedAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		Directory.CreateDirectory (OutputDirectory);

		using var resolver = new DirectoryAssemblyResolver (
			this.CreateTaskLogger (), loadDebugSymbols: true);
		foreach (var item in Assemblies) {
			var directory = Path.GetDirectoryName (Path.GetFullPath (item.ItemSpec));
			if (directory != null && !resolver.SearchDirectories.Contains (directory)) {
				resolver.SearchDirectories.Add (directory);
			}
		}

		var context = new MSBuildLinkContext (resolver, Log);
		var step = new FixAbstractMethodsStep ();
		step.Initialize (context);
		var modified = new List<ITaskItem> ();

		// Resolve the app's binding assemblies before resolving references from
		// libraries compiled against an older version of the same binding.
		foreach (var item in Assemblies) {
			if (IsPostprocessAssembly (item) &&
			    resolver.Load (item.ItemSpec, forceLoad: true) == null) {
				throw new FileNotFoundException ("Could not load prelink assembly.", item.ItemSpec);
			}
		}

		foreach (var item in Assemblies) {
			if (Path.GetFileNameWithoutExtension (item.ItemSpec) == TargetName ||
			    !IsPostprocessAssembly (item)) {
				continue;
			}

			var assembly = resolver.GetAssembly (item.ItemSpec);
			var outputPath = Path.Combine (OutputDirectory, Path.GetFileName (item.ItemSpec));
			if (step.FixAbstractMethods (assembly)) {
				Log.LogDebugMessage ($"  Writing modified assembly: {outputPath}");
				var temporaryPath = outputPath + ".tmp.dll";
				var temporarySymbols = Path.ChangeExtension (temporaryPath, ".pdb");
				var outputSymbols = Path.ChangeExtension (outputPath, ".pdb");
				try {
					assembly.Write (temporaryPath, new WriterParameters {
						WriteSymbols = assembly.MainModule.HasSymbols,
						DeterministicMvid = Deterministic,
					});
					Files.CopyIfChanged (temporaryPath, outputPath);
					if (File.Exists (temporarySymbols)) {
						Files.CopyIfChanged (temporarySymbols, outputSymbols);
					} else if (File.Exists (outputSymbols)) {
						File.Delete (outputSymbols);
					}
				} finally {
					if (File.Exists (temporaryPath)) {
						File.Delete (temporaryPath);
					}
					if (File.Exists (temporarySymbols)) {
						File.Delete (temporarySymbols);
					}
				}
				modified.Add (new TaskItem (outputPath));
			} else {
				if (File.Exists (outputPath)) {
					File.Delete (outputPath);
				}
				var outputSymbols = Path.ChangeExtension (outputPath, ".pdb");
				if (File.Exists (outputSymbols)) {
					File.Delete (outputSymbols);
				}
			}
		}

		ModifiedAssemblies = modified.ToArray ();
		return !Log.HasLoggedErrors;
	}

	static bool IsPostprocessAssembly (ITaskItem item) =>
		bool.TryParse (item.GetMetadata ("PostprocessAssembly"), out var postprocess) &&
		postprocess && !MonoAndroidHelper.IsFrameworkAssembly (item);
}
