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
		public                  string      Branch              { get; set; } = string.Empty;

		protected   override    bool        LogTaskMessages     {
			get { return false; }
		}

		static readonly Regex GitHeadRegex = new Regex ("(?<=refs/heads/).+$", RegexOptions.Compiled);

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitBranch)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (WorkingDirectory)}: {WorkingDirectory.ItemSpec}");

			var build_sourcebranchname = Environment.GetEnvironmentVariable ("BUILD_SOURCEBRANCH");
			if (!string.IsNullOrEmpty (build_sourcebranchname) && build_sourcebranchname.IndexOf ("merge", StringComparison.OrdinalIgnoreCase) == -1) {
				Branch = build_sourcebranchname.Replace ("refs/heads/", string.Empty);
				Log.LogMessage ($"Using BUILD_SOURCEBRANCH value: {Branch}");
				return true;
			}

			string gitHeadFile = Path.Combine (WorkingDirectory.ItemSpec, ".git", "HEAD");
			if (File.Exists (gitHeadFile)) {
				Log.LogMessage ($"Using {gitHeadFile}");
				string gitHeadFileContent = File.ReadAllText (gitHeadFile);
				Match match = GitHeadRegex.Match (gitHeadFileContent);
				Branch = match.Value;
			}

			if (string.IsNullOrEmpty (Branch)) {
				Log.LogMessage ("Using git command");
				base.Execute ();
			}

			// Trim generated dependabot branch names that are too long to produce useful package names
			const int maxBranchLength = 60;
			var lastSlashIndex = Branch.LastIndexOf ('/');
			if (Branch.StartsWith ("dependabot") && lastSlashIndex != -1 && Branch.Length > maxBranchLength) {
				Branch = Branch.Substring (lastSlashIndex + 1);
			}

			// Trim darc/Maestro branch names that are too long
			if (Branch.StartsWith ("darc-") && Branch.Length > maxBranchLength) {
				Branch = Branch.Substring (0, maxBranchLength);
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

