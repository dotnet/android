// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Security.Cryptography;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	// When we regenerate the Resource.designer.cs, if we write it
	// when it hasn't actually changed, the user will get a "Reload?"
	// prompt in IDEs, so we only want to copy the file if there is
	// an actual change.
	public class CopyIfChanged : AndroidTask
	{
		public override string TaskPrefix => "CIC";

		[Required]
		public ITaskItem[] SourceFiles { get; set; } = [];

		[Required]
		public ITaskItem[] DestinationFiles { get; set; } = [];

		public bool CompareFileLengths { get; set; } = true;

		[Output]
		public ITaskItem[]? ModifiedFiles { get; set; }

		private List<ITaskItem> modifiedFiles = new List<ITaskItem>();

		public CopyIfChanged ()
		{
		}

		public override bool RunTask ()
		{
			if (SourceFiles.Length != DestinationFiles.Length) {
				Log.LogWarning ($"SourceFiles ({SourceFiles.Length}) and DestinationFiles ({DestinationFiles.Length}) count mismatch. Truncating to min length.");
				if (SourceFiles.Length == 0 || DestinationFiles.Length == 0) {
					return !Log.HasLoggedErrors;
				}
			}

			int count = Math.Min (SourceFiles.Length, DestinationFiles.Length);
			for (int i = 0; i < count; i++) {
				var src = new FileInfo (SourceFiles [i].ItemSpec);
				if (!src.Exists) {
					Log.LogDebugMessage ($"  Skipping {src} it does not exist");
					continue;
				}
				var dest = new FileInfo (DestinationFiles [i].ItemSpec);
				if (dest.Exists && dest.LastWriteTimeUtc > src.LastWriteTimeUtc && (CompareFileLengths ? dest.Length == src.Length : true)) {
					Log.LogDebugMessage ($"  Skipping {src} it is up to date");
					continue;
				}
				if (!Files.CopyIfChanged (src.FullName, dest.FullName)) {
					Log.LogDebugMessage ($"  Skipping {src} it was not changed.");
					Files.SetWriteable (dest.FullName);
					continue;
				}
				modifiedFiles.Add (new TaskItem (dest.FullName));
			}

			ModifiedFiles = modifiedFiles.ToArray ();

			Log.LogDebugTaskItems (" ModifiedFiles:", ModifiedFiles);

			return true;
		}
	}
}
