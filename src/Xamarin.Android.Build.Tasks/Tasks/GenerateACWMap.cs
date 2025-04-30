using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateACWMap : AndroidTask
{
	public override string TaskPrefix => "ACW";

	[Required]
	public string AcwMapFile { get; set; } = "";

	[Required]
	public string IntermediateOutputDirectory { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	// This property is temporary and is used to ensure that the new "linker step"
	// JLO scanning produces the same results as the old process. It will be removed
	// once the process is complete.
	public bool RunCheckedBuild { get; set; }

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public override bool RunTask ()
	{
		// Temporarily used to ensure we still generate the same as the old code
		if (RunCheckedBuild) {
			// Retrieve the stored NativeCodeGenState
			var nativeCodeGenStates = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>> (
				MonoAndroidHelper.GetProjectBuildSpecificTaskObjectKey (GenerateJavaStubs.NativeCodeGenStateRegisterTaskKey, WorkingDirectory, IntermediateOutputDirectory),
				RegisteredTaskObjectLifetime.Build
			);

			// We only need the first architecture, since this task is architecture-agnostic
			var templateCodeGenState = nativeCodeGenStates.First ().Value;

			var acwMapGen = new ACWMapGenerator (Log);

			if (!acwMapGen.Generate (templateCodeGenState, AcwMapFile)) {
				Log.LogDebugMessage ("ACW map generation failed");
			}

			return !Log.HasLoggedErrors;
		}

		GenerateMap ();

		return !Log.HasLoggedErrors;
	}

	void GenerateMap ()
	{
		// Get the set of assemblies for the "first" ABI. The ACW map is
		// not ABI-specific, so we can use any ABI to generate the wrappers.
		var allAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: true);
		var singleArchAssemblies = allAssembliesPerArch.First ().Value.Values.ToList ();

		var entries = new List<ACWMapEntry> ();

		foreach (var assembly in singleArchAssemblies) {
			var wrappersPath = JavaObjectsXmlFile.GetJavaObjectsXmlFilePath (assembly.ItemSpec);

			if (!File.Exists (wrappersPath)) {
				Log.LogError ($"'{wrappersPath}' not found.");
				return;
			}

			var xml = JavaObjectsXmlFile.Import (wrappersPath, JavaObjectsXmlFileReadType.AndroidResourceFixups);

			entries.AddRange (xml.ACWMapEntries);
		}

		var acwMapGen = new ACWMapGenerator (Log);

		acwMapGen.Generate (entries, AcwMapFile);
	}
}
