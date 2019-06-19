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

			// This needs to be built *after* we copy Java.Interop props or we'll get the wrong Mono.Cecil assembly.
			string remapAsmRefPath = Path.Combine (Configurables.Paths.BuildToolsDir, "remap-assembly-ref", "remap-assembly-ref.sln");
			bool result = await msbuild.Run (
				projectPath: remapAsmRefPath,
				logTag: "remap-assembly-ref",
				binlogName: "build-remap-assembly-ref"
			);

			if (!result) {
				Log.ErrorLine ("Failed to build remap-assembly-ref");
				return false;
			}

			string xfTestPath = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "tests", "Xamarin.Forms-Performance-Integration", "Xamarin.Forms.Performance.Integration.csproj");
			return await msbuild.Run (
				projectPath: xfTestPath,
				logTag: "xfperf",
				arguments: new List <string> {
					"/t:Restore"
				},
				binlogName: "prepare-restore"
			);
		}
	}
}
