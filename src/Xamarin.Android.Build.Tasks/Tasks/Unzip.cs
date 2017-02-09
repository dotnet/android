using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class Unzip : Task
	{
		public ITaskItem [] Sources { get; set; }
		public ITaskItem [] DestinationDirectories { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Unzip Task");
			Log.LogDebugTaskItems ("  Sources: ", Sources);
			Log.LogDebugTaskItems ("  DestinationDirectories: ", DestinationDirectories);

			foreach (var pair in Sources.Zip (DestinationDirectories, (s, d) => new { Source = s, Destination = d })) {
				if (!Directory.Exists (pair.Destination.ItemSpec))
					Directory.CreateDirectory (pair.Destination.ItemSpec);
				using (var z = ZipArchive.Open (pair.Source.ItemSpec, FileMode.Open))
					z.ExtractAll (pair.Destination.ItemSpec);
			}

			return true;
		}
	}
}
