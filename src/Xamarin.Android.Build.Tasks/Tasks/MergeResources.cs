using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tasks
{
	public class MergeResources : Task
	{
		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		[Required]
		public ITaskItem[] DestinationFiles { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public string CacheFile { get; set; }

		ResourceMerger merger = null;
			
		public override bool Execute ()
		{
			// ok copy all the files from Cache into dest path
			// then copy over the App Resources
			// emit warnings if we find duplicates.
			Log.LogDebugMessage ("MergeResources Task");
			Log.LogDebugTaskItems ("  SourceFiles: ", SourceFiles);
			Log.LogDebugTaskItems ("  DestinationFiles: ", DestinationFiles);

			List<int> changedFiles = new List<int> ();

			merger = new ResourceMerger () {
				CacheFile = CacheFile,
				Log = Log,
			};
			merger.Load ();

			for (int i = 0; i < SourceFiles.Length; i++) {
				var src = SourceFiles [i].ItemSpec;
				var destfilename = DestinationFiles [i].ItemSpec;
				if (File.GetLastWriteTimeUtc (src) > File.GetLastWriteTimeUtc (destfilename)) {
					changedFiles.Add (i);
				}
			}

			for (int idx = 0; idx < changedFiles.Count; idx++) {
				var i = changedFiles[idx];
				var src = SourceFiles [i].ItemSpec;
				var destfilename = Path.GetFullPath (DestinationFiles [i].ItemSpec);
				CopyResource (src, destfilename);
				merger.RemoveResourcesForFile (destfilename);
			}

			merger.Save ();

			return !Log.HasLoggedErrors;
		}

		void CopyResource(string src, string destPath)
		{
			var cachedDate = File.GetLastWriteTimeUtc (src);
			var path = Path.GetDirectoryName (src).Trim (new char[] { Path.DirectorySeparatorChar });
			if (File.Exists (destPath)) {
				if (merger.NeedsMerge (path)) {
					merger.MergeValues (src, destPath);
				} else {
					MonoAndroidHelper.CopyIfChanged (src, destPath);
				}
			} else {
				if (merger.NeedsMerge (path))
					merger.MergeValues (src, destPath);
				else
					MonoAndroidHelper.CopyIfChanged (src, destPath);
			}
			MonoAndroidHelper.SetWriteable (destPath);
			MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (destPath, cachedDate, Log);
		}
	}


}

