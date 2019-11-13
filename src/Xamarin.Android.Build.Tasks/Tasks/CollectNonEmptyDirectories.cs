using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks {
	
	public class CollectNonEmptyDirectories : AndroidTask {
		public override string TaskPrefix => "CNE";

		List<ITaskItem> output = new List<ITaskItem> ();

		[Required]
		public ITaskItem[] Directories { get; set; }

		[Required]
		public string LibraryProjectIntermediatePath { get; set; }

		[Required]
		public string StampDirectory { get; set; }

		[Output]
		public ITaskItem[] Output => output.ToArray ();

		public override bool RunTask ()
		{
			var libraryProjectDir = Path.GetFullPath (LibraryProjectIntermediatePath);
			foreach (var directory in Directories) {
				if (!Directory.Exists (directory.ItemSpec)) {
					Log.LogDebugMessage ($"Directory does not exist, skipping: {directory.ItemSpec}");
					continue;
				}
				var firstFile = Directory.EnumerateFiles(directory.ItemSpec, "*.*", SearchOption.AllDirectories).FirstOrDefault ();
				if (firstFile != null) {
					var taskItem = new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", firstFile },
					});
					directory.CopyMetadataTo (taskItem);

					string stampFile = directory.GetMetadata ("StampFile");
					if (string.IsNullOrEmpty (stampFile)) {
						if (Path.GetFullPath (directory.ItemSpec).StartsWith (libraryProjectDir)) {
							// If inside the `lp` directory
							stampFile = Path.GetFullPath (Path.Combine (directory.ItemSpec, "..", "..")) + ".stamp";
						} else {
							// Otherwise use a hashed stamp file
							stampFile = Path.Combine (StampDirectory, Files.HashString (directory.ItemSpec) + ".stamp");
						}
						taskItem.SetMetadata ("StampFile", stampFile);
					} else {
						Log.LogDebugMessage ($"%(StampFile) already set: {stampFile}");
					}
					output.Add (taskItem);
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
