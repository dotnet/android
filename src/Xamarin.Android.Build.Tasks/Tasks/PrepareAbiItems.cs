using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class PrepareAbiItems : AndroidTask
	{
		public override string TaskPrefix => "PAI";

		[Required]
		public string [] BuildTargetAbis { get; set; }

		[Required]
		public string NativeSourcesDir { get; set; }

		[Required]
		public string Mode { get; set; }

		[Output]
		public ITaskItem[] AssemblySources { get; set; }

		public override bool RunTask ()
		{
			var sources = new List<ITaskItem> ();

			TaskItem item;
			NativeAssemblerItemsHelper.KnownMode mode = NativeAssemblerItemsHelper.ToKnownMode (Mode);
			foreach (string abi in BuildTargetAbis) {
				item = new TaskItem (NativeAssemblerItemsHelper.GetSourcePath (Log, mode, NativeSourcesDir, abi));
				item.SetMetadata ("abi", abi);
				item.SetMetadata ("RuntimeIdentifier", MonoAndroidHelper.AbiToRid (abi));
				sources.Add (item);
			}

			AssemblySources = sources.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
