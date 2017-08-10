using System;

using Microsoft.Build.Framework;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public sealed class GitCommitTime : Git
	{
		[Output]
		public string Time { get; set; }

		protected override bool LogTaskMessages {
			get { return false; }
		}

		public GitCommitTime ()
		{
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitCommitTime)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			base.Execute ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Time)}: {Time}");

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			//NOTE: this command needs to return a string that is valid to pass to DateTime.Parse()
			//	The <Touch> MSBuild task requires this: https://docs.microsoft.com/en-us/visualstudio/msbuild/touch-task
			return "log -1 --format=%cd --date=format-local:\"%Y/%m/%d %H:%M:%S\"";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			Time = singleLine;
		}
	}
}
