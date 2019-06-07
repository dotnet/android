using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateFiles
	{
		partial void AddUnixPostBuildSteps (Context context, List<GeneratedFile> steps)
		{
			steps.Add (new GeneratedMakeRulesFile (Path.Combine (Configurables.Paths.BuildBinDir, "rules.mk")));
		}
	}
}
