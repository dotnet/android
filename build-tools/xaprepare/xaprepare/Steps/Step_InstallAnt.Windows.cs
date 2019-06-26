using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_InstallAnt : StepWithDownloadProgress
	{
		public Step_InstallAnt ()
			: base ("Install Ant")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string localPackagePath = Configurables.Paths.AntArchivePath;

			if (await Utilities.VerifyArchive (localPackagePath)) {
				Log.StatusLine ("Ant archive already downloaded and valid");
			} else {
				Uri antUrl = new Uri (Configurables.Urls.AntBaseUri, Configurables.Paths.AntArchiveName);

				Log.StatusLine ("Ant URL: ", $"{antUrl}", tailColor: ConsoleColor.Cyan);

				HttpStatusCode status;
				bool success;
				ulong size;

				(success, size, status) = await Utilities.GetDownloadSizeWithStatus (antUrl);
				if (!success) {
					Log.ErrorLine ($"Failed to access Ant at {antUrl} (HTTP status: {status})");
					return false;
				}

				DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
				Log.StatusLine ($"  {context.Characters.Link} {antUrl}", ConsoleColor.White);
				await Download (context, antUrl, localPackagePath, "Apache Ant", Path.GetFileName (localPackagePath), downloadStatus);

				if (!File.Exists (localPackagePath)) {
					Log.ErrorLine ($"Download of Xamarin.Android Bundle from {antUrl} failed.");
					return false;
				}
			}

			Log.StatusLine ($"Unpacking Ant to {Configurables.Paths.AntInstallDir}");
			string tempDir = $"{Configurables.Paths.AntInstallDir}-ant.temp";
			try {
				if (!await Utilities.Unpack (localPackagePath, tempDir, cleanDestinatioBeforeUnpacking: true)) {
					Log.ErrorLine ("Failed to unpack Ant");
					return false;
				}

				Log.DebugLine ($"Moving unpacked Ant from {tempDir} to {Configurables.Paths.AntInstallDir}");

				// There should be just a single subdirectory
				List<string> subdirs = Directory.EnumerateDirectories (tempDir).ToList ();
				if (subdirs.Count > 1)
					throw new InvalidOperationException ($"Unexpected contents layout of the Ant archive - expected a single subdirectory, instead found {subdirs.Count}");

				Utilities.MoveDirectoryContentsRecursively (subdirs [0], Configurables.Paths.AntInstallDir);
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
			}

			return true;
		}
	}
}
