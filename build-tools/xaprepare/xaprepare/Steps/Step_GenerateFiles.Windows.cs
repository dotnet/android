using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Step_GenerateFiles
	{
		partial void AddOSSpecificSteps (Context context, List<GeneratedFile> steps)
		{
			string? javaSdkDirectory = context.Properties.GetValue ("JavaSdkDirectory");
			if (String.IsNullOrEmpty (javaSdkDirectory))
				javaSdkDirectory = context.OS.JavaHome;

			string jdkJvmPath = Path.Combine (javaSdkDirectory, "jre", "bin", "server", "jvm.dll");
			string jdkIncludePathShared = Path.Combine (javaSdkDirectory, "include");
			string jdkIncludePathOS = Path.Combine (jdkIncludePathShared, "win32");

			var replacements = new Dictionary<string, string> (StringComparer.Ordinal) {
				{ "@JdkJvmPath@",           jdkJvmPath },
				{ "@JdkIncludePathShared@", jdkIncludePathShared },
				{ "@JdkIncludePathOS@",     jdkIncludePathOS },
				{ "@javac@",                context.OS.JavaCPath },
				{ "@java@",                 context.OS.JavaPath },
				{ "@jar@",                  context.OS.JarPath },
				{ "@javahome@",             context.OS.JavaHome },
				{ "@dotnet@",               Configurables.Paths.DotNetPreviewTool },
			};

			var step = new GeneratedPlaceholdersFile (
				replacements,
				Path.Combine (Configurables.Paths.BootstrapResourcesDir, "JdkInfo.Windows.props.in"),
				Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "bin", $"Build{context.Configuration}", "JdkInfo.props")
			);

			steps.Add (step);
		}
	}
}
