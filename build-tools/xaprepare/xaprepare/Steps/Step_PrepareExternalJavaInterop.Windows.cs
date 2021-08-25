using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternalJavaInterop
	{
#pragma warning disable CS1998
		async Task<bool> ExecuteOSSpecific (Context context)
		{
			string javaInteropDir = context.Properties.GetRequiredValue (KnownProperties.JavaInteropFullPath);
			string projectPath = Path.Combine (javaInteropDir, "build-tools", "Java.Interop.BootstrapTasks", "Java.Interop.BootstrapTasks.csproj");
			var msbuild = new MSBuildRunner (context);
			bool result = await msbuild.Run (
				projectPath: projectPath,
				logTag: "java-interop-prepare",
				binlogName: "build-java-interop-prepare"
			);

			if (!result) {
				Log.ErrorLine ("Failed to build java-interop-prepare");
				return false;
			}
			return true;
		}
#pragma warning restore CS1998
	}
}
