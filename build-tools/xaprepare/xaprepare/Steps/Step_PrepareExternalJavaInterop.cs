using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternalJavaInterop : Step
	{
		public Step_PrepareExternalJavaInterop ()
			: base ("Preparing external/Java.Interop")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string javaInteropDir = context.Properties.GetRequiredValue (KnownProperties.JavaInteropFullPath);
			var dotnetPath = context.Properties.GetRequiredValue (KnownProperties.DotNetPreviewPath);
			var dotnetTool = Path.Combine (dotnetPath, "dotnet");
			var msbuild = new MSBuildRunner (context);

			return await msbuild.Run (
				projectPath: Path.Combine (javaInteropDir, "Java.Interop.sln"),
				logTag: "java-interop-prepare",
				arguments: new List<string> {
				   "-t:Prepare",
				   $"-p:MaximumJdkVersion={Configurables.Defaults.MaxJDKVersion}",
				   $"-p:MaxJdkVersion={Configurables.Defaults.MaxJDKVersion}",
				   $"-p:JdksRoot={context.OS.JavaHome}",
				   $"-p:DotnetToolPath={dotnetTool}"
				},
				binlogName: "java-interop-prepare",
				workingDirectory: javaInteropDir
			);
		}
	}
}
