using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternal
	{
		async Task<bool> ExecuteOSSpecific (Context context, NuGetRunner nuget)
		{
			var msbuild = new MSBuildRunner (context);
			string javaInteropSolution = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "Java.Interop.sln");
			bool result = await msbuild.Run (
				projectPath: javaInteropSolution,
				logTag: "java.interop-restore",
				arguments: new List <string> {
				   "/t:Restore"
			    },
				binlogName: "prepare-java.interop-restore"
			);

			if (!result)
				return false;

			result = await NuGetRestore (nuget, javaInteropSolution);
			if (!result)
				return false;

			return await NuGetRestore (nuget, Configurables.Paths.ExternalXamarinAndroidToolsSln);
		}
	}
}
