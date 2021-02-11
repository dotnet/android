using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Processes .so files coming from @(ResolvedFileToPublish).
	/// * Checks if ABI is valid
	/// * Fixes up libmonodroid.so based on $(AndroidIncludeDebugSymbols)
	/// </summary>
	public class ProcessNativeLibraries : AndroidTask
	{
		public override string TaskPrefix => "PRNL";

		/// <summary>
		/// Assumed to be .so files only
		/// </summary>
		public ITaskItem [] InputLibraries { get; set; }

		public bool IncludeDebugSymbols { get; set; }

		[Output]
		public ITaskItem [] OutputLibraries { get; set; }

		public override bool RunTask ()
		{
			if (InputLibraries == null || InputLibraries.Length == 0)
				return true;

			var output = new List<ITaskItem> (InputLibraries.Length);

			foreach (var library in InputLibraries) {
				var abi = AndroidRidAbiHelper.GetNativeLibraryAbi (library);
				if (string.IsNullOrEmpty (abi)) {
					var packageId = library.GetMetadata ("NuGetPackageId");
					if (!string.IsNullOrEmpty (packageId)) {
						Log.LogCodedWarning ("XA4301", library.ItemSpec, 0, Properties.Resources.XA4301_ABI_NuGet, library.ItemSpec, packageId);
					} else {
						Log.LogCodedWarning ("XA4301", library.ItemSpec, 0, Properties.Resources.XA4301_ABI, library.ItemSpec);
					}
					continue;
				}
				// Both libmono-android.debug.so and libmono-android.release.so are in InputLibraries.
				// Use IncludeDebugSymbols to determine which one to include.
				// We may eventually have files such as `libmono-android-checked+asan.release.so` as well.
				var fileName = Path.GetFileNameWithoutExtension (library.ItemSpec);
				if (fileName.StartsWith ("libmono-android", StringComparison.Ordinal)) {
					if (fileName.EndsWith (".debug", StringComparison.Ordinal)) {
						if (!IncludeDebugSymbols)
							continue;
						library.SetMetadata ("ArchiveFileName", "libmonodroid.so");
					} else if (fileName.EndsWith (".release", StringComparison.Ordinal)) {
						if (IncludeDebugSymbols)
							continue;
						library.SetMetadata ("ArchiveFileName", "libmonodroid.so");
					}
				}
				output.Add (library);
			}

			OutputLibraries = output.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
