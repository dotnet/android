using System;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallCorrettoOpenJDK
	{
		string GetArchiveRootDirectoryName ()
		{
			Version v = Configurables.Defaults.CorrettoVersion;

			return $"amazon-corretto-{v.Major}.{v.Minor}.{v.Build:00}.{v.Revision}-linux-x64";
		}
	}
}
