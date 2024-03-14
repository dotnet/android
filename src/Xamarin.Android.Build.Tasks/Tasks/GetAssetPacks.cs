using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using Irony;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tasks
{
	// We have a list of files, we want to get the
	// ones that actually exist on disk.
	public class GetAssetPacks : AndroidTask
	{
		readonly Regex validAssetPackName = new Regex ("^[A-Z0-9_]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		public override string TaskPrefix => "GAP";

		[Required]
		public ITaskItem[] Assets { get; set; }

		[Required]
		public ITaskItem IntermediateDir { get; set; }

		public string[] MetadataToCopy { get; set; } = { "DeliveryType" };

		[Output]
		public ITaskItem[] AssetPacks { get; set; }

		public override bool RunTask ()
		{
			Dictionary<string, ITaskItem> assetPacks = new Dictionary<string, ITaskItem> ();
			Dictionary<string, List<string>> files = new Dictionary<string, List<string>> ();
			foreach (var asset in Assets) {
				var assetPack = asset.GetMetadata ("AssetPack");
				if (string.IsNullOrEmpty (assetPack) || string.Compare (assetPack, "base", StringComparison.OrdinalIgnoreCase) == 0)
					continue;
				if (!IsAssetPackNameValid (assetPack)) {
					Log.LogCodedError ("XA0140", $"The AssetPack value defined for {asset.ItemSpec} is invalid. '{assetPack}' should match the following Regex '[A-Za-z0-9_]'.");
					continue;
				}
				if (!assetPacks.TryGetValue (assetPack, out ITaskItem item)) {
					item = new TaskItem (assetPack);
					item.SetMetadata ("AssetPack", assetPack);
					item.SetMetadata ("AssetPackCacheFile", Path.Combine (IntermediateDir.ItemSpec, assetPack, "assetpack.cache"));
					assetPacks[assetPack] = item;
				}
				foreach (var metadata in MetadataToCopy) {
					if (string.IsNullOrEmpty (item.GetMetadata (metadata)))
						item.SetMetadata (metadata, asset.GetMetadata (metadata));
				}
				if (!files.ContainsKey (assetPack)) {
					files[assetPack] = new List<string> ();
				}
				files[assetPack].Add (asset.ItemSpec);
			}

			foreach (var kvp in assetPacks) {
				// write out the file cache list
				// write out the metadata as well.
				ITaskItem item = kvp.Value;
				var cacheFile = kvp.Value.GetMetadata ("AssetPackCacheFile");
				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) { 
					foreach (var file in files [kvp.Key]) {
						sw.WriteLine ($"{file}:{File.GetLastWriteTimeUtc (file)}");
					}
					var deliveryType = item.GetMetadata ("DeliveryType") ?? "InstallTime";
					if (!IsDeliveryTypeValid (item, deliveryType)) {
						Log.LogCodedError ("XA1039", Properties.Resources.XA1039, item.ItemSpec, deliveryType);
					}
					sw.WriteLine (deliveryType);
					sw.Flush (); 
					Files.CopyIfStreamChanged (sw.BaseStream, cacheFile); 
				}
			}

			AssetPacks = assetPacks.Values.ToArray();

			return !Log.HasLoggedErrors;
		}

		bool IsDeliveryTypeValid (ITaskItem item, string deliveryType)
		{
			if (string.Compare (deliveryType, "installtime", StringComparison.OrdinalIgnoreCase) == 0 &&
				string.Compare (deliveryType, "ondemand", StringComparison.OrdinalIgnoreCase) == 0 &&
				string.Compare (deliveryType, "fastfollow", StringComparison.OrdinalIgnoreCase) == 0) {
					return false;
				}
			return true;
		}

		bool IsAssetPackNameValid (string assetPackName)
		{
			return validAssetPackName.IsMatch (assetPackName);
		}
	}
}