using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

		public string Branch { get; private set; }
		public string Commit { get; private set; }
		public string Name   { get; private set; }
		public string Owner  { get; private set; }

		public static List<ExternalGitDependency> GetDependencies (Context context, string externalFilePath)
		{
			Log.Instance.StatusLine ($"  {context.Characters.Bullet} Reading external dependencies from {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, externalFilePath)}");
			string[] unparsedExternals = File.ReadAllLines (externalFilePath);
			var externals = new List<ExternalGitDependency> (unparsedExternals.Length);

			foreach (string external in unparsedExternals) {
				Match match = externalRegex.Match (external);
				if (match != null && match.Success) {
					if (match.Groups["comment"].Success) {
						// Ignore matching lines which start with '#'.
						continue;
					}

					var e = new ExternalGitDependency {
						Branch = match.Groups["branch"].Value,
						Commit = match.Groups["commit"].Value,
						Name   = match.Groups["repo"].Value,
						Owner  = match.Groups["owner"].Value,
					};
					externals.Add (e);
					Log.Instance.StatusLine ($"    {context.Characters.Bullet} {e.Owner}/{e.Name} ({e.Commit})");
				}
			}

			return externals;
		}
	}
}
