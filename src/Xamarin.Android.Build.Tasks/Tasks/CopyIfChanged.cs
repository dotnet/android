// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
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

		public bool KeepDestinationDates { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CopyIfChanged Task");
			Log.LogDebugTaskItems ("  SourceFiles: {0}", SourceFiles);
			Log.LogDebugTaskItems ("  DestinationFiles: {0}", DestinationFiles);

			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			for (int i = 0; i < SourceFiles.Length; i++) {
				var src = SourceFiles [i].ItemSpec;
				if (!File.Exists (src))
					continue;
				var dest = DestinationFiles [i].ItemSpec;
				var lastWriteTime = File.GetLastWriteTimeUtc (File.Exists (dest) ? dest : src);
				MonoAndroidHelper.SetWriteable (dest);
				if (!MonoAndroidHelper.CopyIfChanged (src, dest))
					continue;
				if (KeepDestinationDates)
					MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (dest, lastWriteTime, Log);
			}
			return true;
		}
	}
}
