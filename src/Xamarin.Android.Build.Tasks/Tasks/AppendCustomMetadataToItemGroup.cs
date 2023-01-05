using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	public class AppendCustomMetadataToItemGroup : AndroidTask {
		public override string TaskPrefix => "ACM";

		[Required]
		public ITaskItem[] Inputs { get; set; }

		[Required]
		public ITaskItem[] MetaDataItems { get; set; }

		[Output]
		public ITaskItem[] Output { get; set; }

		public override bool RunTask ()
		{
			var output = new List<ITaskItem> ();
			var metaData = new Dictionary<string, List<ITaskItem>> (StringComparer.InvariantCultureIgnoreCase);
			foreach (ITaskItem item in MetaDataItems) {
				List<ITaskItem> itemsList;
				if (!metaData.TryGetValue(item.ItemSpec, out itemsList)) {
					itemsList = new List<ITaskItem>();
					metaData.Add(item.ItemSpec, itemsList);
				}
				itemsList.Add(item);
			}

			foreach (var item in Inputs) {
				var fn = Path.GetFileNameWithoutExtension (item.ItemSpec);
				output.Add (item);
				var md = item.GetMetadata ("HasResourceDesignerAssemblyReference");
				if (string.IsNullOrEmpty (md)) {
					var b = MonoAndroidHelper.HasResourceDesignerAssemblyReference (item);
					item.SetMetadata ("HasResourceDesignerAssemblyReference", MonoAndroidHelper.HasResourceDesignerAssemblyReference (item).ToString ());
				}
				List<ITaskItem> metaDataList;
				if (!metaData.TryGetValue (fn, out metaDataList))
					continue;
				foreach (var metaDataItem in metaDataList) {
					Log.LogDebugMessage ($"Copying MetaData for {item.ItemSpec}");
					metaDataItem.CopyMetadataTo (item);
				}
			}

			Output = output.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
