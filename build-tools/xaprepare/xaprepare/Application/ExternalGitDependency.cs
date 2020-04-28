using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Prepare
{
	sealed class ExternalGitDependency : AppObject
	{
		const string GitHubServer = "github.com";

		static readonly Regex externalRegex = new Regex (@"
^
\s*
(?<comment>\#.*)
|
(
  \s*
  (?<owner>[^/]+)
  /
  (?<repo>[^:]+)
  :
  (?<branch>[^@]+)
  @
  (?<commit>.*)
)
$
", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

		public string Branch { get; private set; } = String.Empty;
		public string Commit { get; private set; } = String.Empty;
		public string Name   { get; private set; } = String.Empty;
		public string Owner  { get; private set; } = String.Empty;

		public static List<ExternalGitDependency> GetDependencies (Context context, string externalFilePath, bool quiet = false)
		{
			if (!quiet)
				Log.Instance.StatusLine ($"  {context.Characters.Bullet} Reading external dependencies from {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, externalFilePath)}");
			string[] unparsedExternals = File.ReadAllLines (externalFilePath);
			var externals = new List<ExternalGitDependency> (unparsedExternals.Length);
			bool includeCommercial = context.CheckCondition (KnownConditions.IncludeCommercial);

			foreach (string external in unparsedExternals) {
				Match match = externalRegex.Match (external);
				if (match != null && match.Success) {
					if (match.Groups["comment"].Success) {
						// Ignore matching lines which start with '#'.
						continue;
					}

					string owner = match.Groups["owner"].Value;
					string repo = match.Groups["repo"].Value;
					if (!includeCommercial && Configurables.Defaults.CommercialExternalDependencies.Contains ($"{owner}/{repo}")) {
						Log.Instance.DebugLine ($"Ignoring external commercial dependency '{owner}/{repo}'");
						continue;
					}

					var e = new ExternalGitDependency {
						Branch = match.Groups["branch"].Value,
						Commit = match.Groups["commit"].Value,
						Name   = repo,
						Owner  = owner,
					};
					externals.Add (e);
					if (!quiet)
						Log.Instance.StatusLine ($"    {context.Characters.Bullet} {e.Owner}/{e.Name} ({e.Commit})");
				}
			}

			return externals;
		}
	}
}
