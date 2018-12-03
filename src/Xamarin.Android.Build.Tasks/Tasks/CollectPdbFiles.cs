using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CollectPdbFiles : Task
	{
		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Output]
		public ITaskItem[] PdbFiles { get; set; }

		[Output]
		public ITaskItem[] PortablePdbFiles { get; set; }

		public override bool Execute ()
		{
			var pdbFiles = new List<ITaskItem> ();
			var portablePdbFiles = new List<ITaskItem> ();

			foreach (var file in ResolvedAssemblies) {
				var pdbFile = file.ItemSpec;

				if (!File.Exists (pdbFile))
					continue;

				if (Files.IsPortablePdb (pdbFile)) {
					portablePdbFiles.Add (file);
				} else {
					pdbFiles.Add (file);
				}
			}

			PdbFiles = pdbFiles.ToArray ();
			PortablePdbFiles = portablePdbFiles.ToArray ();

			Log.LogDebugTaskItems ("  [Output] PdbFiles:", PdbFiles);
			Log.LogDebugTaskItems ("  [Output] PortablePdbFiles:", PortablePdbFiles);

			return !Log.HasLoggedErrors;
		}
	}
}
