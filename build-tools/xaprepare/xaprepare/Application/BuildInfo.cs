using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class BuildInfo : AppObject
	{
		static readonly char[] NDKPropertySeparator = new [] { '=' };
		static readonly char[] NDKPlatformDirectorySeparator = new [] { '-' };

		public string CommitOfLastVersionChange { get; private set; }

		// Not available from the start, only after NDK is installed
		public string NDKRevision            { get; private set; } = String.Empty;
		public string NDKVersionMajor        { get; private set; } = String.Empty;
		public string NDKVersionMinor        { get; private set; } = String.Empty;
		public string NDKVersionMicro        { get; private set; } = String.Empty;
		public string NDKMinimumApiAvailable { get; private set; } = String.Empty;

		public string VersionHash            { get; private set; } = String.Empty;
		public string LibZipHash             { get; private set; } = String.Empty;
		public string FullLibZipHash         { get; private set; } = String.Empty;
		public string MonoHash               { get; private set; } = String.Empty;
		public string FullMonoHash           { get; private set; } = String.Empty;

		public async Task GatherGitInfo (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			Log.StatusLine ();
			Log.StatusLine ("Determining basic build information", ConsoleColor.DarkGreen);
			await DetermineLastVersionChangeCommit (context);
			Log.StatusLine ();
			DetermineBundleHashes (context);
			Log.StatusLine ();
		}

		public bool GatherNDKInfo (Context context, string ndkRoot)
		{
			string props = Path.Combine (ndkRoot, "source.properties");
			if (!File.Exists (props)) {
				Log.ErrorLine ("NDK properties file does not exist: ", props, tailColor: Log.DestinationColor);
				return false;
			}

			string[] lines = File.ReadAllLines (props);
			foreach (string l in lines) {
				string line = l.Trim ();
				string[] parts = line.Split (NDKPropertySeparator, 2);
				if (parts.Length != 2)
					continue;

				if (String.Compare ("Pkg.Revision", parts [0].Trim (), StringComparison.Ordinal) != 0)
					continue;

				string rev = parts [1].Trim ();
				NDKRevision = rev;

				Version ver;
				if (!Version.TryParse (rev, out ver)) {
					Log.ErrorLine ($"Unable to parse NDK revision '{rev}' as a valid version string");
					return false;
				}

				NDKVersionMajor = ver.Major.ToString ();
				NDKVersionMinor = ver.Minor.ToString ();
				NDKVersionMicro = ver.Build.ToString ();
				break;
			}

			int minimumApi = Int32.MaxValue;
			string platforms = Path.Combine (ndkRoot, "platforms");
			foreach (string p in Directory.EnumerateDirectories (platforms, "android-*", SearchOption.TopDirectoryOnly)) {
				string pdir = Path.GetFileName (p);
				string[] parts = pdir.Split (NDKPlatformDirectorySeparator, 2);
				if (parts.Length != 2)
					continue;

				int api;
				if (!Int32.TryParse (parts [1].Trim (), out api))
					continue;

				if (api >= minimumApi)
					continue;

				minimumApi = api;
			}

			NDKMinimumApiAvailable = minimumApi.ToString ();
			return true;
		}

		void DetermineBundleHashes (Context context)
		{
			GitRunner git = CreateGitRunner (context);

			Log.StatusLine ($"  {context.Characters.Bullet} LibZip commit hash", ConsoleColor.Gray);
			FullLibZipHash = git.GetTopCommitHash (context.Properties.GetRequiredValue (KnownProperties.LibZipSourceFullPath), shortHash: false);
			LibZipHash = EnsureHash ("LibZip", Utilities.ShortenGitHash (FullLibZipHash));

			Log.StatusLine ($"  {context.Characters.Bullet} Mono commit hash", ConsoleColor.Gray);
			FullMonoHash = git.GetTopCommitHash (context.Properties.GetRequiredValue (KnownProperties.MonoSourceFullPath), shortHash: false);
			MonoHash = EnsureHash ("Mono", Utilities.ShortenGitHash (FullMonoHash));

			if (Configurables.Paths.BundleVersionHashFiles == null || Configurables.Paths.BundleVersionHashFiles.Count == 0) {
				Log.WarningLine ("Bundle version hash files not specified");
				return;
			}

			Log.StatusLine ($"  {context.Characters.Bullet} Generating bundle version hash", ConsoleColor.Gray);
			using (var ha = HashAlgorithm.Create (context.HashAlgorithm)) {
				HashFiles (ha, Configurables.Paths.BundleVersionHashFiles);
				VersionHash = FormatHash (ha.Hash).Substring (0, (int)Configurables.Defaults.AbbreviatedHashLength);
				Log.StatusLine ("    Hash: ", VersionHash, tailColor: ConsoleColor.Cyan);
			}

			string EnsureHash (string name, string hash)
			{
				if (String.IsNullOrEmpty (hash))
					throw new InvalidOperationException ($"Unable to determine {name} commit hash");
				Log.StatusLine ("    Commit: ", hash, tailColor: ConsoleColor.Cyan);
				Log.StatusLine ();

				return hash;
			}
		}

		void HashFiles (HashAlgorithm ha, List<string> globPatterns)
		{
			var block = new byte [4096];
			foreach (string glob in globPatterns) {
				string pattern = glob?.Trim ();
				if (String.IsNullOrEmpty (pattern))
					continue;

				foreach (string file in Directory.EnumerateFiles (Path.GetDirectoryName (pattern), Path.GetFileName (pattern))) {
					Log.StatusLine ("      file: ", Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, file), tailColor: ConsoleColor.Cyan);
					HashFile (ha, file, block);
				}
			}
			ha.TransformFinalBlock (block, 0, 0);
		}

		void HashFile (HashAlgorithm ha, string filePath, byte[] block)
		{
			using (var memoryStream = new MemoryStream ()) {
				//Read the file into a MemoryStream, ignoring newlines
				using (var file = File.OpenRead (filePath)) {
					int readByte;
					while ((readByte = file.ReadByte ()) != -1) {
						byte b = (byte)readByte;
						if (b != '\r' && b != '\n') {
							memoryStream.WriteByte (b);
						}
					}
				}
				memoryStream.Seek (0, SeekOrigin.Begin);

				int read;
				while ((read = memoryStream.Read (block, 0, block.Length)) > 0) {
					ha.TransformBlock (block, 0, read, block, 0);
				}
			}
		}

		string FormatHash (byte[] hash)
		{
			return string.Join (String.Empty, hash.Select (b => b.ToString ("x2")));
		}

		async Task DetermineLastVersionChangeCommit (Context context)
		{
			Log.StatusLine ($"  {context.Characters.Bullet} Commit of last version change", ConsoleColor.Gray);
			GitRunner git = CreateGitRunner (context);
			IList <GitRunner.BlamePorcelainEntry> blameEntries;

			blameEntries = await git.Blame ("Configuration.props");
			if (blameEntries == null || blameEntries.Count == 0)
				throw new InvalidOperationException ("Unable to determine the last version change commit");

			foreach (GitRunner.BlamePorcelainEntry be in blameEntries) {
				if (be == null || String.IsNullOrEmpty (be.Line))
					continue;

				if (be.Line.IndexOf ("<ProductVersion>") >= 0) {
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
