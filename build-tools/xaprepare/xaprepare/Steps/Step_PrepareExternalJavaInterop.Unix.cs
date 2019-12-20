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
			Log.StatusLine ();
			var make = new MakeRunner (context) {
				NoParallelJobs = true
			};
			var result = await make.Run (
				logTag: "java-interop-prepare",
				workingDirectory: javaInteropDir,
				arguments: new List <string> {
					"prepare",
					$"CONFIGURATION={context.Configuration}",
					$"JAVA_HOME={context.OS.JavaHome}",
					$"JI_MAX_JDK={Configurables.Defaults.MaxJDKVersion}",
				}
			);
			if (!result)
				return false;

			Log.StatusLine ();
			result = await make.Run (
				logTag: "java-interop-props",
				workingDirectory: javaInteropDir,
				arguments: new List <string> {
					$"bin/Build{context.Configuration}/JdkInfo.props",
					$"CONFIGURATION={context.Configuration}",
					$"JI_MAX_JDK={Configurables.Defaults.MaxJDKVersion}",
				}
			);
			return result;
		}
	}
}
