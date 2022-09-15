using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract class Step_BaseGenerateFiles : Step
	{
		public Step_BaseGenerateFiles () : base ("Generating files required by the build")
		{
		}

		public Step_BaseGenerateFiles (string description) : base (description)
		{
		}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			List<GeneratedFile>? filesToGenerate = GetFilesToGenerate (context);
			if (filesToGenerate != null && filesToGenerate.Count > 0) {
				foreach (GeneratedFile gf in filesToGenerate) {
					if (gf == null)
						continue;

					Log.Status ("Generating ");
					Log.Status (Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, gf.OutputPath), ConsoleColor.White);
					if (!String.IsNullOrEmpty (gf.InputPath))
						Log.StatusLine ($" {context.Characters.LeftArrow} ", Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, gf.InputPath), leadColor: ConsoleColor.Cyan, tailColor: ConsoleColor.White);
					else
						Log.StatusLine ();

					gf.Generate (context);
				}
			}

			return true;
		}
#pragma warning restore CS1998

		protected abstract List<GeneratedFile>? GetFilesToGenerate (Context context);
	}
}
