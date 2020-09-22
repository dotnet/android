using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_DetermineAzurePipelinesTestJobs : Step
	{
		public Step_DetermineAzurePipelinesTestJobs ()
			: base ("Determine Azure Pipelines test jobs to run from a merge commit")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			Log.StatusLine ("Determining test jobs to run...");

			string commitRevision = Environment.GetEnvironmentVariable ("BUILD_SOURCEVERSION");
			string commitMessage = Environment.GetEnvironmentVariable ("BUILD_SOURCEVERSIONMESSAGE");
			if (string.IsNullOrEmpty (commitRevision) || string.IsNullOrEmpty (commitMessage)) {
				Log.ErrorLine ("One or more source version variable values were empty:");
				Log.ErrorLine ($"BUILD_SOURCEVERSION='{commitRevision}' BUILD_SOURCEVERSIONMESSAGE='{commitMessage}'.");
				return false;
			}

			// Assume we're building a merge commit as part of an Azure Pipelines PR build. Otherwise, this step will fail.
			//  Example: Merge 0b66502c8b9f33cbb8d21b2dab7c100629aec081 into 0bef8aa5cd74d83d77c4e2b3f63975a0deb804b3
			var commitMessagePieces = commitMessage.Split (new string [] { " " }, StringSplitOptions.RemoveEmptyEntries);
			if (string.IsNullOrEmpty (commitMessagePieces [3])) {
				Log.ErrorLine ($"Unable to parse merge commit message from: '{commitMessage}'.");
				return false;
			}

			var git = new GitRunner (context);
			var filesChanged = await git.RunCommandForOutputAsync (BuildPaths.XamarinAndroidSourceRoot, "diff", "--name-only", commitRevision, commitMessagePieces [3]);
			if (filesChanged == null || filesChanged.Count < 1) {
				Log.ErrorLine ($"Unable to determine if any files were changed in this PR.");
				return false;
			}

			var testAreas = new List<string> ();
			foreach (string file in filesChanged) {
				// Compare files changed against common areas requiring additional test scope.
				// MSBuild: Runs all legacy and One .NET Xamarin.Android.Build.Task tests, as well as designer tests.
				// Mono: Runs all BCL and timezone unit tests, as well as designer tests.
				if (file == ".external") {
					testAreas.Add ("Mono");
					testAreas.Add ("MSBuild");
				} else if (file.Contains ("external/Java.Interop")) {
					testAreas.Add ("MSBuild");
				} else if (file.Contains ("Xamarin.Android.Build.Tasks")) {
					testAreas.Add ("MSBuild");
				}
			}

			foreach (var testArea in testAreas.Distinct ()) {
				Log.MessageLine ($"##vso[task.setvariable variable={testArea};isOutput=true]True");
			}

			return true;
		}

	}
}
