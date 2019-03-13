using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks {
	public class AppendCustomMetadataToItemGroup : Task {
		[Required]
		public ITaskItem[] Inputs { get; set; }

		[Required]
		public ITaskItem[] MetaDataItems { get; set; }

		[Output]
		public ITaskItem[] Output { get; set; }

		public override bool Execute ()
		{
			var output = new List<ITaskItem> ();

			foreach (var item in Inputs) {
				var fn = Path.GetFileNameWithoutExtension (item.ItemSpec);
				output.Add (item);
				foreach (var metaData in MetaDataItems) {
					if (string.Compare (metaData.ItemSpec, fn, StringComparison.OrdinalIgnoreCase) != 0)
						continue;
					Log.LogDebugMessage ($"Copying MetaData for {item.ItemSpec}");
					metaData.CopyMetadataTo (item);
				}
			}

			Output = output.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
