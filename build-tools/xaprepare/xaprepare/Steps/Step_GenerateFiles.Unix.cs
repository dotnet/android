using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateFiles
	{
		sealed class GeneratedConfigurationFile : GeneratedFile
		{
			public GeneratedConfigurationFile (string outputPath) :
				base(outputPath)
			{}

			public override void Generate (Context context)
			{
				if (context == null)
					throw new ArgumentNullException (nameof (context));

				using (StreamWriter sw = Utilities.OpenStreamWriter (OutputPath)) {
					sw.WriteLine ("# This file is used by both Make and shell scripts");
					sw.WriteLine ($"CONFIGURATION={context.Configuration}");
					sw.Flush ();
				}
			}
		}

		partial void AddUnixPostBuildSteps (Context context, List<GeneratedFile> steps)
		{
			steps.Add (new GeneratedConfigurationFile (Path.Combine (Configurables.Paths.BinDirRoot, "configuration.mk")));
		}
	}
}
