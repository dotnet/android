using System;
using System.Linq;
using Microsoft.Build.Utilities;
using System.IO;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CollectLibraryAssets : AndroidTask
	{
		public override string TaskPrefix => "CLA";

		public string? AssetDirectory { get; set; }
		public string []? AdditionalAssetDirectories { get; set; }

		public override bool RunTask ()
		{
			if (AdditionalAssetDirectories != null)
				foreach (var dir in AdditionalAssetDirectories)
					foreach (var file in Directory.GetFiles (dir, "*", SearchOption.AllDirectories))
						Files.CopyIfChanged (file, file.Replace (dir, AssetDirectory));
			return true;
		}
	}
}

