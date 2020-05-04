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

		async Task<bool> Restore (MSBuildRunner msbuild, string csprojPath, string logTag, string binLogName, string additionalArgument = null)
		{
			var args = new List<string> { "/t:Restore" };
			if (additionalArgument != null)
				args.Add (additionalArgument);

			return await msbuild.Run (
				projectPath: csprojPath,
				logTag: logTag,
				arguments: args,
				binlogName: binLogName
			);
		}

		protected override async Task<bool> Execute(Context context)
		{
			var msbuild = new MSBuildRunner (context);

			string xfTestPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "tests", "Xamarin.Forms-Performance-Integration", "Xamarin.Forms.Performance.Integration.csproj");
			if (!await Restore (msbuild, xfTestPath, "xfperf", "prepare-restore"))
				return false;

			if (!await Restore (msbuild, xfTestPath, "xfperf", "prepare-restore", "/p:BundleAssemblies=true"))
				return false;

			var apkDiffPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "tools", "apkdiff", "apkdiff.csproj");
			return await Restore (msbuild, apkDiffPath, "apkdiff", "prepare-restore-apkdiff");
		}
	}
}
