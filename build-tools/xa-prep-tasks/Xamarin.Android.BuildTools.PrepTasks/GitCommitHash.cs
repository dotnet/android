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
		[Output]
		public                  string      AbbreviatedCommitHash       { get; set; }

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
			return "log --no-color --first-parent -n1 --pretty=format:%h";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			AbbreviatedCommitHash   = singleLine;
		}
	}
}

