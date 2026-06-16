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

Check CI status and investigate build failures for dotnet/android PRs.

**Key fact:** as of [#11578](https://github.com/dotnet/android/pull/11578), dotnet/android PR validation runs on a **single public** Azure DevOps pipeline — **`dotnet-android`** on `dev.azure.com/dnceng-public` (project `public`, definition id `333`), defined by `build-tools/automation/azure-pipelines-public.yaml`. It runs the **full test matrix for every PR** — both direct and fork. The old internal DevDiv pipeline `Xamarin.Android-PR` (`azure-pipelines.yaml`) now has `pr: none` and **no longer runs on PRs**; it only builds `main`/`release/*`/`feature/*` branches and official signed builds. On GitHub the pipeline surfaces as ~39 granular `dotnet-android (...)` checks (plus `license/cla`); querying AZDO directly adds progress, ETA, and failure detail.

## Prerequisites

| Tool | Check | Setup |
|------|-------|-------|
| `gh` | `gh --version` | https://cli.github.com/ |
| `az` + devops ext | `az version` | `az extension add --name azure-devops` then `az login` |

The pipeline lives in the **public** `dnceng-public` project, so most `build` queries (status, timeline, logs) work without auth. A few `test`-area REST calls need a token — if one returns a sign-in page or 401, tell the user to run `az login` and retry.

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

Both run the **same** `dotnet-android` pipeline with the **full test matrix** — fork status no longer changes *which* pipeline runs or *whether* tests run. It now only affects **triggering**:
- **Direct PRs:** the build starts automatically on every push.
- **Fork PRs:** the public pipeline may wait for a maintainer to approve the run (dnceng-public policy) and may need re-approval after each push. A team member can (re)trigger it by commenting `/azp run` on the PR. Until then the `dotnet-android` checks sit in a pending/expected state.

Highlight the fork status in the output so the user understands why a build may not have started yet.

#### Step 2 — Get GitHub check status

```bash
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" 2>&1 \
  | jq '[.[] | {name, state, bucket, link}]'
```

```powershell
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" | ConvertFrom-Json
```

Note which checks passed/failed/pending. Every `dotnet-android (...)` check `link` points at the **same** AZDO build; `license/cla` is a GitHub-side check.

#### Step 3 — Get the Azure DevOps build status

There is now a **single** AZDO build per PR: **`dotnet-android`** on `dev.azure.com/dnceng-public` (project `public`, definition id `333`), defined by `azure-pipelines-public.yaml`. It runs the full matrix for every PR — build (macOS/Windows/Linux) plus test stages (Linux Tests, MSBuild Tests, MSBuild Emulator Tests, Package/APK Tests, MAUI Tests).

> `Xamarin.Android-PR` on `devdiv.visualstudio.com` no longer runs on PRs (`pr: none`). If you ever see a `Xamarin.Android-PR` check, it belongs to a branch or official build, not PR validation — ignore it for PR status.

Set the org/project once:

```bash
ORG_URL=https://dev.azure.com/dnceng-public
PROJECT=public
```

All `dotnet-android (...)` check links share one build id. Extract it from any of them:
- `https://dev.azure.com/dnceng-public/{project-guid}/_build/results?buildId={id}`

```bash
BUILD_ID=$(gh pr checks $PR --repo dotnet/android --json name,link \
  --jq '[.[] | select(.name | startswith("dotnet-android")) | .link][0]' \
  | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
```

If `BUILD_ID` is empty (checks in "Expected — Waiting for status" with no build URL), the pipeline hasn't been picked up yet:
- **Fork PR:** likely awaiting maintainer approval — report "⏳ Awaiting pipeline approval — a maintainer can start it with `/azp run`."
- **Direct PR:** report "⏳ Not triggered yet — typically starts within a few minutes of a push."

Then stop (nothing to query yet).

First get the overall status including start time and definition id:

```bash
az devops invoke --area build --resource builds \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --org $ORG_URL \
  --query "{status:status, result:result, startTime:startTime, finishTime:finishTime, definitionId:definition.id, definitionName:definition.name}" \
  --output json 2>&1
```

**Compute elapsed time:** Subtract `startTime` from the current time (or from `finishTime` if the build is complete). Present as e.g. "Ran for 2h 18m" or "Running for 42 min".

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

Check `issues` arrays first — they often contain the root cause directly. The granular GitHub checks (e.g. `dotnet-android (Linux Tests Linux > Tests > MSBuild 2)`) also pinpoint which job failed without any AZDO query.

#### Step 3a — Estimate completion time (when build is in progress)

Every PR runs the same full matrix (same ~38 jobs across 8 stages), but **wall-clock duration is dominated by hosted-agent queue time** and varies widely — recent green runs range from **~50 min to ~3 h+** (same stages, very different queue waits). Treat any ETA as rough.

```bash
DEF_ID=333
az devops invoke --area build --resource builds \
  --route-parameters project=$PROJECT \
  --org $ORG_URL \
  --query-parameters "definitions=$DEF_ID&statusFilter=completed&resultFilter=succeeded&\$top=10" \
  --query "value[].{startTime:startTime, finishTime:finishTime}" \
  --output json 2>&1
```

**Compute ETA:**
1. For each recent build, calculate `duration = finishTime - startTime`
2. Compute the **median** (more robust than average); you may drop obvious outliers (very fast <60 min runs that barely queued, or >4 h stragglers)
3. `ETA = startTime + medianDuration`
4. Present as a rough window, e.g. "ETA: ~14:30 UTC (recent runs ≈50 min–3 h, median ~2 h)"

If `startTime` is null (build hasn't started yet), skip the ETA and say "Build queued, not started yet".
If the build already completed, skip the ETA and show the actual duration instead.
If it has been running longer than the median, say "overdue by ~X min — likely agent queue time, not necessarily stuck".

#### Step 3b — Check for failed tests (always do this, especially when the build is still running)

**This step is critical when the build is in progress.** Test results are published as jobs complete, so failures may already be visible before the build finishes. Surfacing these early lets the user start fixing them immediately.

> On `dnceng-public`, `az devops invoke --area test --resource runs` (list-by-build) is broken (404). Use `az rest` against the REST API with the Azure DevOps resource token instead:

```bash
ADO_RESOURCE=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id, for az rest auth
```

Get **all failed tests for the build in one call** via `ResultsByBuild`:

```bash
az rest --method get --resource "$ADO_RESOURCE" \
  --url "$ORG_URL/$PROJECT/_apis/test/ResultsByBuild?buildId=$BUILD_ID&outcomes=Failed&api-version=7.1-preview" \
  --query "value[].{test:automatedTestName, testCase:testCaseTitle, runId:runId}" \
  --output json 2>&1
```

To list the test runs for a build (e.g. for per-run pass/fail totals):

```bash
az rest --method get --resource "$ADO_RESOURCE" \
  --url "$ORG_URL/$PROJECT/_apis/test/runs?buildUri=vstfs:///Build/Build/$BUILD_ID&api-version=7.1" \
  --query "value[].{id:id, name:name, total:totalTests, passed:passedTests}" \
  --output json 2>&1
```

For the full error message / stack trace of the failed tests, list the failed results for the run (use the `runId` from `ResultsByBuild`; repeat per distinct `runId` if failures span multiple runs). Use the **list** form with `outcomes=Failed` — the single-result-by-`testId` route returns null on this org:

```bash
az devops invoke --area test --resource results \
  --route-parameters project=$PROJECT runId=$RUN_ID \
  --org $ORG_URL \
  --query-parameters "outcomes=Failed&\$top=20" \
  --query "value[].{testName:testCaseTitle, outcome:outcome, errorMessage:errorMessage, stackTrace:stackTrace}" \
  --output json 2>&1
```

> **On-device (Package/APK) test failures:** the `Package Tests` stage runs the Mono.Android instrumentation tests via stock NUnit + `dotnet test`/MTP ([#11224](https://github.com/dotnet/android/pull/11224)) and publishes results as VSTest/TRX — the queries above work unchanged (the failed `automatedTestName` is e.g. `Java.InteropTests.JnienvTest.DoNotLeakWeakReferences`). Native/JNI crashes (`UnsatisfiedLinkError`, `SIGSEGV`, `am instrument` going silent) often appear **only in logcat**: each run uploads a `logcat-<testName>.txt` (e.g. `logcat-Mono.Android.NET_Tests-Release.txt`) inside the `Test Results - APKs ...` artifact. Grab it in Phase 2 to diagnose device-test crashes.

> **Distinguish gating failures from flaky/tolerated ones — `ResultsByBuild` alone is NOT a red signal.** The **build `result`** and the **GitHub check states** are authoritative for pass/fail. The device-test lanes run with `continueOnError`, so flaky failures (commonly the network-dependent `System.NetTests.SslTest.*`, or failures only in flavor lanes like `-TrimModePartial` / `-NoAab`) get published as failed test results **without failing the build**. A failure is **gating** only when its job/stage shows `result: failed` in the timeline **and** a matching ❌ GitHub check. So: if the build `result == succeeded` and all checks are green, treat any `ResultsByBuild` failures as **non-gating/flaky** and report them as a brief note — not as red CI.

#### Step 4 — Present summary

Use this format — a single `dotnet-android` build section with its progress and ETA:

```
# CI Status for PR #NNNN — "PR Title"
🔀 **Direct PR** (branch in dotnet/android) — or 🍴 **Fork PR** (external contributor)

## GitHub Checks
| Check | Status |
|-------|--------|
| check-name | ✅ / ❌ / 🟡 |

## dotnet-android [#BuildId](link)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
⏱️ Running for **42 min** · ETA: ~15:45 UTC (recent runs ≈50 min–3 h, median ~2 h)
📊 Jobs: **18/56 completed** · 6 running · 32 waiting

| Stage > Job | Status |
|-------------|--------|
| Mac > macOS > Build | ✅ Succeeded |
| Linux Tests > Linux > Tests > MSBuild 2 | ❌ Failed |
| MSBuild Emulator Tests > macOS > Tests > MSBuild+Emulator 8 | 🟡 In Progress |

### Failures (if any)
❌ Stage > Job > Task
   Error: <first error message>

### Failed Tests
- **Gating** (job/stage `result: failed` + ❌ check) — must be fixed:
  - `Namespace.TestClass.TestMethod` — brief error message
- **Flaky / non-gating** (build still green; e.g. `SslTest.*` or flavor-lane-only) — note, don't block:
  - `System.NetTests.SslTest.HttpsShouldWork` (in `-TrimModePartial`, `-NoAab`)

## What next?
1. View full logs / stack traces for a test failure
2. Download and analyze .binlog artifacts (+ `logcat-*.txt` for device tests)
3. Retry failed stages (re-run with `/azp run` on the PR)
```

**Progress section guidelines:**
- Always show fork status (🔀 Direct PR / 🍴 Fork PR) at the top — it only affects *triggering* now (fork builds may await approval), not which pipeline runs
- There is exactly one PR build (`dotnet-android`); do NOT look for or report a `Xamarin.Android-PR` build
- Always show elapsed time when `startTime` is available
- Show ETA when the build is in progress and historical data is available. If the build has been running longer than the median, say "overdue by ~X min"
- Show job counters as "N/Total completed · M running · P waiting" (pending may be 0 — stages start in parallel)
- If the build hasn't started yet: direct PR → "⏳ Not triggered yet — typically starts within a few minutes of a push"; fork PR → "⏳ Awaiting pipeline approval — a maintainer can start it with `/azp run`"

**Pass/fail verdict — use the build `result` + GitHub checks, not the raw test-failure count:**
- **Build `result: failed` or any ❌ check** → CI is red. Surface the gating failures (the ❌ checks / `result: failed` timeline jobs and their tests).
- **Build still running with a gating job already `result: failed`** → highlight prominently so the user can start fixing immediately:
  > ⚠️ Build still in progress, but the **Package Tests** stage has already failed — you can start investigating now.
- **Build `result: succeeded` and all checks green** → report CI **green**, even if `ResultsByBuild` lists failures. Mention any such failures as a one-line flaky/non-gating note (e.g. "2 flaky `SslTest` failures in `continueOnError` lanes — not blocking").

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

- **Build in progress:** Still query failed timeline records AND `ResultsByBuild`. Report early **gating** failures (timeline jobs with `result: failed`) alongside the in-progress status; treat `ResultsByBuild`-only failures cautiously (they may be flaky/non-gating — see the gating note in Step 3b). Only offer `gh pr checks --watch` if there are no gating failures yet.
- **Checks in "Expected" state (no build URL):** The `dotnet-android` pipeline hasn't started. For a **fork PR** it's likely awaiting maintainer approval — report: "⏳ Awaiting pipeline approval — a maintainer can start it with `/azp run`." For a **direct PR** it usually starts within a few minutes of a push — report: "⏳ Not triggered yet — typically starts within a few minutes of a push."
- **A `Xamarin.Android-PR` check appears:** That pipeline no longer runs on PRs (`pr: none`); if present it belongs to a branch or official build — ignore it for PR status.
- **Sign-in page / 401 on a `test`-area `az rest` call:** Tell the user to run `az login` and retry.
- **Build not found:** Verify the PR number/build ID is correct.
- **No test runs yet:** The build may not have reached the test phase. Report what's available and note that tests haven't started.

## Tips

- The **build `result` + GitHub check states** are the source of truth for pass/fail — the test API (`ResultsByBuild`) lists failures even on green builds (flaky `continueOnError` device-test lanes)
- Focus on the **first** error chronologically — later errors often cascade
- `.binlog` has richer detail than text logs when logs show only "Build FAILED"
- `issues` in timeline records often contain the root cause without needing to download logs
- For on-device (Package/APK) test crashes, the `logcat-*.txt` artifact is usually more informative than the test error message
