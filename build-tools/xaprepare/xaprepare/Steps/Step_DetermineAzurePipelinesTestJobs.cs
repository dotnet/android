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

			var git = new GitRunner (context);
			string commitMessage = Environment.GetEnvironmentVariable ("BUILD_SOURCEVERSIONMESSAGE");
			if (string.IsNullOrEmpty (commitMessage)) {
				var messageLines = await git.RunCommandForOutputAsync (BuildPaths.XamarinAndroidSourceRoot, "log", "-n", "1", "--pretty=%B");
				commitMessage = string.Join (Environment.NewLine, messageLines);
			}

			// Assume we're building a merge commit as part of an Azure Pipelines PR build. Otherwsise, this step will fail.
			//  Example: Merge 0b66502c8b9f33cbb8d21b2dab7c100629aec081 into 0bef8aa5cd74d83d77c4e2b3f63975a0deb804b3
			var commitMessagePieces = commitMessage.Split (new string [] { " " }, StringSplitOptions.RemoveEmptyEntries);
			if (string.IsNullOrEmpty (commitMessagePieces [1]) || string.IsNullOrEmpty (commitMessagePieces [3]))
				return false;

			var filesChanged = await git.RunCommandForOutputAsync (BuildPaths.XamarinAndroidSourceRoot, "diff", "--name-only", commitMessagePieces [1], commitMessagePieces [3]);
			if (filesChanged == null || filesChanged.Count < 1)
				return false;

			var testAreas = new List<string> ();
			foreach (string file in filesChanged) {
				// Compare files changed against common areas requiring additional test scope
				// MSBuild area will run all legacy and One .NET Xamarin.Android.Build.Task tests.
				// Mono area will run BCL and timezone unit tests.
				if (file.Contains (".external")) {
					testAreas.Add ("Mono");
					testAreas.Add ("MSBuild");
				} else if (file.Contains ("external/Java.Interop")) {
					testAreas.Add ("MSBuild");
				} else if (file.Contains ("Xamarin.Android.Build.Tasks")) {
					testAreas.Add ("MSBuild");
				}
			}

			foreach (var testArea in testAreas.Distinct ()) {
				WriteAzurePipelinesOutputVariable (testArea, "True");
			}

			return true;
		}

		void WriteAzurePipelinesOutputVariable (string name, string value)
		{
			Log.MessageLine ($"##vso[task.setvariable variable={name};isOutput=true]{value}");
		}
	}
}
