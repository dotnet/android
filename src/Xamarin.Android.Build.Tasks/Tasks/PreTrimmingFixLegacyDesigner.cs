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

/// <summary>
/// Runs <see cref="FixLegacyResourceDesignerStep"/> on assemblies that are about to be
/// trimmed by ILLink. This rewrites library assemblies so their resource field accesses
/// (ldsfld) become calls to the designer assembly's property getters.
///
/// Running this *before* ILLink means the trimmer sees the rewritten IL and can freely
/// trim unused designer types/fields. This avoids the need to root the entire designer
/// assembly during trimming (which causes an APK size regression).
///
/// Modified assemblies are written to <see cref="OutputDirectory"/> rather than in-place,
/// to avoid mutating files in the shared NuGet cache or shared intermediate output paths.
/// </summary>
public class PreTrimmingFixLegacyDesigner : AndroidTask
{
	public override string TaskPrefix => "PTD";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	[Required]
	public string TargetName { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	public bool Deterministic { get; set; }

	[Output]
	public ITaskItem []? ModifiedAssemblies { get; set; }

	public override bool RunTask ()
	{
		Directory.CreateDirectory (OutputDirectory);

		using var resolver = new DirectoryAssemblyResolver (
			this.CreateTaskLogger (), loadDebugSymbols: true);

		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec) ?? "");
			if (!resolver.SearchDirectories.Contains (dir)) {
				resolver.SearchDirectories.Add (dir);
			}
		}

		var linkContext = new MSBuildLinkContext (resolver, Log);
		var fixLegacyStep = new FixLegacyResourceDesignerStep ();
		fixLegacyStep.Initialize (linkContext);

		var modified = new List<ITaskItem> ();

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
				var outputPath = Path.Combine (OutputDirectory, Path.GetFileName (item.ItemSpec));
				Log.LogDebugMessage ($"  Writing modified assembly: {outputPath}");
				assembly.Write (outputPath, new WriterParameters {
					WriteSymbols = assembly.MainModule.HasSymbols,
					DeterministicMvid = Deterministic,
				});

				var outputItem = new TaskItem (outputPath);
				item.CopyMetadataTo (outputItem);
				outputItem.SetMetadata ("OriginalPath", item.ItemSpec);
				modified.Add (outputItem);
			}
		}

		ModifiedAssemblies = modified.ToArray ();

		return !Log.HasLoggedErrors;
	}
}
