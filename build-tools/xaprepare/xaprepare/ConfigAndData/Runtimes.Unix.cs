using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Runtimes
	{
		partial void PopulateDesignerBclFiles (List<BclFile> designerHostBclFilesToInstall, List<BclFile> designerWindowsBclFilesToInstall)
		{
			designerHostBclFilesToInstall.AddRange (BclToDesigner (BclFileTarget.DesignerHost));
			designerWindowsBclFilesToInstall.AddRange (BclToDesigner (BclFileTarget.DesignerWindows));
		}
	}
}
