using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_GenerateMonoAndroidProfileItems : Step_BaseGenerateFiles
	{
		protected override List<GeneratedFile>? GetFilesToGenerate (Context context) => new List<GeneratedFile> {
			new GeneratedMonoAndroidProjitemsFile(),
		};
	}
}
