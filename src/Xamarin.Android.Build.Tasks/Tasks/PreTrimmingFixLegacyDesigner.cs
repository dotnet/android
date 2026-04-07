#nullable enable

using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Runs <see cref="FixLegacyResourceDesignerStep"/> on assemblies that are about to be
/// trimmed by ILLink. This rewrites library assemblies so their resource field accesses
/// (ldsfld) become calls to the designer assembly's property getters.
///
/// Running this *before* ILLink means the trimmer sees the rewritten IL and can freely
/// trim unused designer types/fields. This avoids the need to root the entire designer
/// assembly during trimming (which causes an APK size regression).
/// </summary>
public class PreTrimmingFixLegacyDesigner : AndroidTask
{
	public override string TaskPrefix => "PTD";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	[Required]
	public string TargetName { get; set; } = "";

	public bool Deterministic { get; set; }

	public override bool RunTask ()
	{
		using var resolver = new DirectoryAssemblyResolver (
			this.CreateTaskLogger (), loadDebugSymbols: true,
			loadReaderParameters: new ReaderParameters { ReadWrite = true });

		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec) ?? "");
			if (!resolver.SearchDirectories.Contains (dir)) {
				resolver.SearchDirectories.Add (dir);
			}
		}

		var linkContext = new MSBuildLinkContext (resolver, Log);
		var fixLegacyStep = new FixLegacyResourceDesignerStep ();
		fixLegacyStep.Initialize (linkContext);

		foreach (var item in Assemblies) {
			// Match the filtering in FixLegacyResourceDesignerStep.ProcessAssembly:
			// skip the main assembly and framework/BCL assemblies.
			if (Path.GetFileNameWithoutExtension (item.ItemSpec) == TargetName) {
				continue;
			}
			if (MonoAndroidHelper.IsFrameworkAssembly (item)) {
				continue;
			}

			var assembly = resolver.GetAssembly (item.ItemSpec);
			if (fixLegacyStep.ProcessAssemblyDesigner (assembly)) {
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
