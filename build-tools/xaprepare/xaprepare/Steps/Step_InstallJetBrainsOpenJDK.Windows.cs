using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallOpenJDK
	{
		async Task<bool> Unpack (string fullArchivePath, string destinationDirectory, bool cleanDestinationBeforeUnpacking = false)
		{
			// https://bintray.com/jetbrains/intellij-jdk/download_file?file_path=jbrsdk-8u202-windows-x64-b1483.37.tar.gz
			// doesn't contain a single root directory!  This causes the
			// "JetBrains root directory not found after unpacking" check to fail on Windows.
			// "Fix" things by setting destinationDirectory to contain RootDirName, allowing
			// the check to succeed.
			if (JdkVersion == Configurables.Defaults.JetBrainsOpenJDK8Version) {
				destinationDirectory = Path.Combine (destinationDirectory, RootDirName);
			}

			if (cleanDestinationBeforeUnpacking)
				Utilities.DeleteDirectorySilent (destinationDirectory);
			Utilities.CreateDirectory (destinationDirectory);

			var sevenZip = new SevenZipRunner (Context.Instance);
			Log.DebugLine ($"Uncompressing {fullArchivePath} to {destinationDirectory}");
			if (!await sevenZip.Extract (fullArchivePath, destinationDirectory)) {
				Log.DebugLine ($"Failed to decompress {fullArchivePath}");
				return false;
			}

			if (fullArchivePath.EndsWith ("tar.gz", StringComparision.OrdinalIgnoreCase)) {
				// On Windows we don't have Tar available and the Windows package is a .tar.gz
				// 7zip can unpack tar.gz but it's a two-stage process - first it decompresses the package, then it can be
				// invoked again to extract the actual tar contents.
				string tarPath = Path.Combine (destinationDirectory, Path.GetFileNameWithoutExtension (fullArchivePath));
				bool ret = await sevenZip.Extract (tarPath, destinationDirectory);
				Utilities.DeleteFileSilent (tarPath);

				if (!ret) {
					Log.DebugLine ($"Failed to extract TAR contents from {tarPath}");
					return false;
				}
			}

			return true;
		}

		void MoveContents (string sourceDir, string destinationDir)
		{
			Utilities.MoveDirectoryContentsRecursively (sourceDir, destinationDir);
		}
	}
}
