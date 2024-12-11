#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects TypeMap to be added to the final archive.
/// </summary>
public class CollectTypeMapFilesForArchive : AndroidTask
{
	public override string TaskPrefix => "CTM";

	public string AndroidPackageFormat { get; set; } = "";

	public ITaskItem [] TypeMappings { get; set; } = [];

	[Output]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		if (TypeMappings.Length == 0)
			return true;

		var rootPath = AndroidPackageFormat.Equals ("aab", StringComparison.InvariantCultureIgnoreCase) ? "root/" : "";
		var files = new List<ITaskItem> ();

		foreach (var tm in TypeMappings) {
			var item = new TaskItem (tm.ItemSpec);
			item.SetMetadata ("ArchivePath", rootPath + Path.GetFileName (tm.ItemSpec));

			files.Add (item);
		}

		FilesToAddToArchive = files.ToArray ();

		return !Log.HasLoggedErrors;
	}
}
