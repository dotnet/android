using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class PrepareAbiItems : Task
	{
		[Required]
		public string [] BuildTargetAbis { get; set; }

		[Required]
		public string ItemNamePattern { get; set; }

		[Output]
		public ITaskItem[] OutputItems { get; set; }

		public override bool Execute ()
		{
			var items = new List<ITaskItem> ();
			foreach (string abi in BuildTargetAbis) {
				var item = new TaskItem (ItemNamePattern.Replace ("@abi@", abi));
				item.SetMetadata ("abi", abi);
				items.Add (item);
			}

			OutputItems = items.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
