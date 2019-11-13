using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class GitBlame : Git
	{
		[Required]
		public                  ITaskItem       FileName        { get; set; }

		public                  string          LineFilter      { get; set; }

		[Output]
		public                  ITaskItem[]     Commits         { get; set; }


		protected   override    bool            LogTaskMessages {
			get { return false; }
		}

		List<ITaskItem>     commits = new List<ITaskItem> ();

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitBlame)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (FileName)}: {FileName.ItemSpec}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (LineFilter)}: {LineFilter}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			base.Execute ();

			Commits     = commits.ToArray ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Commits)}:");
			foreach (var c in Commits) {
				Log.LogMessage (MessageImportance.Low, $"  [Output]   {c.GetMetadata ("CommitHash")} '{c.GetMetadata ("Line")}'");
			}

			return !Log.HasLoggedErrors;
		}

		protected override string GenerateCommandLineCommands ()
		{
			return $"blame \"{FileName.ItemSpec}\"";
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			if (string.IsNullOrEmpty (singleLine))
				return;
			if (LineFilter != null && !singleLine.Contains (LineFilter))
				return;
			var commitHash  = GetCommitHash (singleLine);
			var commit      = new TaskItem (commitHash);
			commit.SetMetadata ("CommitHash",   commitHash);
			commit.SetMetadata ("Line",         singleLine);
			commits.Add (commit);
		}

		static string GetCommitHash (string line)
		{
			int i   = line?.IndexOf (' ') ?? -1;
			return i >= 0
				? line?.Substring (0, i)
				: line;
		}
	}
}
