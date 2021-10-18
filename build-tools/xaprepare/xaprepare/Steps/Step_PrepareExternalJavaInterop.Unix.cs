using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternalJavaInterop
	{
		async Task<bool> ExecuteOSSpecific (Context context)
		{
			string javaInteropDir = context.Properties.GetRequiredValue (KnownProperties.JavaInteropFullPath);
			var dotnetPath = context.Properties.GetRequiredValue (KnownProperties.DotNetPreviewPath);
			var dotnetTool = Path.Combine (dotnetPath, "dotnet");

			Log.StatusLine ();
			var make = new MakeRunner (context) {
				NoParallelJobs = true
			};
			return await make.Run (
				logTag: "java-interop-prepare-core",
				workingDirectory: javaInteropDir,
				arguments: new List <string> {
					"prepare-core",
					$"CONFIGURATION={context.Configuration}",
					$"JI_JAVA_HOME={context.OS.JavaHome}",
					$"JAVA_HOME={context.OS.JavaHome}",
					$"JI_MAX_JDK={Configurables.Defaults.MaxJDKVersion}",
					$"DOTNET_TOOL_PATH={dotnetTool}"
				}
			);
		}
	}
}
