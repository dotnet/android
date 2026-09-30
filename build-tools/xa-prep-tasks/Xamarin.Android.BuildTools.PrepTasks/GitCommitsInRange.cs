using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class GitCommitsInRange : Git
	{
		static readonly object MissingCommit = new object ();

		[Output]
		public                  int         CommitCount     { get; set; }

		[Required]
		public                  string      StartCommit     { get; set; }

		public                  string      EndCommit       { get; set; }

		public                  bool        AllowMissingStartCommit { get; set; }

		protected   override    bool        LogTaskMessages {
			get { return false; }
		}

		public GitCommitsInRange ()
		{
		}

		public override bool Execute ()
		{
			var cacheKey = (typeof (GitCommitsInRange), WorkingDirectory.ItemSpec, StartCommit, GetEndCommit (), ToolPath, ToolExe);
			if (AllowMissingStartCommit && ReferenceEquals (BuildEngine4.GetRegisteredTaskObject (cacheKey, RegisteredTaskObjectLifetime.Build), MissingCommit)) {
				Log.LogMessage (MessageImportance.Normal, $"Using cached git exit code 128. Setting {nameof (CommitCount)} to 0.");
				CommitCount = 0;
				return true;
			}

			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitCommitsInRange)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (StartCommit)}: {StartCommit}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (EndCommit)}: {EndCommit}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			base.Execute ();

			// fatal: bad revision '^cfa4209..HEAD'
			if (ExitCode == 128) {
				if (AllowMissingStartCommit)
					BuildEngine4.RegisterTaskObject (cacheKey, MissingCommit, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
				Log.LogMessage (MessageImportance.Normal, $"git exited with code 128. Setting {nameof (CommitCount)} to 0.");
				CommitCount = 0;
				return true;
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CommitCount)}: {CommitCount}");

			return !Log.HasLoggedErrors;
		}

		protected override bool HandleTaskExecutionErrors ()
		{
			if (AllowMissingStartCommit && ExitCode == 128)
				return true;
			return base.HandleTaskExecutionErrors ();
		}

		protected override string GenerateCommandLineCommands ()
		{
			return $"log {StartCommit}..{GetEndCommit ()} --oneline";
		}

		string GetEndCommit () => string.IsNullOrEmpty (EndCommit) ? "HEAD" : EndCommit;

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			CommitCount++;
		}
	}
}
