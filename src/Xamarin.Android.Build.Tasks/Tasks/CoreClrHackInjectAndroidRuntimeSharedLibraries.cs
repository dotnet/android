using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace  Xamarin.Android.Tasks;

//
// HACK! HACK! To be removed when CoreCLR runtime packs work properly.
//
// Injects CoreCLR runtime pack shared libraries into the `ResolvedFileToPublish`
// item group.
//
public class CoreClrHackInjectAndroidRuntimeSharedLibraries : AndroidTask
{
	public override string TaskPrefix => "CCHIARSL";

	[Required]
	public ITaskItem[] ResolvedFilesToPublish { get; set; }

	[Required]
	public string AndroidRuntime { get; set; } = String.Empty;

	[Output]
	public ITaskItem[] SharedLibrariesToAdd { get; set; } = Array.Empty<ITaskItem> ();

	public override bool RunTask ()
	{
		if (String.Compare (AndroidRuntime, "CoreCLR", StringComparison.OrdinalIgnoreCase) != 0) {
			return true;
		}

		var sharedLibsToAdd = new List<ITaskItem> ();
		foreach (ITaskItem item in ResolvedFilesToPublish) {
			string fileName = Path.GetFileName (item.ItemSpec);
			string dirName = Path.GetDirectoryName (item.ItemSpec).Replace ("Mono", "CoreCLR");
			ITaskItem newItem;
			if (String.Compare (fileName, "libmono-android.release.so", StringComparison.OrdinalIgnoreCase) == 0) {
				newItem = MakeItem (Path.Combine (dirName, "libnet-android.release.so"), item);
			} else if (String.Compare (fileName, "libmono-android.debug.so", StringComparison.OrdinalIgnoreCase) == 0) {
				newItem = MakeItem (Path.Combine (dirName, "libnet-android.debug.so"), item);
			} else {
				continue;
			}

			sharedLibsToAdd.Add (newItem);
		}

		SharedLibrariesToAdd = sharedLibsToAdd.ToArray ();
		return !Log.HasLoggedErrors;
	}

	ITaskItem MakeItem (string identity, ITaskItem templateItem)
	{
		var item = new TaskItem (identity);
		templateItem.CopyMetadataTo (item);

		string fileName = Path.GetFileName (identity);
		item.SetMetadata ("DestinationSubPath", fileName);
		item.SetMetadata ("RelativePath", fileName);

		// No need to null check the metadata value - if it isn't there, then something's broken and we
		// will let it crash.
		item.SetMetadata ("NuGetPackageId", item.GetMetadata ("NuGetPackageId").Replace ("Mono", "CoreCLR"));
		return item;
	}
}
