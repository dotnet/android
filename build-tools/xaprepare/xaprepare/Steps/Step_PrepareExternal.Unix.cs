using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternal
	{
		async Task<bool> ExecuteOSSpecific (Context context, NuGetRunner nuget)
		{
			Log.StatusLine ();
			var make = new MakeRunner (context) {
				NoParallelJobs = true
			};

			bool result = await make.Run (
				logTag: "xamarin-android-tools",
				workingDirectory: Path.Combine (Configurables.Paths.ExternalDir, "xamarin-android-tools"),
				arguments: new List <string> {
					"prepare",
					$"CONFIGURATION={context.Configuration}",
			    }
			);
			if (!result)
				return false;

			string javaInteropDir = context.Properties.GetRequiredValue (KnownProperties.JavaInteropFullPath);
			Log.StatusLine ();
			result = await make.Run (
				logTag: "java-interop-prepare",
				workingDirectory: javaInteropDir,
				arguments: new List <string> {
					"prepare",
					$"CONFIGURATION={context.Configuration}",
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
