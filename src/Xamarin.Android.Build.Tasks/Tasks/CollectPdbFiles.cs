using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CollectPdbFiles : AndroidTask
	{
		public override string TaskPrefix => "CPF";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; } = [];

		[Output]
		public ITaskItem[]? PdbFiles { get; set; }

		[Output]
		public ITaskItem[]? PortablePdbFiles { get; set; }

		public override bool RunTask ()
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
