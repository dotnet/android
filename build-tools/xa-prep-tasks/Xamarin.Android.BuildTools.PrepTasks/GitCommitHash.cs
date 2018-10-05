using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using IOFile = System.IO.File;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public sealed class GitCommitHash : Git
	{
		public                  int         RequiredHashLength          { get; set; } = 7;

		[Output]
		public                  string      AbbreviatedCommitHash       { get; set; }

		[Output]
		public                  string      CommitHash       { get; set; }

		protected   override    bool        LogTaskMessages             {
			get { return false; }
		}

		public GitCommitHash ()
		{
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitCommitHash)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			base.Execute ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (AbbreviatedCommitHash)}: {AbbreviatedCommitHash}");

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return "rev-parse HEAD";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			if (singleLine.Length < 40) {
				Log.LogError ("Commit hash `{0}` is shorter than required length of {1} characters", singleLine, 40);
				return;
			}
			CommitHash = singleLine;
			AbbreviatedCommitHash = singleLine.Substring (0, RequiredHashLength);
		}
	}
}

