using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CheckForRemovedItems : Task 
	{
		[Required]
		public ITaskItem[] Files { get; set; }

		[Required]
		public string Directory { get; set; }

		[Output]
		public ITaskItem RemovedFilesFlag { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("RemoveUnknownFiles Task");
			Log.LogDebugTaskItems ("Files", Files);
			Log.LogDebugMessage ("  Directory:", Directory);

			var absDir = Path.GetFullPath (Directory);

			HashSet<string> knownFiles;

			knownFiles = new HashSet<string> (Files.Select (f => f.GetMetadata ("FullPath")), StringComparer.InvariantCultureIgnoreCase);

			RemovedFilesFlag = null;
			var files = System.IO.Directory.GetFiles (absDir, "*", SearchOption.AllDirectories);
			foreach (string f in files)
				if (!knownFiles.Contains (f)) {
					RemovedFilesFlag = new TaskItem (Path.Combine (absDir, "removedfiles.flag"));
				}

			return !Log.HasLoggedErrors;
		}
	}
}

