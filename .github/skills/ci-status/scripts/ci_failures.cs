#!/usr/bin/env dotnet
// Evidence-based failure analysis for one dnceng-public `dotnet-android` build.
// This tool is read-only: it never retries Azure stages or mutates GitHub issues.
//
// Live:
//   dotnet run ci_failures.cs -- --build-id N [--pr N] [--repo dotnet/android] [--format markdown|json]

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

const string ORG = "https://dev.azure.com/dnceng-public";
const string PROJECT = "public";
const string RES = "499b84ac-1321-427f-aa17-267ca6975798";

string? buildId = null;
string? pr = null;
string repo = "dotnet/android";
string format = "markdown";
var fetchErrors = new List<string> ();
bool prDiffAvailable = false;

for (int i = 0; i < args.Length; i++) {
	switch (args [i]) {
		case "--build-id":
			buildId = ++i < args.Length ? args [i] : null;
			break;
		case "--pr":
			pr = ++i < args.Length ? args [i] : null;
			break;
		case "--repo":
			repo = ++i < args.Length ? args [i] : repo;
			break;
		case "--format":
			format = ++i < args.Length ? args [i] : format;
			break;
	}
}

if (format != "markdown" && format != "json") {
	Console.Error.WriteLine ("--format must be 'markdown' or 'json'");
	return 1;
}

if (string.IsNullOrEmpty (buildId)) {
	Console.Error.WriteLine ("usage: dotnet run ci_failures.cs -- --build-id N [--pr N] [--repo dotnet/android] [--format markdown|json]");
	return 1;
}

var build = AzJson ($"{ORG}/{PROJECT}/_apis/build/builds/{buildId}?api-version=7.1") as JsonObject ?? new JsonObject ();
pr ??= StrN (build ["pr"]);
if (string.IsNullOrEmpty (pr)) {
	var sourceBranch = Str (build ["sourceBranch"]);
	var prMatch = Regex.Match (sourceBranch, @"^refs/pull/(?<pr>\d+)/merge$");
	if (prMatch.Success)
		pr = prMatch.Groups ["pr"].Value;
}

var rootTimeline = AzJson ($"{ORG}/{PROJECT}/_apis/build/builds/{buildId}/timeline?api-version=7.1") as JsonObject ?? new JsonObject ();
var timelines = LoadTimelines (rootTimeline);
var failedTests = AzPagedArray ($"{ORG}/{PROJECT}/_apis/test/ResultsByBuild?buildId={buildId}&outcomes=Failed&api-version=7.1-preview");
var runs = GetArray (AzJson ($"{ORG}/{PROJECT}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version=7.1&includeRunDetails=true"), "value");
var prFiles = LoadPrFiles ();
var runResults = new ConcurrentDictionary<int, JsonArray> ();
var failures = new List<Failure> ();

AnalyzeTimelineFailures ();
AnalyzeFailedTests ();
AnalyzeIncompleteRuns ();

failures = failures
	.GroupBy (f => $"{f.Stage.RefName}|{f.Fingerprint}", StringComparer.Ordinal)
	.Select (g => MergeFailures (g.ToList ()))
	.OrderBy (f => f.Stage.Name, StringComparer.Ordinal)
	.ThenBy (f => f.Fingerprint, StringComparer.Ordinal)
	.ToList ();

var retryPlan = BuildRetryPlan (failures);
var report = new AnalysisReport {
	SchemaVersion = 1,
	BuildId = buildId,
	Pr = pr,
	Build = new BuildSummary {
		Status = Str (build ["status"]),
		Result = Str (build ["result"]),
		SourceBranch = Str (build ["sourceBranch"]),
		SourceVersion = Str (build ["sourceVersion"]),
	},
	Failures = failures,
	RetryPlan = retryPlan,
	Errors = fetchErrors.Distinct (StringComparer.Ordinal).ToList (),
};

if (format == "json") {
	Console.WriteLine (JsonSerializer.Serialize (report, ReportJsonContext.Default.AnalysisReport));
} else {
	PrintMarkdown (report);
}

return fetchErrors.Count == 0 ? 0 : 2;

void AnalyzeTimelineFailures ()
{
	foreach (var timeline in timelines) {
		var records = timeline.Records;
		foreach (var record in records) {
			if (record is null || !IsFailed (record))
				continue;
			var type = Str (record ["type"]);
			if (type != "Task" && type != "Job")
				continue;

			var messages = IssueMessages (record);
			if (type == "Task" && NeedsTaskLog (messages)) {
				var logText = TaskLog (record);
				foreach (var line in SignificantLogLines (logText))
					messages.Add (line);
			}
			messages = DistinctMeaningful (messages);
			var stage = StageFor (record, records);
			if (stage is null)
				continue;
			var job = JobFor (record, records);
			if (messages.Count == 0) {
				if (type == "Task" && job is not null && IssueMessages (job).Count > 0)
					continue;
				if (type == "Job" && HasFailedTaskDescendant (record, records))
					continue;
				messages.Add ("No timeline issue or readable task log identified the root cause.");
			}
			var classification = ClassifyRoot (messages, prFiles);
			var taskName = type == "Task" ? Str (record ["name"]) : "";
			var jobName = job is null ? (type == "Job" ? Str (record ["name"]) : "") : Str (job ["name"]);
			var fingerprint = RootFingerprint (messages, taskName, jobName);
			var terms = SearchTerms (messages, taskName, jobName);
			var retryable = IsRetryable (classification.Category, classification.Confidence);
			var evidence = CompactEvidence (messages, classification);

			failures.Add (new Failure {
				Fingerprint = fingerprint,
				Stage = ToStageInfo (stage, timeline.IsCurrent),
				Job = jobName,
				Task = taskName,
				Classification = classification.Category,
				Confidence = classification.Confidence,
				Gating = IsGatingStage (stage),
				Evidence = evidence,
				IssueSearchTerms = terms,
				Retry = new RetryRecommendation {
					Recommended = retryable,
					Safe = retryable,
					Reason = classification.Reason,
				},
			});
		}
	}
}

void AnalyzeFailedTests ()
{
	if (failedTests.Count == 0)
		return;

	var runById = new Dictionary<int, JsonNode> ();
	foreach (var run in runs)
		if (run is not null)
			runById [ToInt (run ["id"])] = run;

	var failedByName = new Dictionary<string, List<JsonNode>> (StringComparer.Ordinal);
	foreach (var failed in failedTests) {
		if (failed is null)
			continue;
		var name = Str (failed ["automatedTestName"]);
		if (name.Length == 0)
			continue;
		if (!failedByName.TryGetValue (name, out var list))
			failedByName [name] = list = [];
		list.Add (failed);
	}

	foreach (var (testName, rows) in failedByName) {
		var failedRunIds = rows.Select (r => ToInt (r ["runId"])).Where (id => id > 0).ToHashSet ();
		var storage = rows.Select (r => Str (r ["automatedTestStorage"])).FirstOrDefault (s => s.Length > 0) ?? "";
		var family = "";
		foreach (var runId in failedRunIds)
			if (runById.TryGetValue (runId, out var run)) {
				family = BaseOf (Str (run ["name"]));
				break;
			}

		var candidates = runs
			.Where (r => r is not null && (family.Length == 0 || BaseOf (Str (r ["name"])) == family))
			.Cast<JsonNode> ()
			.ToList ();
		var configRows = new Dictionary<string, List<(string completed, string outcome, bool autoRetry, int order)>> (StringComparer.Ordinal);
		string error = "";
		string stack = "";
		int observationOrder = 0;

		foreach (var run in candidates) {
			var runId = ToInt (run ["id"]);
			var results = ResultsForRun (runId);
			var matches = results
				.Where (row => row is not null && Str (row ["automatedTestName"]) == testName)
				.Cast<JsonNode> ()
				.OrderBy (row => Str (row ["completedDate"]), StringComparer.Ordinal)
				.ToList ();
			if (matches.Count == 0)
				continue;
			var outcomes = matches.Select (row => Str (row ["outcome"])).Where (o => o.Length > 0).ToList ();
			if (outcomes.Count == 0)
				continue;
			var displayName = ConfigName (Str (run ["name"]), family);
			var autoRetry = displayName.EndsWith (" (Auto-Retry)", StringComparison.Ordinal);
			var configName = Regex.Replace (displayName, @" \(Auto-Retry\)$", "");
			if (!configRows.TryGetValue (configName, out var observations))
				configRows [configName] = observations = [];
			foreach (var row in matches) {
				var outcome = Str (row ["outcome"]);
				if (outcome.Length > 0)
					observations.Add ((Str (row ["completedDate"]), outcome, autoRetry, observationOrder++));
				if (error.Length == 0 && Str (row ["errorMessage"]).Length > 0) {
					error = FirstLine (Str (row ["errorMessage"]));
					stack = string.Join ("\n", Lines (Str (row ["stackTrace"])).Take (6));
				}
			}
		}

		var configs = configRows
			.OrderBy (pair => pair.Key, StringComparer.Ordinal)
			.Select (pair => new TestConfiguration {
				Name = pair.Key,
				Outcomes = pair.Value.OrderBy (value => value.completed, StringComparer.Ordinal).ThenBy (value => value.order).Select (value => value.outcome).ToList (),
			})
			.ToList ();
		var failedConfigs = configs.Count (c => c.Outcomes.Contains ("Failed", StringComparer.Ordinal));
		var passedConfigs = configs.Count (c => c.Outcomes.Count > 0 && c.Outcomes.All (o => o == "Passed"));
		var retryPassed = configs.Any (c => FailedBeforePassed (c.Outcomes));
		var failedAfterAutoRetry = configRows.Values.SelectMany (values => values).Any (value => value.autoRetry && value.outcome == "Failed");
		var xref = FindXref (testName, prFiles);
		var classification = ClassifyTest (failedConfigs, passedConfigs, retryPassed, failedAfterAutoRetry, xref.Count > 0, prDiffAvailable);
		var firstRun = rows.Select (r => ToInt (r ["runId"])).FirstOrDefault ();
		var stage = StageForRun (runById.TryGetValue (firstRun, out var firstRunNode) ? firstRunNode : null);
		if (stage is null)
			stage = UnknownStage ();
		var evidence = new List<string> ();
		if (retryPassed)
			evidence.Add ("The same test changed from Failed to Passed on retry.");
		if (failedAfterAutoRetry)
			evidence.Add ("The test failed in the pipeline's Auto-Retry run, so the built-in retry did not clear it.");
		if (failedConfigs > 0)
			evidence.Add ($"Failed in {failedConfigs} configuration(s).");
		if (passedConfigs > 0)
			evidence.Add ($"Passed in {passedConfigs} sibling configuration(s).");
		if (xref.Count > 0)
			evidence.Add ($"PR file overlap: {string.Join (", ", xref.Take (5))}");
		if (!prDiffAvailable)
			evidence.Add ("PR diff was unavailable; classification fails closed.");
		if (error.Length > 0)
			evidence.Add (error);

		var retryable = IsRetryable (classification.Category, classification.Confidence);
		failures.Add (new Failure {
			Fingerprint = TestFingerprint (testName, error),
			Stage = stage,
			Job = StageJobForRun (firstRunNode),
			Classification = classification.Category,
			Confidence = classification.Confidence,
			Gating = stage.Result == "failed" || stage.Result == "canceled",
			Evidence = evidence,
			IssueSearchTerms = new List<string> { testName, ShortTestName (testName), FirstErrorToken (error) }.Where (s => s.Length > 0).Distinct ().ToList (),
			Retry = new RetryRecommendation {
				Recommended = retryable,
				Safe = retryable,
				Reason = classification.Reason,
			},
			Test = new TestFailure {
				Name = testName,
				Assembly = storage,
				Error = error,
				Stack = stack,
				Configurations = configs,
				ChangedFiles = xref,
			},
		});
	}
}

void AnalyzeIncompleteRuns ()
{
	foreach (var run in runs) {
		if (run is null)
			continue;
		var incomplete = ToInt (run ["incompleteTests"]);
		if (incomplete <= 0)
			continue;
		var stage = StageForRun (run) ?? UnknownStage ();
		var name = Str (run ["name"]);
		var evidence = $"{incomplete} test(s) did not complete in {name}; the test runner exited or crashed before publishing complete results.";
		var runtimeOverlap = TouchesRuntime (prFiles);
		var category = runtimeOverlap ? "likely-pr-regression" : "timeout-or-crash";
		var confidence = runtimeOverlap ? 0.70 : 0.90;
		var retryable = !runtimeOverlap;
		failures.Add (new Failure {
			Fingerprint = $"incomplete-run|{Slug (name)}",
			Stage = stage,
			Job = StageJobForRun (run),
			Classification = category,
			Confidence = confidence,
			Gating = stage.Result == "failed" || stage.Result == "canceled",
			Evidence = runtimeOverlap
				? [evidence, "The PR changes runtime/native code that can directly cause this crash shape."]
				: [evidence],
			IssueSearchTerms = [name, "incomplete tests", "native crash"],
			Retry = new RetryRecommendation {
				Recommended = retryable,
				Safe = retryable,
				Reason = runtimeOverlap
					? "The PR changes runtime/native code; inspect logcat before retrying."
					: "The lane published incomplete test results; inspect logcat for the culprit and retry the failed stage.",
			},
		});
	}
}

List<TimelineData> LoadTimelines (JsonNode root)
{
	var result = new List<TimelineData> {
		new TimelineData {
			Id = "current",
			IsCurrent = true,
			Records = GetArray (root, "records"),
		},
	};
	var loaded = new HashSet<string> (StringComparer.Ordinal);
	foreach (var stage in result [0].Records) {
		if (stage is null || Str (stage ["type"]) != "Stage")
			continue;
		var attempts = stage ["previousAttempts"] as JsonArray;
		if (attempts is null)
			continue;
		foreach (var attempt in attempts) {
			var timelineId = Str (attempt? ["timelineId"]);
			if (timelineId.Length == 0 || !loaded.Add (timelineId))
				continue;
			var previous = AzJson ($"{ORG}/{PROJECT}/_apis/build/builds/{buildId}/timeline/{timelineId}?api-version=7.1") ?? new JsonObject ();
			result.Add (new TimelineData {
				Id = timelineId,
				IsCurrent = false,
				Records = GetArray (previous, "records"),
			});
		}
	}
	return result;
}

List<string> LoadPrFiles ()
{
	if (string.IsNullOrEmpty (pr)) {
		fetchErrors.Add ("The PR number could not be derived; PR diff evidence is unavailable.");
		return [];
	}
	var (code, stdout, stderr) = Run ("gh", "pr", "diff", pr, "--repo", repo, "--name-only");
	if (code != 0) {
		Console.Error.WriteLine ($"gh diff failed: {Trunc (stderr, 200)}");
		fetchErrors.Add ($"GitHub PR diff could not be loaded for PR #{pr}.");
		return [];
	}
	prDiffAvailable = true;
	return Lines (stdout).Where (line => line.Length > 0).ToList ();
}

JsonArray ResultsForRun (int runId)
{
	if (runId <= 0)
		return new JsonArray ();
	return runResults.GetOrAdd (runId, id => AzPagedArray ($"{ORG}/{PROJECT}/_apis/test/Runs/{id}/results?api-version=7.1"));
}

string TaskLog (JsonNode record)
{
	var logId = IntString (record ["log"]? ["id"]) ?? StrN (record ["log"]? ["id"]);
	if (string.IsNullOrEmpty (logId))
		return "";
	return AzText ($"{ORG}/{PROJECT}/_apis/build/builds/{buildId}/logs/{logId}?api-version=7.1");
}

JsonNode? StageFor (JsonNode record, JsonArray records)
{
	if (Str (record ["type"]) == "Stage")
		return record;
	var byId = records
		.Where (r => r is not null && Str (r ["id"]).Length > 0)
		.Cast<JsonNode> ()
		.ToDictionary (r => Str (r ["id"]), r => r, StringComparer.Ordinal);
	var current = record;
	var seen = new HashSet<string> (StringComparer.Ordinal);
	while (current is not null) {
		if (Str (current ["type"]) == "Stage")
			return current;
		var parentId = Str (current ["parentId"]);
		if (parentId.Length == 0 || !seen.Add (parentId) || !byId.TryGetValue (parentId, out current))
			break;
	}
	var stages = records.Where (r => r is not null && Str (r ["type"]) == "Stage").Cast<JsonNode> ().ToList ();
	return stages.Count == 1 ? stages [0] : null;
}

JsonNode? JobFor (JsonNode record, JsonArray records)
{
	if (Str (record ["type"]) == "Job")
		return record;
	var byId = records
		.Where (r => r is not null && Str (r ["id"]).Length > 0)
		.Cast<JsonNode> ()
		.ToDictionary (r => Str (r ["id"]), r => r, StringComparer.Ordinal);
	var current = record;
	var seen = new HashSet<string> (StringComparer.Ordinal);
	while (current is not null) {
		if (Str (current ["type"]) == "Job")
			return current;
		var parentId = Str (current ["parentId"]);
		if (parentId.Length == 0 || !seen.Add (parentId) || !byId.TryGetValue (parentId, out current))
			break;
	}
	return null;
}

bool HasFailedTaskDescendant (JsonNode job, JsonArray records)
{
	var jobId = Str (job ["id"]);
	if (jobId.Length == 0)
		return false;
	var byId = records
		.Where (record => record is not null && Str (record ["id"]).Length > 0)
		.Cast<JsonNode> ()
		.ToDictionary (record => Str (record ["id"]), record => record, StringComparer.Ordinal);
	foreach (var task in records.Where (record => record is not null && Str (record ["type"]) == "Task" && IsFailed (record)).Cast<JsonNode> ()) {
		var parentId = Str (task ["parentId"]);
		var seen = new HashSet<string> (StringComparer.Ordinal);
		while (parentId.Length > 0 && seen.Add (parentId)) {
			if (parentId == jobId)
				return true;
			if (!byId.TryGetValue (parentId, out var parent))
				break;
			parentId = Str (parent ["parentId"]);
		}
	}
	return false;
}

StageInfo? StageForRun (JsonNode? run)
{
	if (run is null)
		return null;
	var phaseName = Str (run ["pipelineReference"]? ["phaseReference"]? ["phaseName"]);
	if (phaseName.Length == 0)
		phaseName = Str (run ["phase"]);
	if (phaseName.Length == 0)
		return null;
	foreach (var timeline in timelines) {
		var phase = timeline.Records.FirstOrDefault (r => r is not null && Str (r ["type"]) == "Phase" && Str (r ["refName"]) == phaseName);
		if (phase is null)
			continue;
		var stage = StageFor (phase, timeline.Records);
		if (stage is not null)
			return ToStageInfo (stage, timeline.IsCurrent);
	}
	return null;
}

string StageJobForRun (JsonNode? run)
{
	if (run is null)
		return "";
	var phaseName = Str (run ["pipelineReference"]? ["phaseReference"]? ["phaseName"]);
	if (phaseName.Length == 0)
		phaseName = Str (run ["phase"]);
	foreach (var timeline in timelines) {
		var phase = timeline.Records.FirstOrDefault (r => r is not null && Str (r ["type"]) == "Phase" && Str (r ["refName"]) == phaseName);
		if (phase is not null)
			return Str (phase ["name"]);
	}
	return "";
}

StageInfo ToStageInfo (JsonNode stage, bool isCurrent)
	=> new StageInfo {
		Name = Str (stage ["name"]),
		RefName = Str (stage ["refName"]),
		Attempt = Math.Max (1, ToInt (stage ["attempt"])),
		State = Str (stage ["state"]),
		Result = Str (stage ["result"]),
		IsCurrent = isCurrent,
	};

StageInfo UnknownStage ()
	=> new StageInfo {
		Name = "Unknown stage",
		RefName = "",
	};

Classification ClassifyRoot (List<string> messages, List<string> changedFiles)
{
	var text = string.Join ("\n", messages);
	var lower = text.ToLowerInvariant ();

	if (Regex.IsMatch (lower, @"ran longer than the maximum time of \d+ minutes"))
		return new Classification ("timeout-or-crash", 0.99, "Azure terminated the job at its configured maximum duration.");
	if (Regex.IsMatch (lower, @"sigsegv|sigabrt|tombstone|jni detected error|zero tests ran|did not complete"))
		return new Classification ("timeout-or-crash", 0.94, "The evidence identifies a crash or incomplete test run.");
	if (lower.Contains ("atcpu") && Regex.IsMatch (lower, @"500|internal server error|rate limit"))
		return new Classification ("transient-infrastructure", 0.99, "Azure Artifacts rejected the request because of service-side ATCPU/rate limiting.");
	if (lower.Contains ("stopped hearing from agent") || lower.Contains ("remote provider") && lower.Contains ("cancel"))
		return new Classification ("transient-infrastructure", 0.99, "Azure lost the hosted agent or its provider deprovisioned the machine.");
	if (Regex.IsMatch (lower, @"no space left|enospc|free disk space.+lower than"))
		return new Classification ("transient-infrastructure", 0.96, "The hosted agent exhausted local disk space.");
	if (Regex.IsMatch (lower, @"device offline|adb.+broken pipe|can't find service: package|cannot connect to daemon"))
		return new Classification ("transient-infrastructure", 0.90, "The emulator/ADB transport failed without product assertion evidence.");
	if (Regex.IsMatch (lower, @"no route to host|name resolution|could not resolve host|connection refused|http (429|5\d\d)|status code.+(429|5\d\d)"))
		return new Classification ("transient-infrastructure", 0.90, "An external service or network path returned a transient transport/server failure.");
	if (lower.Contains ("nu1301") && Regex.IsMatch (lower, @"service index|timed out|timeout|429|5\d\d"))
		return new Classification ("transient-infrastructure", 0.88, "NuGet failed while reaching the package service rather than resolving deterministic package inputs.");

	var changedPath = ExtractChangedPath (text, changedFiles);
	if (changedPath.Length > 0 && Regex.IsMatch (lower, @"error (cs|xa|apt)\d+|failed\s*:|assert\.|expected:"))
		return new Classification ("likely-pr-regression", 0.86, $"The deterministic error names changed file/component {changedPath}.");

	return new Classification ("unknown", 0.45, "The available task/job message is generic or lacks corroborating root-cause evidence.");
}

Classification ClassifyTest (int failedConfigs, int passedConfigs, bool retryPassed, bool failedAfterAutoRetry, bool changedFileOverlap, bool diffAvailable)
{
	if (retryPassed)
		return new Classification ("known-flaky-test", 0.95, "The exact test failed and then passed on retry without a code change.");
	if (failedAfterAutoRetry && changedFileOverlap)
		return new Classification ("likely-pr-regression", 0.88, "The test failed again in the pipeline Auto-Retry run and overlaps the PR diff.");
	if (failedAfterAutoRetry)
		return new Classification ("unknown", 0.55, "The test failed again in the pipeline Auto-Retry run; retrying again is not yet justified.");
	if (failedConfigs >= 2 && passedConfigs == 0 && changedFileOverlap)
		return new Classification ("likely-pr-regression", 0.90, "The test fails across multiple configurations and the PR changes the implicated component.");
	if (!diffAvailable)
		return new Classification ("unknown", 0.45, "The PR diff was unavailable, so absence of overlap cannot be used as flake evidence.");
	if (failedConfigs >= 1 && passedConfigs >= 1 && !changedFileOverlap)
		return new Classification ("known-flaky-test", 0.72, "The failure is isolated while sibling configurations pass; search for an exact tracker.");
	if (failedConfigs >= 1 && changedFileOverlap)
		return new Classification ("likely-pr-regression", 0.68, "The failing test overlaps the PR diff, but broader reproduction evidence is still missing.");
	return new Classification ("unknown", 0.45, "The isolated test assertion has no retry, history, or direct PR-causality evidence.");
}

RetryPlan BuildRetryPlan (List<Failure> allFailures)
{
	var currentStages = timelines [0].Records
		.Where (r => r is not null && Str (r ["type"]) == "Stage")
		.Cast<JsonNode> ()
		.Select (r => ToStageInfo (r, true))
		.Where (s => s.RefName.Length > 0)
		.ToDictionary (s => s.RefName, s => s, StringComparer.Ordinal);
	var plan = new RetryPlan {
		RequestBody = """{"state":"retry","forceRetryAllJobs":false}""",
	};
	var unattributed = allFailures.Where (failure => failure.Stage.RefName.Length == 0).ToList ();
	if (unattributed.Count > 0) {
		plan.ExcludedStages.Add (new ExcludedStage {
			RefName = "<unresolved>",
			Reason = $"{unattributed.Count} failure(s) could not be associated with a stage; retry planning fails closed.",
		});
		return plan;
	}

	foreach (var group in allFailures.Where (f => f.Stage.RefName.Length > 0).GroupBy (f => f.Stage.RefName, StringComparer.Ordinal)) {
		var stageRef = group.Key;
		if (!currentStages.TryGetValue (stageRef, out var current)) {
			plan.ExcludedStages.Add (new ExcludedStage {
				RefName = stageRef,
				Reason = "The stage is not present in the current timeline.",
			});
			continue;
		}
		if (current.Attempt > 1) {
			plan.ExcludedStages.Add (new ExcludedStage {
				RefName = stageRef,
				Reason = $"The stage already advanced to attempt {current.Attempt} ({current.State}/{current.Result}); do not recommend another automatic retry.",
			});
			continue;
		}
		if (current.State != "completed" || (current.Result != "failed" && current.Result != "canceled")) {
			plan.ExcludedStages.Add (new ExcludedStage {
				RefName = stageRef,
				Reason = $"The stage is not currently completed and failed ({current.State}/{current.Result}).",
			});
			continue;
		}
		var unsafeFailure = group.FirstOrDefault (f => !f.Retry.Safe || !IsRetryable (f.Classification, f.Confidence));
		if (unsafeFailure is not null) {
			plan.ExcludedStages.Add (new ExcludedStage {
				RefName = stageRef,
				Reason = $"Contains {unsafeFailure.Classification} failure {unsafeFailure.Fingerprint}.",
			});
			continue;
		}
		plan.StageRefNames.Add (stageRef);
		plan.Commands.Add ($"az rest --method patch --resource {RES} --url \"{ORG}/{PROJECT}/_apis/build/builds/{buildId}/stages/{stageRef}?api-version=7.1\" --headers Content-Type=application/json --body '{plan.RequestBody}'");
	}

	plan.StageRefNames.Sort (StringComparer.Ordinal);
	plan.Commands.Sort (StringComparer.Ordinal);
	plan.ExcludedStages = plan.ExcludedStages.OrderBy (s => s.RefName, StringComparer.Ordinal).ToList ();
	return plan;
}

Failure MergeFailures (List<Failure> group)
{
	var first = group [0];
	first.Evidence = group.SelectMany (f => f.Evidence).Distinct (StringComparer.Ordinal).Take (10).ToList ();
	first.IssueSearchTerms = group.SelectMany (f => f.IssueSearchTerms).Where (s => s.Length > 0).Distinct (StringComparer.Ordinal).Take (8).ToList ();
	first.Confidence = group.Max (f => f.Confidence);
	first.Gating = group.Any (f => f.Gating);
	first.Attempts = group.Select (f => f.Stage.Attempt).Distinct ().OrderBy (attempt => attempt).ToList ();
	first.Retry.Recommended = group.All (f => f.Retry.Recommended);
	first.Retry.Safe = group.All (f => f.Retry.Safe);
	if (first.Job.Length == 0)
		first.Job = group.Select (f => f.Job).FirstOrDefault (s => s.Length > 0) ?? "";
	if (first.Task.Length == 0)
		first.Task = group.Select (f => f.Task).FirstOrDefault (s => s.Length > 0) ?? "";
	return first;
}

void PrintMarkdown (AnalysisReport result)
{
	Console.WriteLine ($"# Failure analysis - build {result.BuildId}");
	if (!string.IsNullOrEmpty (result.Pr))
		Console.WriteLine ($"PR #{result.Pr}");
	Console.WriteLine ();
	if (result.Errors.Count > 0) {
		Console.WriteLine ("## Analysis errors");
		Console.WriteLine ();
		foreach (var error in result.Errors)
			Console.WriteLine ($"- {error}");
		Console.WriteLine ();
	}
	if (result.Failures.Count == 0) {
		Console.WriteLine (result.Errors.Count > 0
			? "_Failure analysis is incomplete because required data could not be loaded._"
			: "_No failed task, job, test, timeout, or incomplete-run evidence was found._");
		return;
	}

	Console.WriteLine ("## Classification");
	Console.WriteLine ();
	Console.WriteLine ("| Stage / attempt | Failure | Classification | Confidence | Stage retry |");
	Console.WriteLine ("|---|---|---|---:|---|");
	foreach (var failure in result.Failures) {
		var label = failure.Test is null ? (failure.Task.Length > 0 ? failure.Task : failure.Job) : failure.Test.Name;
		var current = failure.Stage.IsCurrent ? "current" : "previous";
		var attempts = failure.Attempts.Count > 0 ? string.Join (",", failure.Attempts) : failure.Stage.Attempt.ToString (CultureInfo.InvariantCulture);
		var retry = result.RetryPlan.StageRefNames.Contains (failure.Stage.RefName, StringComparer.Ordinal) ? "targeted" : "not selected";
		Console.WriteLine ($"| {Md (failure.Stage.Name)} / {attempts} ({current}) | `{Md (label)}` | `{failure.Classification}` | {Confidence (failure.Confidence)} | {retry} |");
	}
	Console.WriteLine ();

	PrintCategory ("Gating code failures", result.Failures.Where (f => f.Classification == "likely-pr-regression"));
	PrintCategory ("Retryable flakes / infrastructure", result.Failures.Where (f => f.Classification == "known-flaky-test" || f.Classification == "transient-infrastructure" || f.Classification == "timeout-or-crash"));
	PrintCategory ("Unknown failures", result.Failures.Where (f => f.Classification == "unknown"));

	Console.WriteLine ("## Targeted retry plan");
	Console.WriteLine ();
	if (result.RetryPlan.StageRefNames.Count > 0) {
		Console.WriteLine ($"Eligible failed stages: {string.Join (", ", result.RetryPlan.StageRefNames.Select (s => $"`{s}`"))}");
		Console.WriteLine ();
		Console.WriteLine ("Run only after explicit user confirmation:");
		Console.WriteLine ();
		Console.WriteLine ("```bash");
		foreach (var command in result.RetryPlan.Commands)
			Console.WriteLine (command);
		Console.WriteLine ("```");
	} else {
		Console.WriteLine ("No stage is currently safe and eligible for an automatic targeted-retry recommendation.");
	}
	if (result.RetryPlan.ExcludedStages.Count > 0) {
		Console.WriteLine ();
		Console.WriteLine ("Excluded:");
		foreach (var excluded in result.RetryPlan.ExcludedStages)
			Console.WriteLine ($"- `{excluded.RefName}` - {excluded.Reason}");
	}
	Console.WriteLine ();
}

void PrintCategory (string title, IEnumerable<Failure> values)
{
	var list = values.ToList ();
	if (list.Count == 0)
		return;
	Console.WriteLine ($"## {title}");
	Console.WriteLine ();
	foreach (var failure in list) {
		var label = failure.Test is null ? (failure.Task.Length > 0 ? failure.Task : failure.Job) : failure.Test.Name;
		Console.WriteLine ($"### {failure.Stage.Name} - `{label}`");
		Console.WriteLine ();
		Console.WriteLine ($"**{failure.Classification}** ({Confidence (failure.Confidence)}) - {failure.Retry.Reason}");
		foreach (var evidence in failure.Evidence)
			Console.WriteLine ($"- {evidence}");
		if (failure.Test is not null && failure.Test.Configurations.Count > 0) {
			foreach (var config in failure.Test.Configurations)
				Console.WriteLine ($"- `{config.Name}`: {string.Join (" -> ", config.Outcomes)}");
		}
		if (failure.IssueSearchTerms.Count > 0)
			Console.WriteLine ($"- issue search: {string.Join (", ", failure.IssueSearchTerms.Select (t => $"`{t}`"))}");
		Console.WriteLine ();
	}
}

List<string> SignificantLogLines (string log)
{
	if (log.Length == 0)
		return [];
	var pattern = new Regex (@"(?i)(##\[error\]|\berror\b|exception|failed|atcpu|http\s+(429|5\d\d)|rate limit|no space|stopped hearing|device offline|sigsegv|sigabrt|zero tests|timed out|timeout)");
	return Lines (log)
		.Where (line => pattern.IsMatch (line))
		.Where (line => !Regex.IsMatch (line, @"^\s*\d+\s+Error\(s\)\s*$", RegexOptions.IgnoreCase))
		.Select (line => Trunc (line.Trim (), 500))
		.Where (line => line.Length > 0)
		.Distinct (StringComparer.Ordinal)
		.Take (20)
		.ToList ();
}

List<string> IssueMessages (JsonNode record)
{
	var issues = record ["issues"] as JsonArray ?? new JsonArray ();
	return issues
		.Where (issue => issue is not null)
		.Select (issue => Str (issue? ["message"]).Trim ())
		.Where (message => message.Length > 0)
		.ToList ();
}

List<string> DistinctMeaningful (List<string> messages)
{
	var result = new List<string> ();
	var seen = new HashSet<string> (StringComparer.Ordinal);
	foreach (var message in messages) {
		var normalized = NormalizeVolatile (message);
		if (normalized.Length == 0 || !seen.Add (normalized))
			continue;
		result.Add (Trunc (message.Replace ("\r", "").Trim (), 1200));
	}
	return result;
}

bool NeedsTaskLog (List<string> messages)
{
	if (messages.Count == 0)
		return true;
	var text = string.Join ("\n", messages).ToLowerInvariant ();
	if (Regex.IsMatch (text, @"atcpu|stopped hearing from agent|maximum time of|no space left|enospc|device offline|no route to host|status code.+(429|5\d\d)|error (cs|xa|apt|nu|xagcpu)\d+"))
		return false;
	return messages.All (message => Regex.IsMatch (message, @"(?i)^(powershell|bash|process|command).*(exited|failed)|^the operation was canceled"));
}

List<string> CompactEvidence (List<string> messages, Classification classification)
{
	var text = string.Join ("\n", messages);
	if (text.Contains ("ATCPU", StringComparison.OrdinalIgnoreCase)) {
		var root = messages.FirstOrDefault (message => message.Contains ("ATCPU", StringComparison.OrdinalIgnoreCase)) ?? FirstRootLine (messages);
		return ["Azure Artifacts returned HTTP 500 because the request exceeded the ATCPU resource limit.", Trunc (FirstLine (root), 500)];
	}
	if (text.Contains ("stopped hearing from agent", StringComparison.OrdinalIgnoreCase))
		return [Trunc (messages.First (message => message.Contains ("stopped hearing from agent", StringComparison.OrdinalIgnoreCase)), 500)];
	if (classification.Category == "timeout-or-crash")
		return [Trunc (messages [0], 500)];
	return messages.Select (message => Trunc (FirstLine (message), 500)).Distinct (StringComparer.Ordinal).Take (4).ToList ();
}

string RootFingerprint (List<string> messages, string task, string job)
{
	var text = string.Join ("\n", messages).ToLowerInvariant ();
	var suffix = Slug (task.Length > 0 ? task : job);
	if (text.Contains ("atcpu"))
		return $"azure-artifacts|atcpu|http-500|{suffix}";
	if (text.Contains ("stopped hearing from agent"))
		return $"hosted-agent|disconnect|{suffix}";
	if (Regex.IsMatch (text, @"ran longer than the maximum time of \d+ minutes")) {
		var match = Regex.Match (text, @"maximum time of (?<minutes>\d+) minutes");
		return $"azure-job-timeout|{(match.Success ? match.Groups ["minutes"].Value : "unknown")}m|{suffix}";
	}
	if (text.Contains ("device offline"))
		return $"adb|device-offline|{suffix}";
	if (Regex.IsMatch (text, @"no space left|enospc"))
		return $"hosted-agent|disk-full|{suffix}";
	return $"root|{Slug (FirstRootLine (messages))}|{suffix}";
}

string TestFingerprint (string name, string error)
	=> $"test|{Slug (name)}|{Slug (FirstErrorToken (error))}";

List<string> SearchTerms (List<string> messages, string task, string job)
{
	var text = string.Join ("\n", messages);
	var terms = new List<string> ();
	if (text.Contains ("ATCPU", StringComparison.OrdinalIgnoreCase))
		terms.Add ("ATCPU");
	if (text.Contains ("stopped hearing from agent", StringComparison.OrdinalIgnoreCase))
		terms.Add ("stopped hearing from agent");
	if (text.Contains ("device offline", StringComparison.OrdinalIgnoreCase))
		terms.Add ("device offline");
	var errorCode = Regex.Match (text, @"\b(?:NU|XA|APT|CS|XAGCPU)\d+\b", RegexOptions.IgnoreCase);
	if (errorCode.Success)
		terms.Add (errorCode.Value.ToUpperInvariant ());
	if (task.Length > 0)
		terms.Add (task);
	else if (job.Length > 0)
		terms.Add (job);
	return terms.Distinct (StringComparer.OrdinalIgnoreCase).ToList ();
}

List<string> FindXref (string testName, List<string> files)
{
	var parts = testName.Split ('.');
	var className = parts.Length >= 2 ? parts [^2] : "";
	var method = parts.Length >= 1 ? parts [^1] : "";
	var result = new SortedSet<string> (StringComparer.Ordinal);
	foreach (var file in files) {
		var leaf = Path.GetFileNameWithoutExtension (file);
		if (leaf == className || leaf == method || className.Length > 0 && file.Contains (className, StringComparison.Ordinal))
			result.Add (file);
	}
	return result.ToList ();
}

bool TouchesRuntime (List<string> files)
	=> files.Any (file =>
		file.StartsWith ("src/native/", StringComparison.Ordinal) ||
		file.StartsWith ("src/Mono.Android/", StringComparison.Ordinal) ||
		file.StartsWith ("external/Java.Interop/src/", StringComparison.Ordinal) ||
		file.StartsWith ("src/monodroid/", StringComparison.Ordinal));

string ExtractChangedPath (string message, List<string> files)
{
	foreach (var line in Lines (message)) {
		if (!Regex.IsMatch (line, @"(?i)error (cs|xa|apt)\d+|failed\s*:|assert\.|expected:"))
			continue;
		var normalizedLine = line.Replace ('\\', '/');
		foreach (var file in files) {
			var leaf = Path.GetFileName (file);
			if (normalizedLine.Contains (file, StringComparison.OrdinalIgnoreCase) ||
					(leaf.Length >= 8 && normalizedLine.Contains (leaf, StringComparison.OrdinalIgnoreCase)))
				return file;
		}
	}
	return "";
}

bool IsRetryable (string category, double confidence)
	=> confidence >= 0.60 && (category == "known-flaky-test" || category == "transient-infrastructure" || category == "timeout-or-crash");

bool FailedBeforePassed (List<string> outcomes)
{
	var failed = outcomes.FindIndex (outcome => outcome == "Failed");
	if (failed < 0)
		return false;
	return outcomes.FindIndex (failed + 1, outcome => outcome == "Passed") >= 0;
}

bool IsFailed (JsonNode record)
{
	var result = Str (record ["result"]);
	return result == "failed" || result == "canceled";
}

bool IsGatingStage (JsonNode stage)
	=> Str (stage ["result"]) == "failed" || Str (stage ["result"]) == "canceled";

string ConfigName (string runName, string family)
{
	var result = runName;
	if (family.Length > 0 && result.StartsWith (family, StringComparison.Ordinal))
		result = result [family.Length..];
	result = result.TrimStart (' ', '-');
	return result.Length > 0 ? result : runName;
}

string BaseOf (string name)
{
	var result = Regex.Replace (name, @" \(Auto-Retry\)$", "");
	result = Regex.Replace (result, @" - (macOS|Windows|Linux)(-\d+)?$", "");
	result = Regex.Replace (result, @"-[A-Za-z0-9]+$", "");
	return result;
}

string FirstRootLine (List<string> messages)
{
	foreach (var message in messages)
		foreach (var line in Lines (message))
			if (!Regex.IsMatch (line, @"(?i)^(##\[error\]\s*)?(powershell|bash|process).*(exited|failed)"))
				return line;
	return messages.Count > 0 ? FirstLine (messages [0]) : "unknown";
}

string FirstErrorToken (string error)
{
	if (error.Length == 0)
		return "";
	var code = Regex.Match (error, @"\b(?:NU|XA|APT|CS|XAGCPU)\d+\b", RegexOptions.IgnoreCase);
	if (code.Success)
		return code.Value.ToUpperInvariant ();
	var status = Regex.Match (error, @"\b(?:HTTP\s*)?(?:429|5\d\d)\b", RegexOptions.IgnoreCase);
	if (status.Success)
		return status.Value.ToUpperInvariant ();
	return Trunc (FirstLine (NormalizeVolatile (error)), 120);
}

string ShortTestName (string testName)
{
	var parts = testName.Split ('.');
	return parts.Length >= 2 ? $"{parts [^2]}.{parts [^1]}" : testName;
}

string NormalizeVolatile (string value)
{
	var result = value.ToLowerInvariant ();
	result = Regex.Replace (result, @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", "<guid>");
	result = Regex.Replace (result, @"\bbuild(?:id)?[ =:#/]*\d+\b", "build <id>");
	result = Regex.Replace (result, @"azure pipelines \d+", "azure pipelines <agent>");
	result = Regex.Replace (result, @"\b\d{4}-\d{2}-\d{2}[t ][0-9:.+-]+z?\b", "<timestamp>");
	result = Regex.Replace (result, @"(?i)(?:[a-z]:\\|/)(?:[^\\/\r\n]+[\\/])+(?<file>[^\\/\r\n]+\.(?:cs|csproj|targets|props|proj|slnx|dll|java|cpp|cc|h|xml))", "<path>/${file}");
	result = Regex.Replace (result, @"\(\d+,\d+\)", "(<line>)");
	result = Regex.Replace (result, @"\s+", " ");
	return result.Trim ();
}

string Slug (string value)
{
	var normalized = NormalizeVolatile (value);
	var result = Regex.Replace (normalized, @"[^a-z0-9]+", "-").Trim ('-');
	return Trunc (result.Length > 0 ? result : "unknown", 100);
}

string Md (string value)
	=> value.Replace ("|", "\\|").Replace ("\r", " ").Replace ("\n", " ");

string Confidence (double value)
	=> value.ToString ("0.00", CultureInfo.InvariantCulture);

string FirstLine (string value)
	=> Lines (value).FirstOrDefault () ?? "";

string [] Lines (string value)
{
	var text = value.Replace ("\r\n", "\n").Trim ();
	return text.Length == 0 ? [] : text.Split ('\n');
}

JsonNode? AzJson (string url)
{
	var (code, stdout, stderr) = Run ("az", "rest", "--method", "get", "--resource", RES, "--url", url, "-o", "json");
	if (code != 0) {
		Console.Error.WriteLine ($"az error {url}\n{Trunc (stderr, 500)}");
		fetchErrors.Add ($"Azure request failed: {url}");
		return null;
	}
	try {
		return JsonNode.Parse (stdout);
	} catch (JsonException e) {
		Console.Error.WriteLine ($"invalid Azure JSON from {url}: {e.Message}");
		fetchErrors.Add ($"Azure response was not valid JSON: {url}");
		return null;
	}
}

JsonArray AzPagedArray (string baseUrl)
{
	const int pageSize = 1000;
	var result = new JsonArray ();
	for (int skip = 0; skip < 100_000; skip += pageSize) {
		var separator = baseUrl.Contains ('?') ? "&" : "?";
		var page = GetArray (AzJson ($"{baseUrl}{separator}$top={pageSize}&$skip={skip}"), "value");
		foreach (var item in page)
			if (item is not null)
				result.Add (item.DeepClone ());
		if (page.Count < pageSize)
			return result;
	}
	fetchErrors.Add ($"Azure result pagination exceeded 100,000 rows: {baseUrl}");
	return result;
}

string AzText (string url)
{
	var (code, stdout, stderr) = Run ("az", "rest", "--method", "get", "--resource", RES, "--url", url, "-o", "json");
	if (code != 0) {
		Console.Error.WriteLine ($"az log error {url}\n{Trunc (stderr, 300)}");
		return "";
	}
	if (stdout.Length >= 2 && stdout [0] == '"' && stdout [^1] == '"') {
		try {
			return JsonNode.Parse (stdout)?.GetValue<string> () ?? "";
		} catch (JsonException) {
			return stdout;
		}
	}
	return stdout;
}

static (int code, string stdout, string stderr) Run (string file, params string [] cliArgs)
{
	var psi = new ProcessStartInfo (file) {
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false,
	};
	foreach (var arg in cliArgs)
		psi.ArgumentList.Add (arg);
	using var process = Process.Start (psi);
	if (process is null)
		return (-1, "", $"failed to start {file}");
	var stdoutTask = process.StandardOutput.ReadToEndAsync ();
	var stderrTask = process.StandardError.ReadToEndAsync ();
	using var timeout = new CancellationTokenSource (TimeSpan.FromMinutes (5));
	try {
		process.WaitForExitAsync (timeout.Token).GetAwaiter ().GetResult ();
	} catch (OperationCanceledException) {
		if (!process.HasExited)
			process.Kill (entireProcessTree: true);
		Task.WhenAll (stdoutTask, stderrTask).GetAwaiter ().GetResult ();
		return (-1, stdoutTask.Result, $"{stderrTask.Result}\n{file} timed out after 5 minutes.");
	}
	Task.WhenAll (stdoutTask, stderrTask).GetAwaiter ().GetResult ();
	string stdout = stdoutTask.Result;
	string stderr = stderrTask.Result;
	return (process.ExitCode, stdout, stderr);
}

static JsonArray GetArray (JsonNode? root, string key)
	=> root? [key] as JsonArray ?? new JsonArray ();

static string Str (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.String ? "" : node.GetValue<string> ();

static string? StrN (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.String ? null : node.GetValue<string> ();

static string? IntString (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.Number ? null : node.ToJsonString ();

static int ToInt (JsonNode? node)
	=> node is null || node.GetValueKind () != JsonValueKind.Number ? 0 : node.GetValue<int> ();

static string Trunc (string value, int length)
	=> value.Length <= length ? value : value [..length];

sealed class TimelineData
{
	public string Id { get; set; } = "";
	public bool IsCurrent { get; set; }
	public JsonArray Records { get; set; } = new JsonArray ();
}

sealed class AnalysisReport
{
	public int SchemaVersion { get; set; }
	public string BuildId { get; set; } = "";
	public string? Pr { get; set; }
	public BuildSummary Build { get; set; } = new BuildSummary ();
	public List<Failure> Failures { get; set; } = [];
	public RetryPlan RetryPlan { get; set; } = new RetryPlan ();
	public List<string> Errors { get; set; } = [];
}

sealed class BuildSummary
{
	public string Status { get; set; } = "";
	public string Result { get; set; } = "";
	public string SourceBranch { get; set; } = "";
	public string SourceVersion { get; set; } = "";
}

sealed class Failure
{
	public string Fingerprint { get; set; } = "";
	public StageInfo Stage { get; set; } = new StageInfo ();
	public string Job { get; set; } = "";
	public string Task { get; set; } = "";
	public string Classification { get; set; } = "unknown";
	public double Confidence { get; set; }
	public bool Gating { get; set; }
	public List<int> Attempts { get; set; } = [];
	public List<string> Evidence { get; set; } = [];
	public List<string> IssueSearchTerms { get; set; } = [];
	public RetryRecommendation Retry { get; set; } = new RetryRecommendation ();
	public TestFailure? Test { get; set; }
}

sealed class StageInfo
{
	public string Name { get; set; } = "";
	public string RefName { get; set; } = "";
	public int Attempt { get; set; } = 1;
	public string State { get; set; } = "";
	public string Result { get; set; } = "";
	public bool IsCurrent { get; set; }
}

sealed class RetryRecommendation
{
	public bool Recommended { get; set; }
	public bool Safe { get; set; }
	public string Reason { get; set; } = "";
}

sealed class TestFailure
{
	public string Name { get; set; } = "";
	public string Assembly { get; set; } = "";
	public string Error { get; set; } = "";
	public string Stack { get; set; } = "";
	public List<TestConfiguration> Configurations { get; set; } = [];
	public List<string> ChangedFiles { get; set; } = [];
}

sealed class TestConfiguration
{
	public string Name { get; set; } = "";
	public List<string> Outcomes { get; set; } = [];
}

sealed class RetryPlan
{
	public List<string> StageRefNames { get; set; } = [];
	public List<ExcludedStage> ExcludedStages { get; set; } = [];
	public string RequestBody { get; set; } = "";
	public List<string> Commands { get; set; } = [];
}

sealed class ExcludedStage
{
	public string RefName { get; set; } = "";
	public string Reason { get; set; } = "";
}

readonly record struct Classification (string Category, double Confidence, string Reason);

[JsonSourceGenerationOptions (PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable (typeof (AnalysisReport))]
partial class ReportJsonContext : JsonSerializerContext
{
}
