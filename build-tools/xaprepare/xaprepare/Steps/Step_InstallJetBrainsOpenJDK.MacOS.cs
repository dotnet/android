using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallJetBrainsOpenJDK
	{
		void MoveContents (string sourceDir, string destinationDir)
		{
			string realSourceDir = Path.Combine (sourceDir, "Contents", "Home");
			Utilities.MoveDirectoryContentsRecursively (realSourceDir, destinationDir);
		}
	}
}
