﻿// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Security.Cryptography;

namespace Xamarin.Android.Tasks
{
	// When we regenerate the Resource.designer.cs, if we write it
	// when it hasn't actually changed, the user will get a "Reload?"
	// prompt in IDEs, so we only want to copy the file if there is
	// an actual change.
	public class CopyIfChanged : Task
	{
		[Required]
		public ITaskItem[] SourceFiles { get; set; }

		[Required]
		public ITaskItem[] DestinationFiles { get; set; }

		[Output]
		public ITaskItem[] ModifiedFiles { get; set; }

		private List<ITaskItem> modifiedFiles = new List<ITaskItem>();

		public override bool Execute ()
		{
			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			for (int i = 0; i < SourceFiles.Length; i++) {
				var src = new FileInfo (SourceFiles [i].ItemSpec);
				if (!src.Exists) {
					Log.LogDebugMessage ($"  Skipping {src} it does not exist");
					continue;
				}
				var dest = new FileInfo (DestinationFiles [i].ItemSpec);
				if (dest.Exists && dest.LastWriteTimeUtc > src.LastWriteTimeUtc && dest.Length == src.Length) {
					Log.LogDebugMessage ($"  Skipping {src} it is up to date");
					continue;
				}
				if (!MonoAndroidHelper.CopyIfChanged (src.FullName, dest.FullName)) {
					Log.LogDebugMessage ($"  Skipping {src} it was not changed.");
					MonoAndroidHelper.SetWriteable (dest.FullName);
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
