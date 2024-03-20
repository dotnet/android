using System;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class RemoveUnknownFiles : AndroidTask
	{
		public override string TaskPrefix => "RUF";

		static bool IsWindows = Path.DirectorySeparatorChar == '\\';

		[Required]
		public ITaskItem[] Files { get; set; }
		
		[Required]
		public string[] Directories { get; set; }
		
		public bool RemoveDirectories { get; set; }

		public string FileType { get; set; } = "AndroidResource";

		[Output]
		public ITaskItem[] RemovedFiles { get; set; }

		[Output]
		public ITaskItem [] RemovedDirectories { get; set; }
		
		public override bool RunTask ()
		{
			HashSet<string> knownFiles;
			List<ITaskItem> removedFiles = new List<ITaskItem> ();
			List<ITaskItem> removedDirectories = new List<ITaskItem> ();
			// Do a case insensitive compare on windows, because the file
			// system is case insensitive [Bug #645833]
			if (IsWindows)
				knownFiles = new HashSet<string> (Files.Select (f => f.GetMetadata ("FullPath")), StringComparer.InvariantCultureIgnoreCase);
			else
				knownFiles = new HashSet<string> (Files.Select (f => f.GetMetadata ("FullPath")));

			var root = "res";
			if (FileType == "AndroidAsset")
				root = "assets";

			foreach (var directory in Directories) {
				var absDir = Path.GetFullPath (directory);
				if (!System.IO.Directory.Exists (absDir)) {
					Log.LogDebugMessage ("Skipping Directory {0}. It does not exists yet.", directory);
					continue;
				}
				var files = System.IO.Directory.GetFiles (absDir, "*", SearchOption.AllDirectories);
				foreach (string f in files) {
					if (!knownFiles.Contains (f)) {
						Log.LogDebugMessage ("Deleting File {0}", f);
						var item = new TaskItem (f.Replace (absDir, root + Path.DirectorySeparatorChar));
						removedFiles.Add (item);
						Microsoft.Android.Build.Tasks.Files.SetWriteable (f);
						File.Delete (f);
					}
				}
				
				if (RemoveDirectories) {
					var knownDirs = new HashSet<string> (knownFiles.Select (d => Path.GetDirectoryName (d)));
					var dirs = System.IO.Directory.GetDirectories (absDir, "*", SearchOption.AllDirectories);

					foreach (string d in dirs.OrderByDescending (s => s.Length)) {
						if (!knownDirs.Contains (d) && IsDirectoryEmpty (d)) {
							Log.LogDebugMessage ("Deleting Directory {0}", d);
							removedDirectories.Add (new TaskItem(d));
							Microsoft.Android.Build.Tasks.Files.SetDirectoryWriteable (d);
							System.IO.Directory.Delete (d);
						}
					}
				}
			}

			RemovedFiles = removedFiles.ToArray ();
			RemovedDirectories = removedDirectories.ToArray ();
			Log.LogDebugTaskItems ("[Output] RemovedFiles", RemovedFiles);
			Log.LogDebugTaskItems ("[Output] RemovedDirectories", RemovedDirectories);
			return true;
		}

		// We are having issues with trees like this:
		// - /Assets
		//   - /test
		//     - /test2
		//       - myasset.txt
		// /test is not in known directories, so we are trying to delete it,
		// even though we need it because of its subdirectories
		// [Bug #654535]
		private bool IsDirectoryEmpty (string dir)
		{
			if (System.IO.Directory.GetFiles (dir).Length != 0)
				return false;

			if (System.IO.Directory.GetDirectories (dir).Length != 0)
				return false;

			return true;
		}
	}
}
