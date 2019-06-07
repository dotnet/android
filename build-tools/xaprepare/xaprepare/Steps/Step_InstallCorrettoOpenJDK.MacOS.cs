using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallCorrettoOpenJDK
	{
		string GetArchiveRootDirectoryName ()
		{
			return Path.Combine ("amazon-corretto-8.jdk", "Contents", "Home");
		}
	}
}
