#!/usr/bin/env dotnet
// Enriched failure analysis for one dnceng-public `dotnet-android` build:
//   1. cross-config matrix per failed test (failed/passed/retried configs) + stack/asserts
//   2. crashed / incomplete lanes (started-but-not-finished culprit lives in logcat)
//   3. branch cross-reference (PR changes that name a failing test's class/namespace/assembly)
//
// Needs `az login`. Usage: dotnet run ci_failures.cs -- --build-id N [--pr N] [--repo dotnet/android]

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string ORG = "https://dev.azure.com/dnceng-public";
const string PROJECT = "public";
const string RES = "499b84ac-1321-427f-aa17-267ca6975798";

// ---------------- argument parsing ----------------
string? buildId = null, pr = null, repo = "dotnet/android";
for (int i = 0; i < args.Length; i++) {
	switch (args [i]) {
		case "--build-id": buildId = ++i < args.Length ? args [i] : null; break;
		case "--pr": pr = ++i < args.Length ? args [i] : null; break;
		case "--repo": repo = ++i < args.Length ? args [i] : repo; break;
	}
}
if (string.IsNullOrEmpty (buildId)) {
	Console.Error.WriteLine ("usage: dotnet run ci_failures.cs -- --build-id N [--pr N] [--repo dotnet/android]");
	return 1;
}

// ---------------- main ----------------
var failed = GetArray (AzJson ($"{ORG}/{PROJECT}/_apis/test/ResultsByBuild?buildId={buildId}&outcomes=Failed&api-version=7.1-preview"), "value");
var runs = GetArray (AzJson ($"{ORG}/{PROJECT}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version=7.1&includeRunDetails=true"), "value");
var timeline = AzJson ($"{ORG}/{PROJECT}/_apis/build/builds/{buildId}/timeline?api-version=7.1") ?? new JsonObject ();

var runById = new Dictionary<int, JsonNode> ();
foreach (var r in runs)
	if (r is not null)
		runById [ToInt (r ["id"])] = r;

P ($"# Failure analysis - build {buildId}");
P ();
if (failed.Count > 0)
	SectionMatrix (buildId, failed, runs, runById);
else {
	P ("_No failed tests in the test API (build may still be red via crash/timeout below)._");
	P ();
}
SectionCrashes (buildId, runs, timeline);
if (!string.IsNullOrEmpty (pr))
	SectionXref (failed, repo, pr);

return 0;

// ---------------- section 1: cross-config matrix ----------------
void SectionMatrix (string bid, JsonArray failed, JsonArray runs, Dictionary<int, JsonNode> runById)
{
	var failRuns = new Dictionary<string, HashSet<int>> ();
	var storage = new Dictionary<string, string?> ();
	foreach (var f in failed) {
		if (f is null)
			continue;
		var name = Str (f ["automatedTestName"]);
		if (name.Length == 0)
			continue;
		if (!failRuns.TryGetValue (name, out var set))
			failRuns [name] = set = new HashSet<int> ();
		set.Add (ToInt (f ["runId"]));
		storage [name] = StrN (f ["automatedTestStorage"]);
	}

	string FirstBase (HashSet<int> rids)
	{
		foreach (var r in rids)
			if (runById.TryGetValue (r, out var run))
				return BaseOf (Str (run ["name"]));
		return "";
	}

	var fam = failRuns.ToDictionary (kv => kv.Key, kv => FirstBase (kv.Value));
	var cand = new Dictionary<string, List<JsonNode>> ();
	foreach (var fk in fam.Values.Distinct ()) {
		var list = new List<JsonNode> ();
		foreach (var r in runs)
			if (r is not null && BaseOf (Str (r ["name"])) == fk)
				list.Add (r);
		cand [fk] = list;
	}
	var ids = new HashSet<int> ();
	foreach (var fk in fam.Values)
		foreach (var r in cand [fk])
			ids.Add (ToInt (r ["id"]));
	var cache = FetchAll (ids);

	P ($"## Failed-test cross-config matrix — {failRuns.Count} distinct test(s)");
	P ();
	foreach (var n in failRuns.Keys.OrderBy (x => x, StringComparer.Ordinal)) {
		var fk = fam [n];
		var cfg = new Dictionary<string, List<(string completed, string outcome)>> ();
		foreach (var r in cand [fk]) {
			var rid = ToInt (r ["id"]);
			if (cache.TryGetValue (rid, out var m) && m.TryGetValue (n, out var row)) {
				var rname = Str (r ["name"]);
				if (!cfg.TryGetValue (rname, out var lst))
					cfg [rname] = lst = new List<(string, string)> ();
				lst.Add ((StrN (r ["completedDate"]) ?? "", row.outcome ?? ""));
			}
		}
		int li = n.LastIndexOf ('.');
		string shortN = li >= 0 ? n [(li + 1)..] : n;
		string ns = li >= 0 ? n [..li] : n;
		P ($"### `{shortN}`  ({ns})");
		storage.TryGetValue (n, out var asm);
		P ($"- assembly `{asm}` · family `{fk}`");
		var fl = new List<string> ();
		var pa = new List<string> ();
		var ot = new List<string> ();
		foreach (var name in cfg.Keys.OrderBy (x => x, StringComparer.Ordinal)) {
			var outs = cfg [name]
				.OrderBy (t => t.completed, StringComparer.Ordinal)
				.ThenBy (t => t.outcome, StringComparer.Ordinal)
				.Select (t => t.outcome).ToList ();
			string label = name.Length >= fk.Length ? name [fk.Length..] : name;
			label = label.TrimStart (' ', '-');
			if (label.Length == 0)
				label = name;
			string disp = outs.Distinct ().Count () > 1 ? string.Join ("->", outs) + " (retry)" : outs [0];
			string entry = $"`{label}`" + (disp == "Passed" ? "" : $" ({disp})");
			if (outs.Contains ("Failed"))
				fl.Add (entry);
			else if (outs.Distinct ().Count () == 1 && outs [0] == "Passed")
				pa.Add (entry);
			else
				ot.Add (entry);
		}
		P ($"- FAILED in: {(fl.Count > 0 ? string.Join (", ", fl) : "-")}");
		P ($"- passed in: {(pa.Count > 0 ? string.Join (", ", pa) : "-")}");
		if (ot.Count > 0)
			P ($"- other: {string.Join (", ", ot)}");
		foreach (var rid in failRuns [n]) {
			if (cache.TryGetValue (rid, out var m) && m.TryGetValue (n, out var row) && !string.IsNullOrEmpty (row.err)) {
				P ($"- assert/error: {Trunc (Lines (row.err) [0], 300)}");
				if (!string.IsNullOrEmpty (row.stack)) {
					P ("  ```");
					foreach (var ln in Lines (row.stack).Take (6))
						P ("  " + Trunc (ln, 200));
					P ("  ```");
				}
				break;
			}
		}
		P ();
	}
}

// ---------------- section 2: crashed / incomplete lanes ----------------
void SectionCrashes (string bid, JsonArray runs, JsonNode timeline)
{
	var recs = (timeline ["records"] as JsonArray) ?? new JsonArray ();
	var published = new Dictionary<string, JsonNode> ();
	foreach (var r in runs)
		if (r is not null)
			published [Str (r ["name"])] = r;
	var crashed = new List<(string name, string why)> ();
	// incomplete test runs (runner died mid-run)
	foreach (var r in runs) {
		if (r is null)
			continue;
		int inc = ToInt (r ["incompleteTests"]);
		if (inc > 0)
			crashed.Add ((Str (r ["name"]), $"{inc} test(s) did not complete - runner died mid-run"));
	}
	// "run <flavor>" tasks that did not cleanly succeed AND published no (complete) results = crash/zero-tests
	foreach (var rec in recs) {
		if (rec is null)
			continue;
		var type = Str (rec ["type"]);
		var name = Str (rec ["name"]);
		var result = Str (rec ["result"]);
		if (type == "Task" && name.StartsWith ("run ") &&
				(result == "failed" || result == "succeededWithIssues" || result == "canceled")) {
			var flavor = name [4..].Trim ();
			if (!published.TryGetValue (flavor, out var run) || ToInt (run ["incompleteTests"]) > 0)
				crashed.Add ((flavor, $"`run` task {result} but no complete test run published - app likely crashed ('Zero tests ran' / native crash)"));
		}
	}
	// job-level timeouts (hang)
	foreach (var rec in recs) {
		if (rec is null)
			continue;
		if (Str (rec ["type"]) == "Job" && Str (rec ["result"]) == "canceled") {
			var issues = (rec ["issues"] as JsonArray) ?? new JsonArray ();
			var msg = string.Join (" ", issues.Select (i => Str (i? ["message"])));
			var m = Regex.Match (msg, @"maximum time of (\d+) minutes");
			if (m.Success)
				crashed.Add ((Str (rec ["name"]), $"timed out at {m.Groups [1].Value}-min cap - likely a hung test; last started test in logcat is the suspect"));
		}
	}
	if (crashed.Count == 0)
		return;
	P ("## Crashed / incomplete lanes  (!)");
	P ();
	P ("These went red with **no usable failed-test list** - the culprit (a test that **started but never finished**, or a native crash) is only in the device **logcat**, not the test API:");
	P ();
	var seen = new HashSet<(string, string)> ();
	foreach (var (name, why) in crashed) {
		if (!seen.Add ((name, why)))
			continue;
		P ($"- **{name}** - {why}");
	}
	P ();
	P ("To name the culprit, list this build's artifacts and download the matching `Test Results - ...` lane (large: 100MB-2GB - prefer a `Debug` lane), then scan its logcat (see references/azdo-queries.md):");
	P ();
	P ("```bash");
	P ($"az pipelines runs artifact list --run-id {bid} --org {ORG} --project {PROJECT} \\");
	P (@"  --query '[?starts_with(name, `Test Results`)].name' -o tsv");
	P ($"az pipelines runs artifact download --run-id {bid} --org {ORG} --project {PROJECT} \\");
	P ("  --artifact-name \"<paste matching Test Results - ... name>\" --path /tmp/cilogs");
	P (@"grep -nE 'Running |\[PASS\]|\[FAIL\]|SIGSEGV|SIGABRT|tombstone|FATAL|art::|JNI DETECTED|Process .*died' \\");
	P ("  /tmp/cilogs/**/logcat-*.txt | tail -60   # last test that STARTED with no PASS/FAIL = crasher");
	P ("```");
	P ();
}

// ---------------- section 3: branch cross-reference ----------------
void SectionXref (JsonArray failed, string repo, string pr)
{
	var names = new SortedSet<string> (StringComparer.Ordinal);
	foreach (var f in failed)
		if (f is not null) {
			var n = Str (f ["automatedTestName"]);
			if (n.Length > 0)
				names.Add (n);
		}
	if (names.Count == 0)
		return;
	var (code, stdout, stderr) = Run ("gh", "pr", "diff", pr, "--repo", repo, "--name-only");
	if (code != 0) {
		Console.Error.Write ($"gh diff failed: {Trunc (stderr, 200)}\n");
		return;
	}
	var files = stdout.Replace ("\r\n", "\n").Split ('\n').Where (l => l.Trim ().Length > 0).ToList ();
	var stems = new Dictionary<string, string> ();
	foreach (var f in files) {
		var leaf = f.Contains ('/') ? f [(f.LastIndexOf ('/') + 1)..] : f;
		var stem = leaf.Contains ('.') ? leaf [..leaf.LastIndexOf ('.')] : leaf;
		stems [stem] = f;
	}
	P ("## Branch cross-reference");
	P ();
	P ($"PR #{pr} changes {files.Count} file(s). Name overlaps with failing tests (judge if causal):");
	P ();
	bool anyHit = false;
	foreach (var n in names) {
		var parts = n.Split ('.');
		string cls = parts.Length >= 2 ? parts [^2] : "";
		string method = parts [^1];
		var nsParts = parts.Take (Math.Max (0, parts.Length - 2)).ToHashSet ();
		var hits = new SortedSet<string> (StringComparer.Ordinal);
		foreach (var (stem, path) in stems) {
			if (stem.Length > 0 && (stem == cls || stem == method || nsParts.Contains (stem) || (cls.Length > 0 && path.Contains (cls))))
				hits.Add (path);
		}
		if (hits.Count > 0) {
			anyHit = true;
			P ($"- `{cls}.{method}` <- {string.Join (", ", hits.Take (5).Select (h => "`" + h + "`"))}");
		}
	}
	if (!anyHit)
		P ("- No direct file-name overlap. Check whether changed runtime/build code affects the failing assembly.");
	P ();
}

// ---------------- helpers ----------------
JsonNode? AzJson (string url)
{
	var (code, stdout, stderr) = Run ("az", "rest", "--method", "get", "--resource", RES, "--url", url, "-o", "json");
	if (code != 0) {
		Console.Error.Write ($"az error {url}\n{Trunc (stderr, 300)}\n");
		return null;
	}
	try {
		return JsonNode.Parse (stdout);
	} catch (JsonException) {
		return null;
	}
}

(int rid, Dictionary<string, (string? outcome, string? err, string? stack)> map) RunResults (int rid)
{
	var data = AzJson ($"{ORG}/{PROJECT}/_apis/test/Runs/{rid}/results?api-version=7.1&$top=5000");
	var outd = new Dictionary<string, (string?, string?, string?)> ();
	var arr = (data? ["value"] as JsonArray) ?? new JsonArray ();
	foreach (var row in arr) {
		if (row is null)
			continue;
		var n = Str (row ["automatedTestName"]);
		if (n.Length > 0)
			outd [n] = (StrN (row ["outcome"]), StrN (row ["errorMessage"]), StrN (row ["stackTrace"]));
	}
	return (rid, outd);
}

Dictionary<int, Dictionary<string, (string? outcome, string? err, string? stack)>> FetchAll (IEnumerable<int> rids)
{
	var result = new ConcurrentDictionary<int, Dictionary<string, (string?, string?, string?)>> ();
	var list = rids.ToList ();
	if (list.Count == 0)
		return new Dictionary<int, Dictionary<string, (string?, string?, string?)>> ();
	Parallel.ForEach (list, new ParallelOptions { MaxDegreeOfParallelism = 6 }, rid => {
		var (r, o) = RunResults (rid);
		result [r] = o;
	});
	return new Dictionary<int, Dictionary<string, (string?, string?, string?)>> (result);
}

// Strip flavor/OS/index suffix so sibling configs share one base.
// 'Mono.Android.NET_Tests-NativeAOT' -> 'Mono.Android.NET_Tests';
// 'Xamarin.Android.Build.Tests - macOS-7' -> 'Xamarin.Android.Build.Tests'.
static string BaseOf (string name)
{
	var b = Regex.Replace (name, @" - (macOS|Windows|Linux)(-\d+)?$", "");
	b = Regex.Replace (b, @"-[A-Za-z0-9]+$", "");
	return b;
}

static (int code, string stdout, string stderr) Run (string file, params string [] cliArgs)
{
	var psi = new ProcessStartInfo (file) {
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false,
	};
	foreach (var a in cliArgs)
		psi.ArgumentList.Add (a);
	using var proc = Process.Start (psi);
	if (proc is null)
		return (-1, "", $"failed to start {file}");
	string stdout = proc.StandardOutput.ReadToEnd ();
	string stderr = proc.StandardError.ReadToEnd ();
	proc.WaitForExit ();
	return (proc.ExitCode, stdout, stderr);
}

static JsonArray GetArray (JsonNode? root, string key)
	=> root? [key] as JsonArray ?? new JsonArray ();

static string Str (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.String ? "" : node.GetValue<string> ();

static string? StrN (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.String ? null : node.GetValue<string> ();

static int ToInt (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.Number ? 0 : node.GetValue<int> ();

static string Trunc (string s, int n)
	=> s.Length <= n ? s : s [..n];

static string [] Lines (string s)
{
	var t = s.Replace ("\r\n", "\n").Trim ();
	return t.Split ('\n');
}

static void P (string s = "") => Console.WriteLine (s);
