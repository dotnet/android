#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
SCRIPT="$ROOT/.github/skills/ci-status/scripts/ci_failures.cs"
FIXTURES="$ROOT/.github/skills/ci-status/tests/fixtures"

analyze()
{
	dotnet run "$SCRIPT" -- --input-dir "$FIXTURES/$1" --format json
}

pr12670="$(analyze pr-12670-infrastructure)"
jq -e '
	.schemaVersion == 1 and
	([.failures[].classification] | all(. == "transient-infrastructure")) and
	([.retryPlan.stageRefNames[]] | sort) == ["java_interop_tests","msbuilddevice_tests","win_build_test"] and
	([.failures[].fingerprint] | any(contains("atcpu"))) and
	([.failures[].fingerprint] | any(contains("disconnect")))
' <<<"$pr12670" >/dev/null

known_flake="$(analyze known-flaky-test)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "known-flaky-test" and
	.failures[0].confidence >= 0.9 and
	.failures[0].retry.safe == true and
	(.failures[0].test.configurations | any(.outcomes == ["Failed","Passed"]))
' <<<"$known_flake" >/dev/null

regression="$(analyze likely-pr-regression)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "likely-pr-regression" and
	.failures[0].retry.safe == false and
	(.failures[0].evidence | any(contains("Auto-Retry")))
' <<<"$regression" >/dev/null
jq -e '(.retryPlan.stageRefNames | length) == 0' <<<"$regression" >/dev/null

timeout="$(analyze timeout)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "timeout-or-crash" and
	.failures[0].confidence >= 0.9 and
	.retryPlan.stageRefNames == ["timeout_stage"]
' <<<"$timeout" >/dev/null

native_crash="$(analyze native-crash)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "timeout-or-crash" and
	.failures[0].fingerprint == "incomplete-run|mono-android-net-tests-debug"
' <<<"$native_crash" >/dev/null

unknown="$(analyze unknown)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "unknown" and
	.failures[0].confidence < 0.6 and
	(.retryPlan.stageRefNames | length) == 0
' <<<"$unknown" >/dev/null

mixed="$(analyze mixed-stage)"
jq -e '
	([.failures[].classification] | sort) == ["likely-pr-regression","transient-infrastructure"] and
	(.retryPlan.stageRefNames | length) == 0
' <<<"$mixed" >/dev/null
jq -e '.retryPlan.excludedStages[0].reason | contains("likely-pr-regression")' <<<"$mixed" >/dev/null
jq -e '.failures[] | select(.classification == "likely-pr-regression") | .fingerprint | contains("foo-cs")' <<<"$mixed" >/dev/null

previous="$(analyze previous-attempt)"
jq -e '
	(.failures | length == 1) and
	.failures[0].classification == "transient-infrastructure" and
	.failures[0].stage.attempt == 1 and
	(.retryPlan.stageRefNames | length) == 0
' <<<"$previous" >/dev/null
jq -e '.retryPlan.excludedStages[0].reason | contains("attempt 2")' <<<"$previous" >/dev/null

unresolved="$(analyze unresolved-stage)"
jq -e '
	([.failures[].stage.refName] | any(. == "")) and
	(.retryPlan.stageRefNames | length) == 0 and
	.retryPlan.excludedStages[0].refName == "<unresolved>"
' <<<"$unresolved" >/dev/null

repeated="$(analyze repeated-attempt)"
jq -e '
	(.failures | length) == 1 and
	.failures[0].attempts == [1,2] and
	(.retryPlan.stageRefNames | length) == 0
' <<<"$repeated" >/dev/null
jq -e '.retryPlan.excludedStages[0].reason | contains("attempt 2")' <<<"$repeated" >/dev/null

set +e
diff_unavailable="$(dotnet run "$SCRIPT" -- --input-dir "$FIXTURES/diff-unavailable" --format json)"
diff_exit=$?
set -e
test "$diff_exit" -ne 0
jq -e '
	.failures[0].classification == "unknown" and
	(.retryPlan.stageRefNames | length) == 0 and
	(.errors | any(contains("diff")))
' <<<"$diff_unavailable" >/dev/null

markdown="$(dotnet run "$SCRIPT" -- --input-dir "$FIXTURES/pr-12670-infrastructure" --format markdown)"
grep -q '## Retryable flakes / infrastructure' <<<"$markdown"
grep -q '`java_interop_tests`' <<<"$markdown"
grep -q 'forceRetryAllJobs' <<<"$markdown"
grep -q '0.99' <<<"$markdown"

previous_markdown="$(dotnet run "$SCRIPT" -- --input-dir "$FIXTURES/previous-attempt" --format markdown)"
grep -q '1 (previous)' <<<"$previous_markdown"

set +e
missing="$(dotnet run "$SCRIPT" -- --input-dir "$ROOT/.github/skills/ci-status/tests" --format json)"
missing_exit=$?
set -e
test "$missing_exit" -ne 0
jq -e '(.errors | length) >= 2' <<<"$missing" >/dev/null

echo "All ci-status fixtures passed."
