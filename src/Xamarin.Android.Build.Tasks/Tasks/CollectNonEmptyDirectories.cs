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
					output.Add (new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", firstFile}
					}));
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
