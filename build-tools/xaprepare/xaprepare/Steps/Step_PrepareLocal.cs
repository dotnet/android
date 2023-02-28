using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_PrepareLocal : Step
	{
		public Step_PrepareLocal ()
			: base ("Preparing local components")
		{}


		protected override async Task<bool> Execute(Context context)
		{
			var msbuild = new MSBuildRunner (context);
			string xfTestPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "tests", "Xamarin.Forms-Performance-Integration", "Xamarin.Forms.Performance.Integration.csproj");

			return await msbuild.Restore (projectPath: xfTestPath, logTag: "xfperf", binlogName: "prepare-local");
		}
	}
}
