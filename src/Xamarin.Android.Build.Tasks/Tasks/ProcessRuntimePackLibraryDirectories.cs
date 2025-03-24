#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class ProcessRuntimePackLibraryDirectories : AndroidTask
{
	public override string TaskPrefix => "FRPLD";

	static readonly HashSet<string> NativeLibraryNames = new (StringComparer.OrdinalIgnoreCase) {
		"libarchive-dso-stub.so",
		"libc.so",
		"libdl.so",
		"liblog.so",
		"libm.so",
		"libz.so",
	};

	[Required]
	public ITaskItem[] ResolvedFilesToPublish { get; set; } = Array.Empty<ITaskItem> ();

	[Output]
	public ITaskItem[] RuntimePackLibraryDirectories { get; set; } = Array.Empty<ITaskItem> ();

	[Output]
	public ITaskItem[] NativeLibrariesToRemove { get; set; }  = Array.Empty<ITaskItem> ();

	public override bool RunTask ()
	{
		var libDirs = new List<ITaskItem> ();
		var librariesToRemove = new List<ITaskItem> ();
		var seenRIDs = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (ITaskItem item in ResolvedFilesToPublish) {
			if (!IsInSupportedRuntimePack (item)) {
				continue;
			}

			string? fileName = Path.GetFileName (item.ItemSpec);
			if (String.IsNullOrEmpty (fileName) || !NativeLibraryNames.Contains (fileName)) {
				continue;
			}

			string? rid = item.GetMetadata ("RuntimeIdentifier");
			if (String.IsNullOrEmpty (rid)) {
				Log.LogDebugMessage ($"Ignoring item '{item}' because it contains no runtime identifier metadata");
				continue;
			}

			if (!seenRIDs.Contains (rid)) {
				string? dirName = Path.GetDirectoryName (item.ItemSpec);
				if (String.IsNullOrEmpty (dirName)) {
					Log.LogDebugMessage ($"Item '{item}' path doesn't contain full file path");
				} else {
					libDirs.Add (MakeLibDirItem (item, dirName));
				}
				seenRIDs.Add (rid);
				Log.LogDebugMessage ($"Discovered runtime pack library directory for '{rid}': {dirName}");
			}

			librariesToRemove.Add (item);
			Log.LogDebugMessage ($"Item '{item}' will be removed from the set of native libraries to publish");
		}

		RuntimePackLibraryDirectories = libDirs.ToArray ();
		NativeLibrariesToRemove = librariesToRemove.ToArray ();

		return !Log.HasLoggedErrors;
	}

	ITaskItem MakeLibDirItem (ITaskItem sourceItem, string dir)
	{
		var ret = new TaskItem (dir);
		sourceItem.CopyMetadataTo (ret);

		// These make no sense for directories, remove just to be safe
		ret.RemoveMetadata ("CopyLocal");
		ret.RemoveMetadata ("CopyToPublishDirectory");
		ret.RemoveMetadata ("DestinationSubPath");
		ret.RemoveMetadata ("RelativePath");
		return ret;
	}

	bool IsInSupportedRuntimePack (ITaskItem item)
	{
		string? NuGetPackageId = item.GetMetadata ("NuGetPackageId");
		if (String.IsNullOrEmpty (NuGetPackageId)) {
			return false;
		}

		return NuGetPackageId.StartsWith ("Microsoft.Android.Runtime.CoreCLR.", StringComparison.OrdinalIgnoreCase) ||
		       NuGetPackageId.StartsWith ("Microsoft.Android.Runtime.Mono.", StringComparison.OrdinalIgnoreCase);
	}
}
