using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks {
	
	public class CollectNonEmptyDirectories : Task {
		List<ITaskItem> output = new List<ITaskItem> ();

		[Required]
		public ITaskItem[] Directories { get; set; }

		[Output]
		public ITaskItem[] Output => output.ToArray ();

		public override bool Execute ()
		{
			foreach (var directory in Directories) {
				var firstFile = Directory.EnumerateFiles(directory.ItemSpec, "*.*", SearchOption.AllDirectories).FirstOrDefault ();
				if (firstFile != null) {
					var taskItem = new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", firstFile },
						{"StampFile", Path.GetFullPath (Path.Combine (directory.ItemSpec, "..", "..")) + ".stamp" },
					});
					directory.CopyMetadataTo (taskItem);
					output.Add (taskItem);
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
