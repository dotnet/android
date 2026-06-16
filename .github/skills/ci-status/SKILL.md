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

PR validation runs on **one public** Azure DevOps pipeline: **`dotnet-android`** on `dev.azure.com/dnceng-public` (project `public`, definition id `333`, `build-tools/automation/azure-pipelines-public.yaml`), full test matrix for **every** PR. On GitHub it shows as ~39 `dotnet-android (...)` checks plus `license/cla`, all backed by **one** build.

Repo-specific things you must know (everything else is standard `gh`/`az`):

- **`Xamarin.Android-PR`** (devdiv) has `pr: none` — it does NOT run on PRs. If you see that check it's a branch/official build; ignore it for PR status.
- **Fork status only changes triggering, not which pipeline runs.** Fork builds may wait for a maintainer to approve the run (and re-approve per push) via an `/azp run` comment; direct builds auto-start on push.
- The **`test` area of `az devops invoke` is broken on dnceng-public (404)** — get test results via `az rest` (below). The `build` area works, unauthenticated. `az rest` / log+artifact downloads need `az login` (else a sign-in page / 401).
- **The build `result` + GitHub check states are the source of truth — not the test API.** Device-test lanes run with `continueOnError`, so flaky failures (notably `System.NetTests.SslTest.*`, or failures only in flavor lanes like `-TrimModePartial`/`-NoAab`) appear as failed tests on otherwise-green builds.

## Phase 1 — Status (always)

```bash
ORG=https://dev.azure.com/dnceng-public; PROJECT=public

# Resolve the PR (drop --repo/$PR to auto-detect from the current branch); stop if none:
gh pr view $PR --repo dotnet/android --json number,title,isCrossRepository

# GitHub checks (every dotnet-android link points at the same build):
gh pr checks $PR --repo dotnet/android --json name,state,link

# Shared build id:
BUILD_ID=$(gh pr checks $PR --repo dotnet/android --json name,link \
  --jq '[.[]|select(.name|startswith("dotnet-android")).link][0]' | grep -oE 'buildId=[0-9]+' | cut -d= -f2 | head -1)
```

Empty `BUILD_ID` (checks "Expected", no build URL) = pipeline not started: fork PR → "awaiting `/azp run` approval"; direct PR → "not triggered yet (starts within minutes of a push)". Report and stop.

Build status, then timeline (job progress + failures so far — both valid mid-build):

```bash
az devops invoke --area build --resource builds --org $ORG \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query "{status:status, result:result, startTime:startTime, finishTime:finishTime}" -o json

az devops invoke --area build --resource timeline --org $ORG \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query "records[?type=='Job'].{name:name, state:state, result:result}" -o json
```

Job `state` is `completed`/`inProgress`/`pending` (pending is often 0 — stages start in parallel). `records[?result=='failed']` gives failing stages/jobs/tasks; their `issues[]` usually carry the root cause, and the granular check names (e.g. `dotnet-android (Linux Tests Linux > Tests > MSBuild 2)`) already pinpoint the lane.

Failed tests — `az devops invoke --area test` 404s here, so use `az rest`:

```bash
RES=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/test/ResultsByBuild?buildId=$BUILD_ID&outcomes=Failed&api-version=7.1-preview" \
  --query "value[].{test:automatedTestName, runId:runId}" -o json
```

`ResultsByBuild` returns every failed test across all runs (only `Failed`/`Aborted` are queryable). For per-test error/stack, the ETA query, or the test-runs list, see [references/azdo-queries.md](references/azdo-queries.md).

### Verdict — judge by build `result` + checks, NOT the failed-test count

- **`result: failed` or any ❌ check** → red. Surface the gating failures (the ❌ checks / `result: failed` timeline jobs and their tests). If the build is still running with a job already failed, lead with that so the user can start fixing now.
- **`result: succeeded` and all checks green** → green, even if `ResultsByBuild` lists failures — those are flaky/non-gating `continueOnError` lanes. Mention them in one line; don't block.

### Report — use this format (omit sections that don't apply)

```
# CI Status — PR #NNNN "<title>"
🔀 Direct PR   (or 🍴 Fork PR — may await `/azp run` approval)

## dotnet-android [#<buildId>](<link>)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
⏱️ <elapsed>  ·  ETA ~HH:MM UTC (rough — recent runs ≈50 min–3 h)   ← only while in progress
📊 Jobs: <done>/<total> done · <running> running · <waiting> waiting

| Stage > Job | Status |
|-------------|--------|
| Mac > macOS > Build | ✅ |
| Package Tests > macOS > Tests > APKs 2 | ❌ |

### Failures                ← if any
❌ <Stage> > <Job> — <first error from issues[]>

### Failed tests            ← if any
- **Gating** (must fix): `Ns.Class.Test` — <error>
- **Flaky / non-gating** (build still green; e.g. `SslTest.*`, `-TrimModePartial`/`-NoAab` lanes): `...`

## Verdict: ✅ green  /  ❌ red — <one-line reason>

## What next?
1. Logs / stack trace for a failure
2. `.binlog` (+ `logcat-*.txt` for device-test crashes)
3. Re-run a flaky/failed stage with `/azp run`
```

Notes: every `dotnet-android (...)` check is one job, so the Stage > Job table *is* the check list (the only non-`dotnet-android` check is `license/cla`). For `Package Tests` (on-device) crashes — `UnsatisfiedLinkError`, `SIGSEGV`, a silent `am instrument` — the cause is usually in `logcat-<testName>.txt` inside the `Test Results - APKs ...` artifact, not the test message.

## Phase 2 — Deep dive (only if asked)

- Logs, per-test error/stack, ETA, test-runs list → [references/azdo-queries.md](references/azdo-queries.md)
- `.binlog` download + analysis → [references/binlog-analysis.md](references/binlog-analysis.md)
- Categorizing a failure (real / flaky / infra) → [references/error-patterns.md](references/error-patterns.md)
