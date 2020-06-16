using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallJetBrainsOpenJDK
	{
		async Task<bool> Unpack (string fullArchivePath, string destinationDirectory, bool cleanDestinationBeforeUnpacking = false)
		{
			return await Utilities.Unpack (fullArchivePath, destinationDirectory, cleanDestinatioBeforeUnpacking: true);
		}

		void MoveContents (string sourceDir, string destinationDir)
		{
			string realSourceDir = Path.Combine (sourceDir, "Contents", "Home");
			Utilities.MoveDirectoryContentsRecursively (realSourceDir, destinationDir);
		}
	}
}
