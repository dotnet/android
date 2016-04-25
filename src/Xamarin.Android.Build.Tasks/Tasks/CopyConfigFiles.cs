using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class CopyConfigFiles : Task
	{
		
		public ITaskItem[] SourceFiles { get; set; }
		
		public ITaskItem[] DestinationFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugTaskItems ("SourceFiles:", SourceFiles);
			Log.LogDebugTaskItems ("DestinationFiles:", DestinationFiles);

			if (SourceFiles.Length != DestinationFiles.Length)
				throw new ArgumentException ("source and destination count mismatch");

			for (int i = 0; i < SourceFiles.Length; i++) {
				var src = SourceFiles [i].ItemSpec;
				var dst = DestinationFiles [i].ItemSpec;
				var date = DateTime.Now;
				if (File.Exists (src)) {
					MonoAndroidHelper.CopyIfChanged (src, dst);
					MonoAndroidHelper.SetWriteable (dst);
					MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (dst, date, Log);
				}
			}
			return true;
		}
	}
}

