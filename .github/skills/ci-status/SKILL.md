---
name: ci-status
description: >
  Check CI build status and investigate failures for dotnet/android PRs. ALWAYS use this skill when
  the user asks "check CI", "CI status", "why is CI failing", "is CI green", "why is my PR blocked",
  or anything about build status on a PR. Auto-detects the current PR from the git branch when no
  PR number is given. Covers both GitHub checks and internal Azure DevOps builds.
  DO NOT USE FOR: GitHub Actions workflow authoring, non-dotnet/android repos.
---

# CI Status

Check CI status and investigate build failures for dotnet/android PRs.

**Key fact:** dotnet/android's primary CI runs on Azure DevOps (internal). GitHub checks alone are insufficient — they may all show ✅ while the internal build is failing.

## Prerequisites

| Tool | Check | Setup |
|------|-------|-------|
| `gh` | `gh --version` | https://cli.github.com/ |
| `az` + devops ext | `az version` | `az extension add --name azure-devops` then `az login` |

If `az` is not authenticated, stop and tell the user to run `az login`.

## Workflow

### Phase 1: Quick Status (always do this first)

#### Step 1 — Resolve the PR and detect fork status

**No PR specified** — detect from current branch:

```bash
gh pr view --json number,title,url,headRefName,isCrossRepository --jq '{number,title,url,headRefName,isCrossRepository}'
```

**PR number given** — use it directly:

```bash
gh pr view $PR --repo dotnet/android --json number,title,url,headRefName,isCrossRepository --jq '{number,title,url,headRefName,isCrossRepository}'
```

If no PR exists for the current branch, tell the user and stop.

**`isCrossRepository`** tells you whether the PR is from a fork:
- `true` → **fork PR** (external contributor)
- `false` → **direct PR** (team member, branch in dotnet/android)

This matters for CI behavior:
- **Fork PRs:** `Xamarin.Android-PR` does NOT run. `dotnet-android` runs the full pipeline including tests.
- **Direct PRs:** `Xamarin.Android-PR` runs the full test suite. `dotnet-android` skips test stages (build-only) since tests run on DevDiv instead.

Highlight the fork status in the output so the user understands which checks to expect.

#### Step 2 — Get GitHub check status

```bash
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" 2>&1 \
  | jq '[.[] | {name, state, bucket, link}]'
```

```powershell
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" | ConvertFrom-Json
```

Note which checks passed/failed/pending. The `link` field contains the AZDO build URL for internal checks.

#### Step 3 — Get Azure DevOps build status (repeat for EACH build)

There are typically **two separate AZDO builds** for a dotnet/android PR. They run **independently** — neither waits for the other:
- **`dotnet-android`** on `dev.azure.com/dnceng-public` — Defined in `azure-pipelines-public.yaml` with an explicit `pr:` trigger.
  - **Fork PRs:** runs the full pipeline including build + tests (since `Xamarin.Android-PR` won't run for forks).
  - **Direct PRs:** runs **build-only** — test stages are auto-skipped because those run on DevDiv instead. This means the `dotnet-android` build will be significantly shorter for direct PRs.
- **`Xamarin.Android-PR`** on `devdiv.visualstudio.com` — full test suite, MAUI integration, compliance. Defined in `azure-pipelines.yaml` but its PR trigger is configured in the AZDO UI, not in YAML.
  - **Fork PRs:** does NOT run at all (no access to internal resources).
  - **Direct PRs:** runs the full test matrix. May take a few minutes to start after a push.

Use the **pipeline definition name** (from the `definitionName` field) as the label in output — do NOT label them "Public" or "Internal".

When a check shows **"Expected — Waiting for status to be reported"** on GitHub (typically `Xamarin.Android-PR`):
- **For direct PRs:** the pipeline hasn't been triggered yet — this is normal, it's not waiting for the other build, just for AZDO to pick it up. Report it as: "⏳ Not triggered yet — typically starts within a few minutes of a push."
- **For fork PRs:** `Xamarin.Android-PR` will NOT run. Report: "⏳ Will not run — fork PRs don't trigger the internal pipeline."

Extract AZDO build URLs from the check `link` fields. Parse `{orgUrl}`, `{project}`, and `{buildId}` from patterns:
- `https://dev.azure.com/{org}/{project}/_build/results?buildId={id}`
- `https://{org}.visualstudio.com/{project}/_build/results?buildId={id}`

**Run Steps 3, 3a, and 3b for each AZDO build independently.** The builds have different pipelines, different job counts, and different typical durations — each gets its own progress and ETA.

For each build, first get the overall status including start time and definition ID:

```bash
az devops invoke --area build --resource builds \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --org $ORG_URL \
  --query "{status:status, result:result, startTime:startTime, finishTime:finishTime, definitionId:definition.id, definitionName:definition.name}" \
  --output json 2>&1
```

**Compute elapsed time:** Subtract `startTime` from the current time (or from `finishTime` if the build is complete). Present as e.g. "Ran for 42 min" or "Running for 42 min".

Then fetch the build timeline for **all jobs** (to get progress counts) and **any failures so far** — even when the build is still in progress:

```bash
az devops invoke --area build --resource timeline \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --org $ORG_URL \
  --query "records[?type=='Job'] | [].{name:name, state:state, result:result}" \
  --output json 2>&1
```

**Compute job progress counters** from the timeline response:
- Count jobs where `state == 'completed'` → **finished**
- Count jobs where `state == 'inProgress'` → **running**
- Count jobs where `state == 'pending'` → **waiting**
- Total = finished + running + waiting

Then fetch failures:

```bash
az devops invoke --area build --resource timeline \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --org $ORG_URL \
  --query "records[?result=='failed'] | [].{name:name, type:type, result:result, issues:issues, errorCount:errorCount, log:log}" \
  --output json 2>&1
```

Check `issues` arrays first — they often contain the root cause directly.

#### Step 3a — Estimate completion time per build (when build is in progress)

Use the `definitionId` from the build to query recent successful builds of the **same pipeline definition** and compute the median duration. **Do this separately for each build** — the pipelines have very different durations.

**Important:** The `dotnet-android` pipeline duration varies significantly based on whether the PR is from a fork:
- **Direct PRs:** `dotnet-android` runs build-only (tests skipped) — typically much shorter (~1h 45min)
- **Fork PRs:** `dotnet-android` runs the full pipeline with tests — typically much longer

To get accurate ETAs, filter historical builds to match the current PR type. You can approximate this by looking at the **job count** of the current build vs historical builds — build-only runs have ~3 jobs while full runs have many more. Alternatively, compare the historical durations and pick the ones that are similar in magnitude to what you'd expect for the current build type.

```bash
az devops invoke --area build --resource builds \
  --route-parameters project=$PROJECT \
  --org $ORG_URL \
  --query-parameters "definitions=$DEF_ID&statusFilter=completed&resultFilter=succeeded&\$top=10" \
  --query "value[].{startTime:startTime, finishTime:finishTime}" \
  --output json 2>&1
```

**Compute ETA:**
1. For each recent build, calculate `duration = finishTime - startTime`
2. Filter to builds with similar duration profile (short ~1-2h for build-only, long ~3h+ for full runs) matching the current PR type
3. Compute the **median** duration of the filtered set (more robust than average against outliers)
4. `ETA = startTime + medianDuration`
5. Present as: "ETA: ~14:30 UTC (typical for direct PRs: ~1h 45min)"

If `startTime` is null (build hasn't started yet), skip the ETA and say "Build queued, not started yet".
If the build already completed, skip the ETA and show the actual duration instead.

#### Step 3b — Check for failed tests (always do this, especially when the build is still running)

**This step is critical** and must run for every build — even when no timeline records are marked `failed`, and even when the build's status is `succeeded` or `succeededWithIssues`. Many failing test runs live under timeline jobs that are themselves `succeededWithIssues` (the wrapper script swallows a non-zero exit), and crashed instrumentations show up as `unanalyzedTests > 0` rather than `Failed`. The previous narrow filter missed both cases.

**Always enumerate ALL test runs for the build, not just the ones whose containing job is `failed`.**

> ⚠️ Do **not** use `az devops invoke --area test --resource runs` — on `devdiv` it incorrectly routes to `/test/Runs/Statistics` and returns 404. Use the REST API directly with an AAD bearer token (works on both `devdiv` and `dnceng-public`):

```bash
# 499b84ac-1321-427f-aa17-267ca6975798 is the well-known AAD resource ID for Azure DevOps.
TOKEN=$(az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv)

curl -sL -u ":$TOKEN" \
  "$ORG_URL/$PROJECT/_apis/test/runs?buildUri=vstfs:///Build/Build/$BUILD_ID&api-version=7.0" \
  | jq '[.value[] | {id, name, totalTests, passedTests, unanalyzedTests, state}
                    | select((.totalTests // 0) > (.passedTests // 0))]'
```

The `select` filter is **`totalTests > passedTests`** on purpose: it catches failed tests, not-executed tests, inconclusive tests, and crashed/unanalyzed tests in one go. Do NOT filter on `runStatistics[?outcome=='Failed']` — that misses `Unanalyzed` (crashes) and several other buckets.

For each failing run, fetch the individual results — first `outcomes=Failed`, then `outcomes=NotExecuted` if the run had `unanalyzedTests > 0`:

```bash
curl -sL -u ":$TOKEN" \
  "$ORG_URL/$PROJECT/_apis/test/runs/$RUN_ID/results?outcomes=Failed&\$top=50&api-version=7.0" \
  | jq '[.value[] | {testName: .testCaseTitle, outcome, error: .errorMessage}]'

# Only run this if the run has unanalyzedTests > 0 (e.g. a crashed instrumentation run).
curl -sL -u ":$TOKEN" \
  "$ORG_URL/$PROJECT/_apis/test/runs/$RUN_ID/results?outcomes=NotExecuted&\$top=50&api-version=7.0" \
  | jq '[.value[] | {testName: .testCaseTitle, outcome, error: .errorMessage}]'
```

If `errorMessage` is truncated or missing, fetch the full result (errorMessage + stackTrace) for a specific test:

```bash
curl -sL -u ":$TOKEN" \
  "$ORG_URL/$PROJECT/_apis/test/runs/$RUN_ID/results/$TEST_ID?api-version=7.0" \
  | jq '{name: .testCaseTitle, error: .errorMessage, stack: .stackTrace}'
```

**Heuristic for diagnosing a crashed instrumentation:** if a run has `totalTests=1, passedTests=0, unanalyzedTests=1` and the lone result's title is "Possible Crash / …", the test APK process crashed before NUnit/xUnit could write results. Download the matching `Test Results - APKs …` build artifact and inspect `logcat-*.txt` for `Fatal signal` / `FATAL` / tombstone.

#### Step 4 — Present summary

Use this format — **one section per AZDO build**, each with its own progress and ETA:

```
# CI Status for PR #NNNN — "PR Title"
🔀 **Direct PR** (branch in dotnet/android) — or 🍴 **Fork PR** (external contributor)

## GitHub Checks
| Check | Status |
|-------|--------|
| check-name | ✅ / ❌ / 🟡 |

## dotnet-android [#BuildId](link)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
ℹ️ Build-only (tests run on Xamarin.Android-PR for direct PRs) — or ℹ️ Full pipeline with tests (fork PR)
⏱️ Running for **12 min** · ETA: ~15:15 UTC (typical for direct PRs: ~1h 45min)
📊 Jobs: **0/3 completed** · 1 running · 2 waiting

| Job | Status |
|-----|--------|
| macOS > Build | 🟡 In Progress |
| Linux > Build | ⏳ Waiting |
| Windows > Build & Smoke Test | ⏳ Waiting |

## Xamarin.Android-PR [#BuildId](link)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
— or for fork PRs: ⏳ **Will not run** — fork PRs don't trigger this pipeline
⏱️ Running for **42 min** · ETA: ~15:45 UTC (typical: ~2h 30min)
📊 Jobs: **18/56 completed** · 6 running · 32 waiting

### Failures (if any)
❌ Stage > Job > Task
   Error: <first error message>

### Failed Tests (if any — even while build is still running)
| Test Run | Failed | Total |
|----------|--------|-------|
| run-name | N | M |

**Failed test names:**
- `Namespace.TestClass.TestMethod` — brief error message
- ...

## What next?
1. View full logs / stack traces for a test failure
2. Download and analyze .binlog artifacts
3. Retry failed stages
```

**Progress section guidelines:**
- Always show fork status (🔀 Direct PR / 🍴 Fork PR) at the top — it determines which builds run and their expected durations
- For `dotnet-android`, note whether it's build-only (direct PR) or full pipeline (fork PR)
- For `Xamarin.Android-PR` on fork PRs, don't try to query it — just report "Will not run"
- Always show elapsed time when `startTime` is available
- Show ETA when the build is in progress and historical data is available. If the build has been running longer than the median, say "overdue by ~X min"
- Show job counters as "N/Total completed · M running · P waiting"
- If the build hasn't started yet, show "⏳ Not triggered yet — typically starts within a few minutes of a push"
- If a check is in "Expected" state with no build URL on a direct PR, the AZDO pipeline hasn't picked it up yet — this is normal and not gated on other builds

**If the build is still running but tests have already failed**, highlight these prominently so the user can start fixing them immediately. Use a note like:

> ⚠️ Build still in progress, but **N tests have already failed** — you can start investigating these now.

**If no failures found anywhere**, report CI as green and stop. "No failures found anywhere" means **all three** of:
1. No timeline records with `result == 'failed'`.
2. For every test run returned by Step 3b, `passedTests == totalTests` (no Failed, NotExecuted, Inconclusive, or unanalyzed buckets).
3. All required GitHub checks are `SUCCESS`.

If a GitHub check is RED but the timeline shows no failures, **do not** report green — go back to Step 3b and enumerate test runs, since the failure is almost certainly hiding in a `succeededWithIssues` job's test results.

### Phase 2: Deep Investigation (only if user requests)

Only proceed here if the user asks to investigate a specific failure, view logs, or analyze binlogs.

#### Fetch logs

Get the `log.id` from failed timeline records, then:

```bash
az devops invoke --area build --resource logs \
  --route-parameters project=$PROJECT buildId=$BUILD_ID logId=$LOG_ID \
  --org $ORG_URL --project $PROJECT \
  --out-file "/tmp/azdo-log-$LOG_ID.log" 2>&1
tail -40 "/tmp/azdo-log-$LOG_ID.log"
```

```powershell
$logFile = Join-Path $env:TEMP "azdo-log-$LOG_ID.log"
az devops invoke --area build --resource logs `
  --route-parameters project=$PROJECT buildId=$BUILD_ID logId=$LOG_ID `
  --org $ORG_URL --project $PROJECT `
  --out-file $logFile
Get-Content $logFile -Tail 40
```

#### Analyze .binlog artifacts

See [references/binlog-analysis.md](references/binlog-analysis.md) for binlog download and analysis commands.

#### Categorize failures

See [references/error-patterns.md](references/error-patterns.md) for dotnet/android-specific error patterns and categorization.

## Error Handling

- **Build in progress:** Still query for failed timeline records AND test runs. Report any early failures alongside the in-progress status. Only offer `gh pr checks --watch` if there are no failures yet.
- **Check in "Expected" state (no build URL):** The AZDO pipeline hasn't been triggered yet. This is normal — the two pipelines (`dotnet-android` and `Xamarin.Android-PR`) run independently, not sequentially. Report: "⏳ Not triggered yet — typically starts within a few minutes of a push." Do NOT say it's waiting for the other build.
- **Auth expired:** Tell user to run `az login` and retry.
- **Build not found:** Verify the PR number/build ID is correct.
- **No test runs yet:** The build may not have reached the test phase. Report what's available and note that tests haven't started.

## Tips

- Focus on the **first** error chronologically — later errors often cascade
- `.binlog` has richer detail than text logs when logs show only "Build FAILED"
- `issues` in timeline records often contain the root cause without needing to download logs
