using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_PrepareExternalGitDependencies : Step
	{
		public Step_PrepareExternalGitDependencies ()
			: base ("Preparing external GIT dependencies")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			List<ExternalGitDependency> externalDependencies = ExternalGitDependency.GetDependencies (context, Configurables.Paths.ExternalGitDepsFilePath);

			bool failed = false;
			Log.StatusLine ();
			Log.StatusLine ("Updating external repositories");
			var git = new GitRunner (context) {
				EchoCmdAndArguments = false
			};
			foreach (ExternalGitDependency egd in externalDependencies) {
				Log.StatusLine ($"  {context.Characters.Bullet} {egd.Name}");

				string destDir = Path.Combine (Configurables.Paths.ExternalGitDepsDestDir, egd.Name);
				if (!Directory.Exists (destDir)) {
					var egdUrl = await GetGitHubURL (egd, git);
					if (egd.Owner == "DevDiv") {
						egdUrl = GetDevDivUrl (egd);
						Log.WarningLine ($"You may be prompted to enter DevDiv credentials to clone {egd.Name}. Please navigate to {edgUrl} and click the 'Generate Git Credentials' button to get credentials.");
					}
					Log.StatusLine ($"    {context.Characters.Link} cloning from {egd.Owner}/{egd.Name}");
					if (!await git.Clone (egdUrl, destDir)) {
						Log.ErrorLine ($"Failed to clone {egd.Name}");
						failed = true;
						continue;
					}
				}

				Log.StatusLine ($"    {context.Characters.Link} fetching changes from {egd.Owner}/{egd.Name}");
				if (!await git.Fetch (destDir)) {
					Log.ErrorLine ($"Failed to fetch changes for {egd.Name}");
					failed = true;
					continue;
				}

				Log.StatusLine ($"    {context.Characters.Bullet} checking out commit {egd.Commit}");
				if (!await git.CheckoutCommit (destDir, egd.Commit)) {
					Log.ErrorLine ($"Failed to checkout commit {egd.Commit} for {egd.Name}");
					failed = true;
					continue;
				}

				string gitModules = Path.Combine (destDir, ".gitmodules");
				if (!Utilities.FileExists (gitModules))
					continue;

				Log.StatusLine ($"    {context.Characters.Bullet} updating submodules");
				if (!await git.SubmoduleUpdate (destDir)) {
					Log.ErrorLine ($"Failed to update submodules for {egd.Name}");
					failed = true;
				}
			}

			return !failed;
		}

		async Task<string> GetGitHubURL (ExternalGitDependency egd, GitRunner git)
		{
			string? ghToken = Environment.GetEnvironmentVariable("GH_AUTH_SECRET");
			if (!String.IsNullOrEmpty (ghToken)) {
				return  $"https://{ghToken}@github.com:/{egd.Owner}/{egd.Name}";
			} else {
				if (await git.IsRepoUrlHttps (BuildPaths.XamarinAndroidSourceRoot)) {
					return $"https://github.com:/{egd.Owner}/{egd.Name}";
				} else {
					return $"git@github.com:/{egd.Owner}/{egd.Name}";
				}
			}
		}

		string GetDevDivUrl (ExternalGitDependency egd)
		{
			return $"https://devdiv.visualstudio.com/{egd.Owner}/_git/{egd.Name}";
		}
	}
}
