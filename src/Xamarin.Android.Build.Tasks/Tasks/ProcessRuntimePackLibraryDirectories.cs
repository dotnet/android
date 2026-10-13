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
		"libc.so",
		"libdl.so",
		"liblog.so",
		"libm.so",
		"libz.so",
	};

	[Required]
	public ITaskItem[] ResolvedFilesToPublish { get; set; } = [];

	[Output]
	public ITaskItem[] NativeLibrariesToRemove { get; set; }  = [];

	public override bool RunTask ()
	{
		var librariesToRemove = new List<ITaskItem> ();

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

			librariesToRemove.Add (item);
			Log.LogDebugMessage ($"Item '{item}' will be removed from the set of native libraries to publish");
		}

		NativeLibrariesToRemove = librariesToRemove.ToArray ();

		return !Log.HasLoggedErrors;
	}

	bool IsInSupportedRuntimePack (ITaskItem item)
	{
		string? NuGetPackageId = item.GetMetadata ("NuGetPackageId");
		if (String.IsNullOrEmpty (NuGetPackageId)) {
			return false;
		}

		return NuGetPackageId.StartsWith ("Microsoft.Android.Runtime.CoreCLR.", StringComparison.OrdinalIgnoreCase) ||
		       NuGetPackageId.StartsWith ("Microsoft.Android.Runtime.Mono.", StringComparison.OrdinalIgnoreCase) ||
		       NuGetPackageId.StartsWith ("Microsoft.Android.Runtime.NativeAOT.", StringComparison.OrdinalIgnoreCase);
	}
}
