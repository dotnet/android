using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class GeneratedMakeRulesFile
	{
		partial void OutputOSVariables (Context context, StreamWriter sw)
		{
			sw.WriteLine ($"export MACOSX_DEPLOYMENT_TARGET := {Configurables.Defaults.MacOSDeploymentTarget}");
			sw.WriteLine ($"HOMEBREW_PREFIX := {context.OS.HomebrewPrefix}");
		}
	}
}
