using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Extends the <Move/> task, so we can preserve directory structure of an unknown subfolder.
	/// It fills out the SourceFiles & DestinationFiles properties, clears DestinationFolder, and calls the base <Move/> task.
	/// </summary>
	public class MoveSubfolder : Move
	{
		[Required]
		public string SourceFolder { get; set; }

		/// <summary>
		/// An optional path inside the unknown subfolder
		/// </summary>
		public string AdditionalPath { get; set; }

		/// <summary>
		/// This is only needed to remove [Required]
		/// </summary>
		public new ITaskItem [] SourceFiles {
			get => base.SourceFiles;
			set => base.SourceFiles = value;
		}

		public override bool Execute ()
		{
			// If we encounter unexpected input, we can just call base.Execute();
			if (DestinationFolder == null) {
				Log.LogDebugMessage ("DestinationFolder is null.");
				return base.Execute ();
			}

			var directories = Directory.GetDirectories (SourceFolder);
			if (directories.Length == 0) {
				Log.LogDebugMessage ("No subdirectories found.");
				return !Log.HasLoggedErrors;
			}

			// Full path to an unknown subfolder
			var subfolder = Path.GetFullPath (directories.First ());
			if (!string.IsNullOrEmpty (AdditionalPath)) {
				subfolder = Path.Combine (subfolder, AdditionalPath);
			}
			subfolder = subfolder.TrimEnd (Path.DirectorySeparatorChar); // trim off trailing / if present
			Log.LogDebugMessage ($"Copying from: {subfolder}");

			var sourceFiles = new List<ITaskItem> ();
			var destinationFiles = new List<ITaskItem> ();
			foreach (var file in Directory.GetFiles (subfolder, "*", SearchOption.AllDirectories)) {
				var relativePath = file.Substring (subfolder.Length + 1);
				sourceFiles.Add (new TaskItem (file));
				destinationFiles.Add (new TaskItem (Path.Combine (DestinationFolder.ItemSpec, relativePath)));
			}
			DestinationFolder = null;
			DestinationFiles = destinationFiles.ToArray ();
			SourceFiles = sourceFiles.ToArray ();
			return base.Execute ();
		}
	}
}
