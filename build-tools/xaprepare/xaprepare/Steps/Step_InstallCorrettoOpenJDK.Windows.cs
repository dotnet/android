using System;
using System.Threading;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallCorrettoOpenJDK
	{
		static readonly TimeSpan BeforeMoveSleepTime = TimeSpan.FromSeconds (30);

		string GetArchiveRootDirectoryName ()
		{
			Version v = Configurables.Defaults.CorrettoVersion;

			return $"jdk1.8.0_{v.Minor}";
		}
	}
}
