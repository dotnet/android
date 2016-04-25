using System;
using System.Linq;
using Microsoft.Build.Utilities;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class CollectLibraryAssets : Task
	{
		public string AssetDirectory { get; set; }
		public string [] AdditionalAssetDirectories { get; set; }

		public override bool Execute ()
		{
			if (AdditionalAssetDirectories != null)
				foreach (var dir in AdditionalAssetDirectories)
					foreach (var file in Directory.GetFiles (dir, "*", SearchOption.AllDirectories))
						MonoAndroidHelper.CopyIfChanged (file, file.Replace (dir, AssetDirectory));
			return true;
		}
	}
}

