using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternal : Step
	{
		public Step_PrepareExternal ()
			: base ("Preparing external components")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			var msbuild = new MSBuildRunner (context);

			string slnPath = Path.Combine (Configurables.Paths.ExternalDir, "debugger-libs", "debugger-libs.sln");
			bool result = await msbuild.Run (
				projectPath: slnPath,
				logTag: "debugger-libs-restore",
				arguments: new List <string> {
				   "/t:Restore"
			    },
				binlogName: "prepare-debugger-libs-restore"
			);

			if (!result)
				return false;

			return await ExecuteOSSpecific (context);
		}

		async Task<bool> NuGetRestore (NuGetRunner nuget, string solutionFilePath)
		{
			if (!await nuget.Restore (solutionFilePath)) {
				Log.ErrorLine ($"NuGet restore for solution {solutionFilePath} failed");
				return false;
			}

			return true;
		}
	}
}
