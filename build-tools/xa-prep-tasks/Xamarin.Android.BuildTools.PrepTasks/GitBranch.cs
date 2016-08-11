using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using IOFile = System.IO.File;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public sealed class GitBranch : Git
	{
		[Output]
		public                  string      Branch              { get; set; }

		protected   override    bool        LogTaskMessages     {
			get { return false; }
		}

		public GitBranch ()
		{
			// `git rev-parse --abbrev-ref HEAD` currently writes `HEAD` to stderr; no idea why.
			LogStandardErrorAsError = false;
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitBranch)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			base.Execute ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Branch)}: {Branch}");

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return "rev-parse --abbrev-ref HEAD";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			Branch  = singleLine;
		}
	}
}

