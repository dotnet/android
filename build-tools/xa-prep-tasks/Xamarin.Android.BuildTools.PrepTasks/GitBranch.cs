using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

		static readonly Regex GitHeadRegex = new Regex ("(?<=refs/heads/).+$", RegexOptions.Compiled);

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitBranch)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			string gitHeadFile = Path.Combine (WorkingDirectory.ItemSpec, ".git", "HEAD");
			if (File.Exists (gitHeadFile)) {
				string gitHeadFileContent = File.ReadAllText (gitHeadFile);
				Match match = GitHeadRegex.Match (gitHeadFileContent);
				Branch = match.Value;
			}

			if (string.IsNullOrEmpty (Branch)) {
				base.Execute ();
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Branch)}: {Branch}");

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return "name-rev --name-only --exclude=tags/* HEAD";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;

			// Strip common unecessary characters.
			singleLine = singleLine.Replace ("remotes/origin/", string.Empty);
			int index = singleLine.IndexOf ('~');
			if (index > 0)
				singleLine = singleLine.Remove (index);

			Branch  = singleLine;
		}
	}
}

