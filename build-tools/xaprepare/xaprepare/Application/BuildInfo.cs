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

		// NDK version info is now derived directly from BuildAndroidPlatforms.AndroidNdkPkgRevision
		// (single source of truth shared with src/androidsdk/androidsdk.targets via Configuration.props).
		public string NDKRevision => BuildAndroidPlatforms.AndroidNdkPkgRevision;
		public string NDKVersionMajor => NDKVersion.Major.ToString ();
		public string NDKVersionMinor => NDKVersion.Minor.ToString ();
		public string NDKVersionMicro => NDKVersion.Build.ToString ();

		Version? cachedNdkVersion;
		Version NDKVersion {
			get {
				if (cachedNdkVersion != null)
					return cachedNdkVersion;
				if (!Utilities.ParseAndroidPkgRevision (BuildAndroidPlatforms.AndroidNdkPkgRevision, out Version? ver, out _) || ver == null)
					throw new InvalidOperationException ($"Unable to parse NDK revision '{BuildAndroidPlatforms.AndroidNdkPkgRevision}' as a valid version string");
				cachedNdkVersion = ver;
				return ver;
			}
		}

		public string VersionHash            { get; private set; } = String.Empty;
		public string XACommitHash           { get; private set; } = String.Empty;
		public string XABranch               { get; private set; } = String.Empty;

		public async Task GatherGitInfo (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			Log.StatusLine ();
			Log.StatusLine ("Determining basic build information", ConsoleColor.DarkGreen);
			await DetermineLastVersionChangeCommit (context);
			Log.StatusLine ();
			DetermineXACommitInfo (context);
		}

		void DetermineXACommitInfo (Context context)
		{
			GitRunner git = CreateGitRunner (context);
			XACommitHash = git.GetTopCommitHash (shortHash: false);
			XABranch = git.GetBranchName ();
		}

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
