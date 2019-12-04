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

			string prepTasksPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "build-tools", "xa-prep-tasks", "xa-prep-tasks.csproj");
			string bootstrapTasksPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "build-tools", "Xamarin.Android.Tools.BootstrapTasks", "Xamarin.Android.Tools.BootstrapTasks.csproj");
			string xfTestPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "tests", "Xamarin.Forms-Performance-Integration", "Xamarin.Forms.Performance.Integration.csproj");

			if (!await RestorePackageRefProjects (msbuild, prepTasksPath, "prep-tasks-restore")) {
				return false;
			}

			if (!await RestorePackageRefProjects (msbuild, bootstrapTasksPath, "bootstrap-tasks-restore")) {
				return false;
			}

			if (!await RestorePackageRefProjects (msbuild, xfTestPath, "xfperf-restore")) {
				return false;
			}

			return true;
		}

		async Task<bool> RestorePackageRefProjects (MSBuildRunner msbuild, string projectPath, string tag)
		{
			return await msbuild.Run (
				projectPath: projectPath,
				logTag: tag,
				arguments: new List<string> {
					"/t:Restore"
				},
				binlogName: tag
			);
		}
	}
}
