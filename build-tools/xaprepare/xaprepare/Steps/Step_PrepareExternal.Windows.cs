using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternal
	{
		async Task<bool> ExecuteOSSpecific (Context context)
		{
			var msbuild = new MSBuildRunner (context);
			string javaInteropSolution = Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "Java.Interop.sln");

			return await msbuild.Run (
				projectPath: javaInteropSolution,
				logTag: "java.interop-restore",
				arguments: new List <string> {
				   "/t:Restore"
			    },
				binlogName: "prepare-java.interop-restore"
			);
		}
	}
}
