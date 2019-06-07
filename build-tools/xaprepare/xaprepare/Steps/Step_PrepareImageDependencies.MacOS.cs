using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareImageDependencies
	{
		partial void GatherMacPackages (List<string> brewTaps, List<string> brewPackages, List<string> pkgUrls)
		{
			foreach (Program p in Context.Instance.OS.Dependencies) {
				var homebrewProgram = p as HomebrewProgram;
				if (homebrewProgram == null)
					continue;

				if (!String.IsNullOrEmpty (homebrewProgram.HomebrewTapName))
					brewTaps.Add (homebrewProgram.HomebrewTapName);

				if (homebrewProgram.HomebrewFormulaUrl != null)
					brewPackages.Add (homebrewProgram.HomebrewFormulaUrl.ToString ());
				else
					brewPackages.Add (homebrewProgram.Name);
			}

			pkgUrls.Add (Configurables.Urls.MonoPackage.ToString ());
		}
	}
}
