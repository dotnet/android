using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class CreateFilePaths : Task
	{
		[Required]
		public string[]	   SourceFileNames		{ get; set; }

		[Required]
		public string[]	   SourceDirectories		{ get; set; }

		[Required]
		public string[]	   DestinationDirectories	{ get; set; }

		[Output]
		public ITaskItem[] FullSourceFilePaths		{ get; set; }

		[Output]
		public ITaskItem[] FullDestinationFilePaths	{ get; set; }

		public override bool Execute ()
		{
			if (SourceFileNames.Length != SourceDirectories.Length || SourceFileNames.Length != DestinationDirectories.Length)
				Log.LogError ("Input paramters must be arrays of the same size");
			else
				DoExecute ();

			return !Log.HasLoggedErrors;
		}

		void DoExecute ()
		{
			var sourcePaths = new List<ITaskItem> ();
			var destinationPaths = new List<ITaskItem> ();

			for (int i = 0; i < SourceFileNames.Length; i++) {
				string sourceFile = SourceFileNames [i].Trim ();
				string sourceDir = SourceDirectories [i].Trim ();
				string destDir = DestinationDirectories [i].Trim ();
				bool canContinue = true;

				canContinue &= AssertNotEmpty (sourceFile, nameof (SourceFileNames), i);
				canContinue &= AssertNotEmpty (sourceDir, nameof (SourceDirectories), i);
				canContinue &= AssertNotEmpty (destDir, nameof (DestinationDirectories), i);

				if (!canContinue)
					continue;

				string[] parts = sourceFile.Split (':');
				if (parts.Length > 2) {
					Log.LogError ($"Too many colons in {sourceFile} (SourceFileNames[{i}]), the format is 'file[:dest/file]'");
					continue;
				}

				sourcePaths.Add (new TaskItem (Path.Combine (sourceDir, parts [0])));
				destinationPaths.Add (new TaskItem (Path.Combine (destDir, parts.Length == 1 ? parts [0] : parts [1])));
			}

			FullSourceFilePaths = sourcePaths.ToArray ();
			FullDestinationFilePaths = destinationPaths.ToArray ();

			bool AssertNotEmpty (string s, string name, int i)
			{
				if (String.IsNullOrEmpty (s)) {
					Log.LogError ($"Element {i} of input array {name} must not be an empty/whitespace-only string");
					return false;
				}

				return true;
			}
		}
	}
}
