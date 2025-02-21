#nullable enable
using System.Collections.Concurrent;
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

	public override bool RunTask ()
	{
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

		if (Log.HasLoggedErrors) {
			// Ensure that on a rebuild, we don't *skip* the `_GenerateJavaStubs` target,
			// by ensuring that the target outputs have been deleted.
			Files.DeleteFile (AcwMapFile, Log);
		}

		return !Log.HasLoggedErrors;
	}
}
