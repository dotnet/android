#nullable enable

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

		foreach (var item in Assemblies) {
			if (MonoAndroidHelper.IsFrameworkAssembly (item)) {
				continue;
			}

			var assembly = resolver.GetAssembly (item.ItemSpec);

			bool modified = AddKeepAlivesHelper.AddKeepAlives (
				assembly,
				cache,
				() => resolver.Resolve (AssemblyNameReference.Parse ("System.Private.CoreLib")),
				(msg) => Log.LogDebugMessage (msg));

			if (!modified) {
				continue;
			}

			Log.LogDebugMessage ($"  Writing modified assembly: {item.ItemSpec}");
			assembly.Write (new WriterParameters {
				WriteSymbols = assembly.MainModule.HasSymbols,
				DeterministicMvid = Deterministic,
			});
		}

		return !Log.HasLoggedErrors;
	}
}
