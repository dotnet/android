using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ComputeHash : AndroidTask
	{
		public override string TaskPrefix => "CPT";

		[Required]
		public ITaskItem [] Source { get; set; }

		public bool CopyMetaData { get; set; } = true;

		[Output]
		public ITaskItem [] Output { get; set; }

		public override bool RunTask ()
		{
			var output = new List<ITaskItem> (Source.Length);
			foreach (var item in Source) {
				var newItem = new TaskItem(item.ItemSpec, new Dictionary<string, string>() {
					{ "Hash", Files.HashString (item.ItemSpec) }
				});
				if (CopyMetaData)
					item.CopyMetadataTo (newItem);
				output.Add (newItem);
			}
			Output = output.ToArray ();
			Log.LogDebugTaskItems ("Output : ", Output);
			return !Log.HasLoggedErrors;
		}
	}
}
