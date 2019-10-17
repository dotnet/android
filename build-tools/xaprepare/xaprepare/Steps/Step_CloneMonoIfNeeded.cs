using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_CloneMonoIfNeeded : Step
	{
		public Step_CloneMonoIfNeeded ()
			: base ("Cloning mono sources if needed")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			if (await IsMonoArchiveUriValid () && !context.ForceRuntimesBuild) {
				Log.InfoLine ("Skipping Mono source clone. A rebuild was not requested, and a valid archive exists.");
				return true;
			}

			Log.InfoLine ("Configuring Mono sources.");
			var git = new GitRunner (context);
			string monoRepoUri = "https://github.com/mono/mono.git";
			string destinationDir = Configurables.Paths.MonoSourceFullPath;

			// If our local Mono path does not have a `.git` directory, delete and re-clone.
			if (Directory.Exists(destinationDir) && !Directory.Exists (Path.Combine (destinationDir, ".git"))) {
				Utilities.DeleteDirectory (destinationDir, recurse: true);
			}

			if (!Directory.Exists (destinationDir)) {
				Log.StatusLine ($"    {context.Characters.Link} cloning from {monoRepoUri}");
				if (!await git.Clone (monoRepoUri, destinationDir)) {
					Log.ErrorLine ($"Failed to clone Mono");
					return false;
				}
			}

			Log.StatusLine ($"    {context.Characters.Link} fetching changes from mono/mono");
			if (!await git.Fetch (destinationDir)) {
				Log.ErrorLine ($"Failed to fetch changes for Mono");
				return false;
			}

			Log.StatusLine ($"    {context.Characters.Bullet} checking out commit {context.BuildInfo.MonoHash}");
			if (!await git.CheckoutCommit (destinationDir, context.BuildInfo.MonoHash)) {
				Log.ErrorLine ($"Failed to checkout commit {context.BuildInfo.MonoHash} for Mono");
				return false;
			}

			if (Utilities.FileExists (Path.Combine (destinationDir, ".gitmodules"))) {
				Log.StatusLine ($"    {context.Characters.Bullet} updating submodules");
				if (!await git.SubmoduleUpdate (destinationDir)) {
					Log.ErrorLine ($"Failed to update submodules for Mono");
					return false;
				}
			}

			return true;

		}

		async Task<bool> IsMonoArchiveUriValid ()
		{
			var url = new Uri (Configurables.Urls.MonoArchive_BaseUri, Configurables.Paths.MonoArchiveFileName);
			Log.StatusLine ($"Checking Mono archive at {url}");

			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				Log.InfoLine ($"Failed locate Mono archive or obtain archive size." +
					$" HTTP status code: {status} ({(int) status})", ConsoleColor.Red);
			}
			return success;
		}
	}
}
