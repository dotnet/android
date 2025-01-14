using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Xamarin.Android.Tasks;

public class RecreateResolvedRuntimePacks : AndroidTask
{
	public override string TaskPrefix => "RRRP";

	[Required]
	public ITaskItem[] ResolvedNativeLibraries { get; set; }

	[Output]
	public ITaskItem[] ResolvedRuntimePacks { get; set; }

	public override bool RunTask ()
	{
		// We need to find `libc.so` that comes from one of our runtime packs
		var libcPath = String.Format ("{0}native{0}libc.so", Path.DirectorySeparatorChar);
		var runtimePacks = new Dictionary <string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
		foreach (ITaskItem library in ResolvedNativeLibraries) {
			if (!library.ItemSpec.EndsWith (libcPath, StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			if (!GetMetadata (library, "RuntimeIdentifier", out string? rid) || runtimePacks.ContainsKey (rid)) {
				continue;
			}

			if (!GetMetadata (library, "NuGetPackageId", out string? nugetPackageId)) {
				continue;
			}

			if (!GetMetadata (library, "NuGetPackageVersion", out string? nugetPackageVersion)) {
				continue;
			}

			string tail = String.Format ("{0}runtimes{0}{1}{2}", Path.DirectorySeparatorChar, rid, libcPath);
			int tailIndex = library.ItemSpec.IndexOf (tail);
			if (tailIndex < 0) {
				continue;
			}
			string packageDir = library.ItemSpec.Substring (0, tailIndex);

			var pack = new TaskItem (nugetPackageId);
			pack.SetMetadata ("FrameworkName", "Microsoft.Android");
			pack.SetMetadata ("NuGetPackageId", nugetPackageId);
			pack.SetMetadata ("NuGetPackageVersion", nugetPackageVersion);
			pack.SetMetadata ("RuntimeIdentifier", rid);
			pack.SetMetadata ("PackageDirectory", packageDir);

			runtimePacks.Add (rid, pack);
		}
		ResolvedRuntimePacks = runtimePacks.Values.ToArray ();

		return !Log.HasLoggedErrors;

		bool GetMetadata (ITaskItem item, string metadataName, out string? metadataValue)
		{
			metadataValue = item.GetMetadata (metadataName);
			return !String.IsNullOrEmpty (metadataValue);
		}
	}
}
