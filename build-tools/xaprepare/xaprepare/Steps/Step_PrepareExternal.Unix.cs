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

			return result;
		}
	}
}
