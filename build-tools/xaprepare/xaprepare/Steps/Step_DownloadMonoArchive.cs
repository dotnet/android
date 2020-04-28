using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	class Step_DownloadMonoArchive : StepWithDownloadProgress
	{
		public Step_DownloadMonoArchive ()
			: base ("Downloading Mono archive")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			bool success = await DownloadMonoArchive (context);

			if (!success) {
				Log.InfoLine ("Mono archive not present");
				return true;
			} else
				Utilities.SaveAbiChoice (context);

			context.MonoArchiveDownloaded = success;
			return true;
		}

		async Task<bool> DownloadMonoArchive (Context context)
		{
			if (context.ForceRuntimesBuild) {
				Log.StatusLine ("Mono runtime rebuild forced, Mono Archive download skipped");
				return false;
			}

			Log.StatusLine ("Checking if all runtime files are present");
			var allRuntimes = new Runtimes ();
			if (MonoRuntimesHelpers.AreRuntimeItemsInstalled (context, allRuntimes)) {

				// User might have changed the set of ABIs to build, we need to check and rebuild if necessary
				if (!Utilities.AbiChoiceChanged (context)) {
					Log.StatusLine ("Mono runtimes already present and complete. No need to download or build.");
					return true;
				}

				Log.StatusLine ("Mono already present, but the choice of ABIs changed since previous build, runtime refresh is necessary");
			}
			Log.Instance.StatusLine ($"  {Context.Instance.Characters.Bullet} some files are missing, download and extraction required");

			string localPath, archiveFileName;
			if (string.IsNullOrEmpty (context.MonoArchiveCustomUrl)) {
				localPath = Configurables.Paths.MonoArchiveLocalPath;
				archiveFileName = Configurables.Paths.MonoArchiveFileName;
			} else {
				var uri = new Uri (context.MonoArchiveCustomUrl);
				archiveFileName = Path.GetFileName (uri.LocalPath);
				localPath = Path.Combine (context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory), archiveFileName);
			}

			bool result = false;
			for (uint i = 0; i < 3; i++) {
				result = await DownloadAndUpackIfNeeded (
					context,
					"Mono",
					context.MonoArchiveCustomUrl,
					localPath,
					archiveFileName,
					Configurables.Paths.MonoSDKSOutputDir
				);

				if (result)
					break;
			}

			if (!result)
				return false;

			for (uint i = 0; i < 3; i++) {
				result = await DownloadAndUpackIfNeeded (
					context,
					"Windows Mono",
					customUrl: String.Empty,
					localPath: Configurables.Paths.MonoArchiveWindowsLocalPath,
					archiveFileName: Configurables.Paths.MonoArchiveWindowsFileName,
					destinationDirectory: Configurables.Paths.BCLWindowsOutputDir
				);

				if (result)
					break;
			}

			return result;
		}

		async Task<bool> DownloadAndUpackIfNeeded (Context context, string name, string customUrl, string localPath, string archiveFileName, string destinationDirectory)
		{
			if (File.Exists (localPath)) {
				Log.StatusLine ($"{name} archive already downloaded");
			} else {
				Utilities.DeleteFileSilent (localPath);

				var url = string.IsNullOrEmpty (customUrl) ?
					new Uri (Configurables.Urls.MonoArchive_BaseUri, archiveFileName) :
					new Uri (customUrl);
				Log.StatusLine ($"Downloading {name} archive from {url}");

				(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
				if (!success) {
					if (status == HttpStatusCode.NotFound)
						Log.Info ($"{name} archive URL not found");
					else
						Log.Info ($"Failed to obtain {name} archive size. HTTP status code: {status} ({(int)status})");
					Log.InfoLine (". Mono runtimes will be rebuilt");
					return false;
				}

				DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
				Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
				await Download (context, url, localPath, $"{name} Archive", archiveFileName, downloadStatus);

				if (!File.Exists (localPath)) {
					Log.InfoLine ($"Download of {name} archive from {url} failed");
					return false;
				}
			}

			string tempDir = $"{destinationDirectory}.tmp";
			if (!await Utilities.Unpack (localPath, tempDir, cleanDestinatioBeforeUnpacking: true)) {
				Utilities.DeleteFileSilent (localPath);
				Utilities.DeleteDirectorySilent (destinationDirectory);
				Log.WarningLine ($"Failed to unpack {name} archive {localPath}");
				Utilities.DeleteFileSilent (localPath);
				return false;
			}

			Log.DebugLine ($"Moving unpacked Mono archive from {tempDir} to {destinationDirectory}");
			try {
				Utilities.MoveDirectoryContentsRecursively (tempDir, destinationDirectory, resetFileTimestamp: true);
			} finally {
				Utilities.DeleteDirectorySilent (tempDir);
				// Clean up zip after extraction if running on a hosted azure pipelines agent.
				if (context.IsRunningOnHostedAzureAgent)
					Utilities.DeleteFileSilent (localPath);
			}

			return true;
		}
	}
}
