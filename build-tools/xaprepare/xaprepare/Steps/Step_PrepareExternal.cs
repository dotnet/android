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
			bool result = await msbuild.Restore (
				projectPath: slnPath,
				logTag: "debugger-libs-restore",
				binlogName: "prepare-debugger-libs-restore"
			);

			if (!result)
				return false;

			return await msbuild.Restore (
				projectPath: Configurables.Paths.ExternalXamarinAndroidToolsSln,
				logTag: "xat-restore",
				arguments: new List<string> () { "--configfile", Path.Combine (Configurables.Paths.ExternalDir, "xamarin-android-tools", "NuGet.config") },
				binlogName: "prepare-xat-restore"
			);
		}

	}
}
