---
name: ci-status
description: >
  Check CI build status and investigate failures for dotnet/android PRs. ALWAYS use this skill when
  the user asks "check CI", "CI status", "why is CI failing", "is CI green", "why is my PR blocked",
  or anything about build status on a PR. Auto-detects the current PR from the git branch when no
  PR number is given. Covers GitHub checks and the public Azure DevOps pipeline (dnceng-public).
  DO NOT USE FOR: GitHub Actions workflow authoring, non-dotnet/android repos.
---

# CI Status

Triage the single public `dotnet-android` Azure DevOps build for a `dotnet/android` PR. Always gather status; when the build is red, always classify every independent root failure before recommending an action.

The pipeline is definition **333** on `dev.azure.com/dnceng-public` (project `public`). It surfaces as ~39 `dotnet-android (...)` GitHub checks plus `license/cla`.

## Rules that apply throughout

- Judge green/red by Azure build `result` plus GitHub check states, never by failed-test count alone. Device lanes use `continueOnError`, so failed tests can exist in a green build.
- A fork PR can await `/azp run` approval; direct PRs auto-start. Fork status changes triggering, not the pipeline being analyzed.
- `build`-area `az devops invoke` works unauthenticated. `az rest`, logs, artifacts, test APIs, and stage retries need `az login`.
- Root-cause context outranks error-code prefixes. `NU1301` with Azure Artifacts HTTP 500/ATCPU is infrastructure, not automatically a package regression.
- Never mutate Azure or GitHub without explicit user confirmation. Analysis, issue search, and command generation are read-only.
- Prefer targeted retry of selected failed stages. `/azp run` is an explicit last resort, not the default retry action.

```bash
ORG=https://dev.azure.com/dnceng-public
PROJECT=public
RES=499b84ac-1321-427f-aa17-267ca6975798
```

## Phase 1 — status and evidence

### 1. Resolve the PR and build

Drop `--repo`/`$PR` to auto-detect the PR from the current branch:

```bash
gh pr view $PR --repo dotnet/android --json number,title,isCrossRepository,headRefOid
gh pr checks $PR --repo dotnet/android --json name,state,link
BUILD_ID=$(gh pr checks $PR --repo dotnet/android --json name,link \
  --jq '[.[]|select(.name|startswith("dotnet-android")).link][0]' | grep -oE 'buildId=[0-9]+' | cut -d= -f2 | head -1)
```

If `BUILD_ID` is empty, report “awaiting `/azp run` approval” for a fork or “not triggered yet” for a direct PR, then stop.

### 2. Fetch build and timeline status

```bash
az devops invoke --area build --resource builds --org "$ORG" \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query '{status:status,result:result,sourceBranch:sourceBranch,sourceVersion:sourceVersion,startTime:startTime,finishTime:finishTime}' -o json

az devops invoke --area build --resource timeline --org "$ORG" \
  --route-parameters project=$PROJECT buildId=$BUILD_ID --query 'records[]' -o json > /tmp/tl.json
```

List jobs and failed records:

```bash
jq -r '.[]|select(.type=="Job")|[(.result // .state),.name]|@tsv' /tmp/tl.json | sort
jq -r '.[]|select(.result=="failed" or .result=="canceled")|[.type,.name,.refName,((.issues//[])|map(.message)|join(" | "))]|@tsv' /tmp/tl.json
```

Time every job and spell out the status:

- `✅ Passed` · `❌ Failed` · `⏹️ Canceled`
- `⏱️ Timed out (N-min cap)` when the job issue says it exceeded its maximum time
- `🟡 Running` · `⏳ Queued`

```bash
jq -r '
  def secs: sub("\\.[0-9]+";"")|fromdateiso8601;
  def hms: if .==null then "—" else (./1|floor) as $s|($s/3600|floor) as $h|(($s%3600)/60|floor) as $m|($s%60) as $x|
    if $h>0 then "\($h)h\(if $m<10 then "0" else "" end)\($m)m" elif $m>0 then "\($m)m\(if $x<10 then "0" else "" end)\($x)s" else "\($x)s" end end;
  def reason:
    ((.issues//[])|map(.message)|join("  ")) as $msg
    | if .result=="succeeded" then "✅ Passed"
      elif .result=="canceled" or .result=="failed" then
        (if ($msg|test("maximum time of")) then ($msg|capture("maximum time of (?<m>[0-9]+) minutes")|"⏱️ Timed out (\(.m)-min cap)")
         elif .result=="canceled" then "⏹️ Canceled" else "❌ Failed" end)
      elif .state=="inProgress" then "🟡 Running"
      elif .state=="pending" then "⏳ Queued"
      else "· \(.result // .state)" end;
  (now) as $now | ([.[]|select(.startTime!=null)|(.startTime|secs)]|min) as $t0
  | .[]|select(.type=="Job")
  | [reason,.name,
      (if .startTime then ((.startTime|secs)-$t0|hms) else "—" end),
      (if .startTime then (((.finishTime|if .==null then $now else secs end))-(.startTime|secs)|hms) else "—" end),
      (if .finishTime then (($now-(.finishTime|secs))|hms)+" ago" elif .state=="inProgress" then "running" else "—" end)]
  | @tsv' /tmp/tl.json | sort -t$'\t' -k2 | column -t -s$'\t'
```

For ETA, failed-test details, run→job mapping, previous attempts, and crash logcat, use [references/azdo-queries.md](references/azdo-queries.md).

### 3. Analyze and classify every red build

Run the helper in JSON mode; this is the structured source for the report:

```bash
az account show >/dev/null || {
  echo "Azure login required. Run: az login --tenant <tenant-associated-with-dnceng-public>"
}

if ! dotnet run .github/skills/ci-status/scripts/ci_failures.cs -- \
  --build-id $BUILD_ID --pr $PR --format json > /tmp/ci-analysis.json; then
  if test -s /tmp/ci-analysis.json; then
    jq '{errors,build,failures}' /tmp/ci-analysis.json
  else
    echo "The analyzer failed before producing JSON (SDK/compile/process failure)."
  fi
  echo "CI analysis is incomplete; resolve the data/auth errors before classifying or retrying."
fi
```

If the command fails, stop the CI verdict/retry flow after reporting the error; do not infer “no failures”.

The analyzer:

- follows `previousAttempts` so original failures remain visible while targeted retries run;
- associates tasks/jobs/tests with the ancestor stage and stable stage `refName`;
- reads failed task logs when timeline issues are generic;
- builds failed-test cross-configuration/retry evidence;
- emits normalized fingerprints, classification/confidence, issue-search terms, and a stage-safe retry plan;
- never PATCHes Azure or mutates GitHub.

Inspect the result:

```bash
jq '{errors,failures:[.failures[]|{stage:.stage.name,stageRef:.stage.refName,attempt:.stage.attempt,isCurrent:.stage.isCurrent,job,task,test:.test.name,classification,confidence,evidence,issueSearchTerms,retry}],retryPlan}' /tmp/ci-analysis.json
```

Use the five categories from [references/error-patterns.md](references/error-patterns.md):

- `likely-pr-regression`
- `known-flaky-test`
- `transient-infrastructure`
- `timeout-or-crash`
- `unknown`

Confidence:

- `0.80`–`1.00`: high; state directly.
- `0.60`–`0.79`: medium; say “likely” and name missing evidence.
- below `0.60`: `unknown`; diagnose before retrying.

If a stage contains any regression/unknown root, the analyzer excludes that mixed stage from the automatic retry plan.

### 4. Search for an existing flaky-CI issue

For retryable classifications, use each failure's `fingerprint` and `issueSearchTerms` with [references/flaky-issues.md](references/flaky-issues.md).

Run separate exact searches, merge/deduplicate candidates, and score them:

- 75+: recommend commenting on the most recently updated exact tracker.
- 55–74: show candidates and require a human choice.
- below 55: recommend a new issue.

An inventory such as #12704 is supporting history, not automatically the best exact tracker. Do not comment/create until the user confirms.

## Phase 2 — verdict and report

Judge the build result first, then explain causality:

- Azure `result: succeeded` and all checks green → **green**, even if non-gating test results contain flakes.
- Azure `result: failed` or any red check → **red**.
- A red build can still be “red only because of high-confidence flakes/infrastructure”; do not call it green, but separate it from a PR regression.

Report:

```text
# CI Status — PR #NNNN "<title>"
🔀 Direct PR   (or 🍴 Fork PR — may await `/azp run` approval)

## dotnet-android [#<buildId>](<link>)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
📊 Jobs: <done>/<total> done · <running> running · <waiting> waiting

| Stage > Job | Status | Wait | Run | Finished |
|---|---|---:|---:|---|
| ... |

## Gating code failures
<only likely-pr-regression roots; omit when none>

## Retryable flakes / infrastructure
| Stage | Classification | Confidence | Evidence | Tracker | Retry |
|---|---|---:|---|---|---|

## Unknown failures
<diagnose before retry; omit when none>

## Verdict
❌ Red due to a likely PR regression.
or
❌ Red only because of N high-confidence flakes/infrastructure failures; no PR-regression evidence found.
or
❌ Red with unresolved unknown/mixed failures.

## What next?
1. Fix/investigate gating regressions and unknowns.
2. Retry failed jobs in the analyzer-selected stages (only after confirmation).
3. Update the exact flaky issue, or open a new issue if no strong match (only after confirmation).
4. Use `/azp run` only if targeted retry is unavailable and a full run is explicitly approved.
```

## Confirmation-gated targeted retry

When `retryPlan.stageRefNames` is non-empty:

1. Show the user the stage display names, refNames, classifications, and generated commands.
2. Warn that failed dependent jobs can rerun.
3. Ask for explicit confirmation.
4. Follow the identity/state safeguards and PATCH procedure in [references/azdo-queries.md](references/azdo-queries.md).
5. Refresh the timeline and verify the stage attempt incremented or entered pending/in-progress.

Do not retry:

- `likely-pr-regression` or `unknown` stages;
- mixed stages unless the user explicitly selects them;
- stages that already advanced to another attempt;
- a different build/PR than the one analyzed.

If PATCH is unavailable, give the command to an authorized maintainer or recommend Azure DevOps UI **Retry failed jobs**. Never silently fall back to `/azp run`.

## Deep-dive references

- Failure categories, confidence, and conflict rules → [references/error-patterns.md](references/error-patterns.md)
- Azure queries, previous attempts, targeted retry, auth/permissions, crash logcat → [references/azdo-queries.md](references/azdo-queries.md)
- Flaky issue search, scoring, comment/new-issue templates → [references/flaky-issues.md](references/flaky-issues.md)
- `.binlog` download and analysis → [references/binlog-analysis.md](references/binlog-analysis.md)
