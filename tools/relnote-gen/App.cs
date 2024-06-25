using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Xamarin.Android.Tools.ReleaseNotes;

enum State {
	None,
	LookingForSummary,
	InBody,
	InDiff,
	InReleaseNotesDiff,
}

static class App {

	const string XamarinAndroidCommitBase   = "https://github.com/dotnet/android/commit/";
	const string XamarinAndroidPullBase     = "https://github.com/dotnet/android/pull/";
	const string XamarinAndroidIssuesBase   = "https://github.com/dotnet/android/issues/";

	static readonly Regex SummaryParser = new Regex (
		@"^\s*(\[(?<component>[^]]+)\]\s+)?" +
		@"(?<summary>.*?)" +
		@"\s*(\((?<pr>#.*)\))?$"
	);
	static readonly Regex FixesParser = new Regex (
		@"^\s*Fixes(:)?\s*(?<url>.*)$"
	);

	public static void Main (string[] args)
	{
		var commits = new Dictionary<string, List<CommitInfo>>(StringComparer.OrdinalIgnoreCase);

		var commit = (CommitInfo?) null;
		var state = State.None;
		var line = (string?) null;
		while ((line = Console.ReadLine ()) != null) {
			if (line.StartsWith ("commit ", StringComparison.OrdinalIgnoreCase)) {
				commit = new CommitInfo (line.Substring ("commit ".Length));
				state = State.LookingForSummary;
				continue;
			}
			if (line.StartsWith ("Author:", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}
			if (line.StartsWith ("Date:", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}
			// Console.WriteLine ($"# jonp: state={state}; line={line}");
			switch (state) {
			case State.LookingForSummary:
				Debug.Assert (commit != null);
				if (string.IsNullOrEmpty (line)) {
					continue;
				}
				var (component, pr, summary) = GetSummaryInfo (line);
				commit.Summary  = summary;
				commit.PR       = pr;
				if (!commits.TryGetValue (component, out var list)) {
					commits.Add (component, list = new());
				}
				list.Add (commit);
				state = State.InBody;
				break;
			case State.InBody:
				Debug.Assert (commit != null);
				var m = FixesParser.Match (line);
				if (m.Success && m.Groups ["url"].Value is string u && !string.IsNullOrEmpty (u)) {
					commit.Fixes.Add (u);
					continue;
				}
				if (line.StartsWith ("diff", StringComparison.Ordinal)) {
					state = State.InDiff;
					continue;
				}
				commit.CommitMessage.Add (line);
				break;
			case State.InDiff:
				if (line.StartsWith ("+++ b/Documentation/release-notes/", StringComparison.OrdinalIgnoreCase)) {
					state = State.InReleaseNotesDiff;
					continue;
				}
				break;
			case State.InReleaseNotesDiff:
				Debug.Assert (commit != null);
				if (line.StartsWith ("@@ -0", StringComparison.Ordinal)) {
					continue;
				}
				if (line.StartsWith (@"\ No newline at end of file", StringComparison.Ordinal) ||
						line.StartsWith ("diff", StringComparison.Ordinal)) {
					state = State.InDiff;
					continue;
				}
				commit.ReleaseNotes.Add (line.Substring (1));
				break;
			default:
				break;
			}
		}

		foreach (var component in commits.Keys.OrderBy (k => k)) {
			var c = component;
			if (c == "") {
				c = "Miscellaneous";
			}
			Console.WriteLine ($"- [{c}](#{c})");
		}

		foreach (var e in commits.OrderBy (e => e.Key)) {
			Console.WriteLine ();
			var component = e.Key;
			if (component == "") {
				component = "Miscellaneous";
			}
			Console.WriteLine ($"<a name=\"{component}\"></a>");
			Console.WriteLine ($"### {component}");
			foreach (var c in ((IEnumerable<CommitInfo>)e.Value).Reverse ()) {
				Console.WriteLine ();
				WriteCommit (c);
			}
		}
	}

	static (string component, string? pr, string summary) GetSummaryInfo (string line)
	{
		var m = SummaryParser.Match (line);
		if (!m.Success) {
			return ("", null, line);
		}

		var x = (
			component: m.Groups ["component"].Value?.Trim () ?? "",
			pr: m.Groups ["pr"].Value?.Trim (),
			summary: m.Groups ["summary"].Value?.Trim () ?? "");
		return x;
	}

	static void WriteCommit (CommitInfo c)
	{
		var links = c.Fixes.Select (f => GetLinkFromUrl (f)).ToList ();
		links.Sort ();
		if (!string.IsNullOrEmpty (c.PR)) {
			links.Add ($"[PR {c.PR}]({XamarinAndroidPullBase}{c.PR})");
		}
		links.Add ($"[Commit {c.CommitHash.Substring (0, 8)}]({XamarinAndroidCommitBase}{c.CommitHash})");
		var linkIndent = string.Join (",\n  ", links);
		Console.WriteLine ($"- {c.Summary}  ");
		Console.WriteLine ($"  ({linkIndent})");
		if (c.ReleaseNotes.Count > 0) {
			Console.WriteLine ();
			Console.WriteLine ($"  {string.Join ("\n  ", c.ReleaseNotes)}");
		}
		if (c.CommitMessage.Count > 0) {
			Console.WriteLine ($"  <!-- begin {c.CommitHash} commit message");
			foreach (var m in c.CommitMessage) {
				var v = m;
				if (v?.Length > 2) {
					v = v.Substring (2);
				}
				Console.WriteLine (v);
			}
			Console.WriteLine ($"  end {c.CommitHash} commit message -->");
			Console.WriteLine ("");
		}
	}

	static string GetLinkFromUrl (string url)
	{
		int s = url.LastIndexOf ('/');
		if (s <= 0) {
			return $"[Issue #{url}]({XamarinAndroidIssuesBase + url})";
		}
		return $"[Issue #{url.Substring (s+1)}]({url})";
	}
}








