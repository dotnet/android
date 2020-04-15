using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternal
	{
		async Task<bool> ExecuteOSSpecific (Context context)
		{
			Log.StatusLine ();
			var make = new MakeRunner (context) {
				NoParallelJobs = true
			};

			return await make.Run (
				logTag: "xamarin-android-tools",
				workingDirectory: Path.Combine (Configurables.Paths.ExternalDir, "xamarin-android-tools"),
				arguments: new List <string> {
					"prepare",
					$"CONFIGURATION={context.Configuration}",
			    }
			);
		}
	}
}
