using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallJetBrainsOpenJDK
	{
		void MoveContents (string sourceDir, string destinationDir)
		{
			Utilities.MoveDirectoryContentsRecursively (sourceDir, destinationDir);
		}
	}
}
