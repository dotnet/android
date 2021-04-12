using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallOpenJDK
	{
		void MoveContents (string sourceDir, string destinationDir)
		{
			string realSourceDir = Path.Combine (sourceDir, "Contents", "Home");
			Utilities.MoveDirectoryContentsRecursively (realSourceDir, destinationDir);
		}
	}
}
