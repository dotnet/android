using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class PrepareSatelliteAssemblies : AndroidTask
{
	public override string TaskPrefix => "PSA";

	[Required]
	public string[] BuildTargetAbis                    { get; set; } = Array.Empty<string> ();

	[Required]
	public ITaskItem[] ReferenceSatellitePaths         { get; set; } = Array.Empty<ITaskItem> ();

	[Required]
	public ITaskItem[] IntermediateSatelliteAssemblies { get; set; } = Array.Empty<ITaskItem> ();

	[Output]
	public ITaskItem[] ProcessedSatelliteAssemblies { get; set; }

	public override bool RunTask ()
	{
		var output = new List<ITaskItem> ();

		SetMetadata (ReferenceSatellitePaths, output);
		SetMetadata (IntermediateSatelliteAssemblies, output);

		ProcessedSatelliteAssemblies = output.ToArray ();
		return !Log.HasLoggedErrors;
	}

	void SetMetadata (ITaskItem[] items, List<ITaskItem> output)
	{
		foreach (ITaskItem item in items) {
			SetMetadata (item, output);
		}
	}

	void SetMetadata (ITaskItem item, List<ITaskItem> output)
	{
		string? culture = item.GetMetadata ("Culture");
		if (String.IsNullOrEmpty (culture)) {
			throw new InvalidOperationException ($"Assembly item '{item}' is missing the 'Culture' metadata");
		}

		string assemblyName = Path.GetFileName (item.ItemSpec);
		foreach (string abi in BuildTargetAbis) {
			var newItem = new TaskItem (item);
			newItem.SetMetadata ("Abi", abi);

			SetDestinationPathsMetadata (newItem, MonoAndroidHelper.MakeZipArchivePath (abi, culture, assemblyName));
			output.Add (newItem);
		}

		void SetDestinationPathsMetadata (ITaskItem item, string zipArchivePath)
		{
			item.SetMetadata ("DestinationSubPath", zipArchivePath);
			item.SetMetadata ("DestinationSubDirectory", Path.GetDirectoryName (zipArchivePath));
		}
	}
}
