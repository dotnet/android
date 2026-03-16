#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks;

/// <summary>
/// An MSBuild task that injects GC.KeepAlive() calls into binding methods of trimmed assemblies.
///
/// This runs in the inner build after ILLink but before ReadyToRun/crossgen2 compilation,
/// so that R2R images are generated from the already-modified assemblies.
/// </summary>
public class AddKeepAlives : AndroidTask
{
	public override string TaskPrefix => "AKA";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	public bool Deterministic { get; set; }

	public override bool RunTask ()
	{
		var resolver = new DefaultAssemblyResolver ();
		var cache = new TypeDefinitionCache ();
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

				ProcessAssembly (assembly.ItemSpec, resolver, cache);
			}
		} finally {
			resolver.Dispose ();
		}

		return !Log.HasLoggedErrors;
	}

	void ProcessAssembly (string assemblyPath, IAssemblyResolver resolver, IMetadataResolver cache)
	{
		string pdbPath = Path.ChangeExtension (assemblyPath, ".pdb");
		bool havePdb = File.Exists (pdbPath);

		var readerParams = new ReaderParameters {
			ReadSymbols = havePdb,
			ReadWrite = true,
			AssemblyResolver = resolver,
		};

		using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams)) {
			bool modified = AddKeepAlivesHelper.AddKeepAlives (
				assembly,
				cache,
				() => GetCorlibAssembly (resolver),
				(msg) => Log.LogDebugMessage (msg));

			if (!modified) {
				return;
			}

			Log.LogDebugMessage ($"  Writing modified assembly: {assemblyPath}");
			assembly.Write (new WriterParameters {
				WriteSymbols = havePdb,
				DeterministicMvid = Deterministic,
			});
		}
	}

	static AssemblyDefinition GetCorlibAssembly (IAssemblyResolver resolver)
	{
		return resolver.Resolve (AssemblyNameReference.Parse ("System.Private.CoreLib"));
	}
}
