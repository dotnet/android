using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Runtimes
	{
		partial void AddUnixBundleItems (List<BundleItem> bundleItems)
		{
			bundleItems.AddRange (UnixBundleItems);
		}

		partial void PopulateDesignerBclFiles (List<BclFile> designerHostBclFilesToInstall, List<BclFile> designerWindowsBclFilesToInstall)
		{
			designerHostBclFilesToInstall.AddRange (BclToDesigner (BclFileTarget.DesignerHost));
			designerWindowsBclFilesToInstall.AddRange (BclToDesigner (BclFileTarget.DesignerWindows));

			List<BclFile> BclToDesigner (BclFileTarget ignoreForTarget)
			{
				return BclFilesToInstall.Where (bf => ShouldInclude (bf, ignoreForTarget)).Select (bf => new BclFile (bf.Name, bf.Type, excludeDebugSymbols: true, version: bf.Version, target: ignoreForTarget)).ToList ();
			}

			bool ShouldInclude (BclFile bf, BclFileTarget ignoreForTarget)
			{
				if (DesignerIgnoreFiles == null || !DesignerIgnoreFiles.TryGetValue (bf.Name, out (BclFileType Type, BclFileTarget Target) bft)) {
					return true;
				}

				if (bf.Type != bft.Type || bft.Target != ignoreForTarget)
					return true;

				Log.Instance.DebugLine ($"BCL file {bf.Name} will NOT be included in the installed Designer BCL files ({ignoreForTarget})");
				return false;
			}
		}
	}
}
