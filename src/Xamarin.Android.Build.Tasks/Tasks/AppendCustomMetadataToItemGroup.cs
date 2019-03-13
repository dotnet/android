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
			var metaData = new Dictionary<string, List<ITaskItem>> (StringComparer.InvariantCultureIgnoreCase);
			foreach (ITaskItem item in MetaDataItems) {
				if (!metaData.ContainsKey (item.ItemSpec))
					metaData.Add (item.ItemSpec, new List<ITaskItem> ());
				metaData[item.ItemSpec].Add (item);
			}

			foreach (var item in Inputs) {
				var fn = Path.GetFileNameWithoutExtension (item.ItemSpec);
				output.Add (item);
				if (!metaData.ContainsKey (fn))
					continue;
				List<ITaskItem> metaDateList = metaData [fn];
				if (metaDateList == null)
					continue;
				foreach (var metaDateItem in metaDateList) {
					Log.LogDebugMessage ($"Copying MetaData for {item.ItemSpec}");
					metaDateItem.CopyMetadataTo (item);
				}
			}

			Output = output.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
