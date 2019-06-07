using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareBundle : StepWithDownloadProgress
	{
		static Uri BundleUriPrefix   => Configurables.Urls.Bundle_XABundleDownloadPrefix;
		static string BundleFileName => Configurables.Paths.XABundleFileName;
		static bool osSupportsMonoBuild;

		public Step_PrepareBundle ()
			: base ("Preparing the binary bundle")
		{
			InitOS ();
		}

		protected override async Task<bool> Execute (Context context)
		{
			if (context.ForceRuntimesBuild) {
				if (osSupportsMonoBuild) {
					Log.InfoLine ("Rebuilding Mono runtimes as requested");
					return false;
				}

				Log.InfoLine ($"Forced Mono runtimes rebuild requested but rebuilding on {context.OS.Type} is currently not supported.");
			}

			string localPackagePath = Path.Combine (Configurables.Paths.BundleArchivePath);
			if (await Utilities.VerifyArchive (localPackagePath)) {
				Log.StatusLine ("Xamarin.Android Bundle archive already downloaded and valid");
			} else {
				if (!String.IsNullOrEmpty (context.XABundlePath)) {
					// User indicated they wanted to use a specific bundle that's supposed to be on disk. It's not (or
					// it's invalid) and that means we have no way of getting it - we can't download the default one
					// since that was not the intention behind overriding the location. Thus, we error out.
					throw new InvalidOperationException ($"Xamarin.Android bundle indicated on the command line does not exist ({context.XABundlePath})");
				}

				var bundleUrl = new Uri (BundleUriPrefix, BundleFileName);

				Log.StatusLine ("Bundle URL: ", $"{bundleUrl}", tailColor: ConsoleColor.Cyan);

				HttpStatusCode status;
				bool success;
				ulong size;

				(success, size, status) = await Utilities.GetDownloadSizeWithStatus (bundleUrl);
				if (!success) {
					if (status == HttpStatusCode.NotFound) {
						if (osSupportsMonoBuild)
							Log.StatusLine ("   not found, will need to rebuild");
						else
							Log.ErrorLine ($"   not found, rebuilding on {context.OS.Type} is not currently supported");
						return false;
					}

					if (String.IsNullOrEmpty (bundle404Message))
						throw new InvalidOperationException ($"Failed to access bundle at {bundleUrl} (HTTP status: {status})");
					else
						throw new InvalidOperationException (bundle404Message);
				}

				DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
				Log.StatusLine ($"  {context.Characters.Link} {bundleUrl}", ConsoleColor.White);
				await Download (context, bundleUrl, localPackagePath, "Xamarin.Android Bundle", Path.GetFileName (localPackagePath), downloadStatus);

				if (!File.Exists (localPackagePath)) {
					Log.ErrorLine ($"Download of Xamarin.Android Bundle from {bundleUrl} failed.");
					return false;
				}
			}

			Log.StatusLine ($"Unpacking bundle to {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, Configurables.Paths.BundleInstallDir)}");
			string tempDir = $"{Configurables.Paths.BundleInstallDir}-bundle.temp";
			try {
				if (!await Utilities.Unpack (localPackagePath, tempDir, cleanDestinatioBeforeUnpacking: true)) {
					Log.WarningLine ("Failed to unpack bundle, will need to rebuild");
					return false;
				}

				Log.DebugLine ("Moving unpacked bundle from {tempDir} to {Configurables.Paths.Bundle_InstallDir}");
				Utilities.MoveDirectoryContentsRecursively (tempDir, Configurables.Paths.BundleInstallDir, resetFileTimestamp: true);
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
			}

			return true;
		}
	}
}
