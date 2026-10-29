using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public class GenerateResourceDesignerAssembly : MultithreadedAndroidTask
{
	public override string TaskPrefix => "GRDA";

	[Required]
	public ITaskItem RTxtFile { get; set; } = new TaskItem ();

	public ITaskItem? ResourceMap { get; set; }

	[Required]
	public bool IsApplication { get; set; }

	[Required]
	public bool DesignTimeBuild { get; set; }

	[Required]
	public ITaskItem OutputFile { get; set; } = new TaskItem ();

	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	[Required]
	public string TargetFrameworkIdentifier { get; set; } = "";

	[Required]
	public string ProjectDir { get; set; } = "";

	[Required]
	public ITaskItem [] Resources { get; set; } = [];

	[Required]
	public string ResourceDirectory { get; set; } = "";

	public string? CaseMapFile { get; set; }
	public ITaskItem []? AdditionalResourceDirectories { get; set; }
	public ITaskItem []? FrameworkDirectories { get; set; }
	public bool Deterministic { get; set; }
	public string? AssemblyName { get; set; }

	public override bool RunTask ()
	{
		var rtxtFile = TaskEnvironment.GetAbsolutePath (RTxtFile.ItemSpec);
		var outputFile = TaskEnvironment.GetAbsolutePath (OutputFile.ItemSpec);
		var mapFile = TaskEnvironment.GetAbsolutePath (CaseMapFile ?? throw new ArgumentNullException (nameof (CaseMapFile)));
		var map = (BuildEngine as IBuildEngine4)?.GetRegisteredTaskObjectAssemblyLocal<Dictionary<string, string>> (Path.GetFullPath (mapFile.Value), RegisteredTaskObjectLifetime.Build)
			?? MonoAndroidHelper.LoadMapFile (File.Exists (mapFile) ? File.ReadLines (mapFile) : [], StringComparer.OrdinalIgnoreCase);

		var targetFramework = new FrameworkName ($"{TargetFrameworkIdentifier},Version={TargetFrameworkVersion}");
		var resources = new RtxtParser ().Parse (File.Exists (rtxtFile) ? File.ReadLines (rtxtFile) : [], rtxtFile, Log, map);
		var emitter = new ResourceDesignerAssemblyEmitter ();
		var image = emitter.Emit (Path.GetFileNameWithoutExtension (outputFile), targetFramework, resources,
			IsApplication, AssemblyName, Deterministic);
		var stream = MemoryStreamPool.Shared.Rent ();
		try {
			image.WriteContentTo (stream);
			stream.Position = 0;
			if (Files.CopyIfStreamChanged (stream, outputFile, TaskEnvironment))
				Log.LogDebugMessage ($"Updated '{OutputFile.ItemSpec}'.");
			else
				Log.LogDebugMessage ($"'{OutputFile.ItemSpec}' was up to date.");
		} finally {
			MemoryStreamPool.Shared.Return (stream);
		}
		return !Log.HasLoggedErrors;
	}
}
