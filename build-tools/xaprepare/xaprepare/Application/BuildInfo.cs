using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class BuildInfo : AppObject
	{
		public string CommitOfLastVersionChange { get; private set; } = String.Empty;

		public string VersionHash            { get; private set; } = String.Empty;

		async Task DetermineLastVersionChangeCommit (Context context)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} Commit of last version change", ConsoleColor.Gray);
			GitRunner git = CreateGitRunner (context);
			IList <GitRunner.BlamePorcelainEntry>? blameEntries;

			blameEntries = await git.Blame ("Configuration.props");
			if (blameEntries == null || blameEntries.Count == 0)
				throw new InvalidOperationException ("Unable to determine the last version change commit");

			foreach (GitRunner.BlamePorcelainEntry be in blameEntries) {
				if (be == null || String.IsNullOrEmpty (be.Line))
					continue;

				if (be.Line.IndexOf ("<ProductVersion>", StringComparison.Ordinal) >= 0) {
					Log.DebugLine ($"Last version change on {GetCommitDate (be)} by {be.Author}");
					CommitOfLastVersionChange = be.Commit;
					Log.StatusLine ("    Commit: ", be.Commit, tailColor: ConsoleColor.Cyan);
					break;
				}
			}
		}

		string GetCommitDate (GitRunner.BlamePorcelainEntry be)
		{
			int tzOffset = GetTZOffset (be.CommitterTZ, be);
			int committerTimeUTC = (int)be.CommitterTime + tzOffset;
			var unixEpoch = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime dt = unixEpoch.AddSeconds (committerTimeUTC);

			return dt.ToString ("R", DateTimeFormatInfo.InvariantInfo);
		}

		int GetTZOffset (string tz, GitRunner.BlamePorcelainEntry be)
		{
			if (String.IsNullOrEmpty (tz)) {
				Log.DebugLine ($"No timezone information from `git blame` for commit {be.Commit}");
				return 0;
			}

			if (tz.Length != 5) {
				LogUnexpectedFormat ();
				return 0;
			}

			int tzSign;
			switch (tz [0]) {
				case '-':
					tzSign = 1;
					break;

				case '+':
					tzSign = -1;
					break;

				default:
					LogUnexpectedFormat ();
					return 0;
			}

			if (!Int32.TryParse (tz.Substring (1, 2), out int hours)) {
				LogUnexpectedFormat ();
				return 0;
			}

			if (!Int32.TryParse (tz.Substring (3, 2), out int minutes)) {
				LogUnexpectedFormat ();
				return 0;
			}

			return tzSign * ((hours * 3600) + (minutes * 60));

			void LogUnexpectedFormat ()
			{
				Log.DebugLine ($"Unexpected timezone format from `git blame` for commit {be.Commit}: {tz}");
			}
		}

		GitRunner CreateGitRunner (Context context)
		{
			return new GitRunner (context, Log) {
				LogMessageIndent = "    Running: "
			};
		}
	}
}
