using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class GitCommitsInRange : Git
	{
		[Output]
		public                  int         CommitCount     { get; set; }

		public                  string      StartCommit     { get; set; }

		public                  string      EndCommit       { get; set; }

		protected   override    bool        LogTaskMessages {
			get { return false; }
		}

		public GitCommitsInRange ()
		{
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitCommitsInRange)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (StartCommit)}: {StartCommit}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (EndCommit)}: {EndCommit}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			// Only run if non-empty
			if (!string.IsNullOrEmpty (StartCommit)) {
				base.Execute ();
			} else {
				CommitCount = 0;
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CommitCount)}: {CommitCount}");

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			string endCommit    = string.IsNullOrEmpty (EndCommit)
				? "HEAD"
				: EndCommit;
			return $"log {StartCommit}..{endCommit} --oneline";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			CommitCount++;
		}
	}
}
