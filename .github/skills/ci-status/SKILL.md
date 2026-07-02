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

Triage CI for a `dotnet/android` PR in two phases: **Phase 1** (always) gathers status and renders the report; **Phase 2** (only when asked) drills in via the references. Run the commands verbatim — the `jq`/`az` queries are exact and fragile.

Every PR runs **one** public Azure DevOps build: pipeline **`dotnet-android`** on `dev.azure.com/dnceng-public` (project `public`, definition `333`), full test matrix. It surfaces on GitHub as ~39 `dotnet-android (...)` checks plus `license/cla`, all backed by that single build.

## Pipeline facts (apply throughout)

Everything else is standard `gh`/`az` plus the **azure-devops** CLI extension (`az extension add --name azure-devops`); only these are non-obvious:

- **Judge pass/fail by the build `result` + GitHub check states — never by the test API.** Device-test lanes run with `continueOnError`, so flaky failures (notably `System.NetTests.SslTest.*`, or failures only in flavor lanes like `-TrimModePartial`/`-NoAab`) show as failed tests on otherwise-green builds.
- **Expect a fork PR to await `/azp run` approval** (re-approved per push); direct PRs auto-start on push. Forks change only triggering, not which pipeline runs.
- **Query test results with `az rest`** — `az devops invoke --area test --resource runs` 404s on dnceng-public, so use `az rest` for the `runs` and `ResultsByBuild` endpoints. Other `--area test` resources (e.g. `--resource results`, see references/azdo-queries.md) work fine. The `build` area works unauthenticated; `az rest` and log/artifact downloads need `az login` (else 401).

## Phase 1 — Status (always)

Run the steps in order; each `jq` reuses a file an earlier fetch saved:

1. **Resolve the PR** and its build id — stop if none or not yet built.
2. **Fetch the build result** and save the timeline.
3. **Derive** job status (3a), per-job timing (3b), and the failing-job test breakdown (3c).
4. **Decide the verdict**, then **write the report**.

```bash
ORG=https://dev.azure.com/dnceng-public; PROJECT=public
```

**Step 1 — Resolve the PR.** Drop `--repo`/`$PR` to auto-detect from the current branch:

```bash
gh pr view $PR --repo dotnet/android --json number,title,isCrossRepository
gh pr checks $PR --repo dotnet/android --json name,state,link
BUILD_ID=$(gh pr checks $PR --repo dotnet/android --json name,link \
  --jq '[.[]|select(.name|startswith("dotnet-android")).link][0]' | grep -oE 'buildId=[0-9]+' | cut -d= -f2 | head -1)
```

If `BUILD_ID` is empty (checks "Expected", no build URL), the pipeline hasn't started — report "awaiting `/azp run` approval" (fork) or "not triggered yet" (direct), then stop.

**Step 2 — Fetch the build result and save the timeline** (both valid mid-build; `/tmp/tl.json` is reused by Steps 3–4):

```bash
az devops invoke --area build --resource builds --org $ORG \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query "{status:status, result:result, startTime:startTime, finishTime:finishTime}" -o json

az devops invoke --area build --resource timeline --org $ORG \
  --route-parameters project=$PROJECT buildId=$BUILD_ID --query "records[]" -o json > /tmp/tl.json
```

**Step 3a — List job status, then failing records.** `state` is `completed`/`inProgress`/`pending` (pending is often 0 — stages start in parallel). Trust failing `issues[]` for the root cause; check names (e.g. `dotnet-android (Linux Tests Linux > Tests > MSBuild 2)`) already name the lane:

```bash
jq -r '.[]|select(.type=="Job")|[(.result // .state), .name]|@tsv' /tmp/tl.json | sort
jq -r '.[]|select(.result=="failed" or .result=="canceled")|[.type,.name,((.issues//[])|map(.message)|join(" | "))]|@tsv' /tmp/tl.json
```

**Step 3b — Time every job and spell out its status.** Emit one row per job: `Status` · `Wait` (build start → job start: upstream builds + agent queue) · `Run` (execution) · `Finished` (… ago, or `running`). Always spell `Status` out — never a bare icon (this vocabulary is reused in the report):

- `✅ Passed` · `❌ Failed` · `⏹️ Canceled`
- `⏱️ Timed out (N-min cap)` — a `canceled` job whose `issues[]` says *"ran longer than the maximum time"* (read N from the message)
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
  | [ reason, .name,
      (if .startTime then ((.startTime|secs)-$t0|hms) else "—" end),
      (if .startTime then (((.finishTime|if .==null then $now else secs end))-(.startTime|secs)|hms) else "—" end),
      (if .finishTime then (($now-(.finishTime|secs))|hms)+" ago" elif .state=="inProgress" then "running" else "—" end) ]
  | @tsv' /tmp/tl.json | sort -t$'\t' -k2 | column -t -s$'\t'
```

The `reason` function detects timeout from each job's own `issues[]`. Refine a bare `❌ Failed` with the Step 3c count: **0 failed tests ⇒ a canceled `Run tests` task or the `fail if any issues occurred` gate, not a real failure** — say so.

**Step 3c — Fetch failed tests + per-flavor counts** (two `az rest` calls; `--area test --resource runs` 404s here, so we use `az rest` directly): **(a)** failed test names + their `runId`; **(b)** every run's per-flavor counts + its phase (`unanalyzedTests`=failed, `notApplicableTests`=skipped):

```bash
RES=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/test/ResultsByBuild?buildId=$BUILD_ID&outcomes=Failed&api-version=7.1-preview" \
  --query "value[].{test:automatedTestName, runId:runId}" -o json > /tmp/failed.json

az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/test/runs?buildUri=vstfs:///Build/Build/$BUILD_ID&api-version=7.1&includeRunDetails=true" \
  --query "value[].{id:id, name:name, total:totalTests, passed:passedTests, failed:unanalyzedTests, skipped:notApplicableTests, phase:pipelineReference.phaseReference.phaseName}" -o json > /tmp/runs.json
```

Then build the breakdown — for each failed/canceled job, list its flavors (test runs) with `passed/total · fail · skip`, failed test names nested beneath:

```bash
jq -r --slurpfile failed /tmp/failed.json --slurpfile tl /tmp/tl.json '
  [$tl[0][]|select(.type=="Phase")] as $ph
  | ($ph|map(select(.result=="failed" or .result=="canceled"))|map(.refName)) as $bad
  | $failed[0] as $ft
  | group_by(.phase)[] | select(.[0].phase as $p|$bad|index($p))
  | .[0].phase as $p | ($ph[]|select(.refName==$p)|.name) as $job
  | "### \($job) — \(map(.total)|add) tests: \(map(.passed)|add) passed, \(map(.failed)|add) failed, \(map(.skipped)|add) skipped",
    (sort_by(-.failed,.name)[]
      | (if .failed>0 then "❌" else "✅" end) as $m
      | "  \($m) \(.name)  (\(.passed)/\(.total) pass, \(.failed) fail, \(.skipped) skip)",
        (.id as $rid|$ft[]|select(.runId==$rid)|"       ↳ \(.test)"))
' /tmp/runs.json
```

`ResultsByBuild` returns every failed test across runs (only `Failed`/`Aborted` are queryable). Matrix lanes that share one phase (e.g. `MSBuild+Emulator`) aggregate in the breakdown — use the Step 3b timing table to pinpoint the numbered job that died. For per-test error/stack, the ETA query, and the run→job mapping, see [references/azdo-queries.md](references/azdo-queries.md).

**Step 3d — Deep failure analysis (run whenever the build is red).** From the repo root, run the bundled C# file-based app — it turns raw failures into the **per-test cross-config matrix**, **crash detection**, and **branch cross-reference** the report needs (makes its own `az`/`gh` calls, needs `az login` and the .NET SDK, ~15–45 s — scales with the affected test family + retries):

```bash
dotnet run .github/skills/ci-status/scripts/ci_failures.cs -- --build-id $BUILD_ID --pr $PR
```

(First run restores/builds the app, so allow a few extra seconds. Omit `--pr $PR` to skip the branch cross-reference.)

It prints three report-ready sections:
- **Cross-config matrix** — per failed test: the flavors/OSes where it **failed** vs **passed**, with same-build retries shown as `Failed→Passed (retry)` (a retry that passes ⇒ flaky), plus the assembly and the assert/stack. Failing in one flavor/OS only localizes the cause; failing across many is systemic.
- **Crashed / incomplete lanes** — lanes that went red with *no* usable failed-test list (`Zero tests ran`, an incomplete run, or a timeout/hang). The culprit (a test that **started but never finished**, or a native crash) lives only in the device **logcat**; the script prints the download+grep command (also in [references/azdo-queries.md](references/azdo-queries.md)).
- **Branch cross-reference** — PR-changed files whose name matches a failing test's class/namespace/assembly: a lead for an obvious cause. Confirm against the diff before asserting causation.


### Step 4 — Verdict (decide before writing). Judge by build `result` + checks, NOT the failed-test count:

- **`result: failed`, or any ❌ check → red.** Lead with the gating failures (their jobs + tests). If the build is still running with a job already failed, surface it so the user can start fixing now.
- **`result: succeeded` and all checks green → green** — even if `ResultsByBuild` lists failures, those are flaky `continueOnError` lanes. Note them in one line; don't block.

### Report format

Emit this structure (omit sections that don't apply). Spell out every `Status` per the Step 3b vocabulary, refining `❌ Failed` with the Step 3c count:

```
# CI Status — PR #NNNN "<title>"
🔀 Direct PR   (or 🍴 Fork PR — may await `/azp run` approval)

## dotnet-android [#<buildId>](<link>)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress
⏱️ <elapsed>  ·  ETA ~HH:MM UTC (rough — recent runs ≈50 min–3 h)   ← only while in progress
📊 Jobs: <done>/<total> done · <running> running · <waiting> waiting

| Stage > Job | Status | Wait | Run | Finished |
|-------------|--------|------|-----|----------|
| Mac > macOS > Build | ✅ Passed | 12m | 23m | 8h28m ago |
| Package Tests > macOS > Tests > APKs 2 | ❌ Failed — 1 test (flaky GC) | 1h42m | 1h13m | 6h12m ago |
| Package Tests > macOS > Tests > APKs 1 | ❌ Failed — 0 tests (canceled run / gate) | 1h41m | 26m31s | 7h02m ago |
| MSBuild Emulator Tests > … > MSBuild+Emulator 6 | ⏱️ Timed out (180-min cap) | 1h44m | 3h00m | 4h21m ago |
(List every job, or — for a large matrix — the failed/canceled/timed-out lanes plus the slowest few.)

### Failures                ← if any
❌ <Stage> > <Job> — <first error from issues[]>

### Failed tests — cross-config (Step 3d)   ← one block per failed test
**`SslWithinTasksShouldWork`** (`System.NetTests.SslTest` · `microsoft.android.run.dll`)
- ❌ failed: `NoAab` (Failed→Passed on retry), `TrimModePartial` (Failed→Passed on retry)
- ✅ passed: `Release`, `CoreCLR`, `Debug`, +4 more
- `System.Net.WebException : 503 Service Unavailable` ⇒ flaky network, non-gating
      at System.NetTests.SslTest.SslWithinTasksShouldWork()

### Crashed / incomplete lanes (Step 3d)   ← if any
⚠️ **Mono.Android.NET_Tests-Debug** — `run` task succeededWithIssues, no results published ("Zero tests ran" / native crash). Name the culprit from logcat (Step 3d command).

### Branch cross-reference (Step 3d)   ← if --pr and a name overlaps
🔍 `SomeType.SomeTest` ⟵ `src/.../SomeType.cs` changed in this PR — likely cause; confirm in the diff.

## Verdict: ✅ green  /  ❌ red — <one-line reason>

## What next?
1. Logs / stack trace for a failure
2. `.binlog` (+ `logcat-*.txt` for device-test crashes)
3. Re-run a flaky/failed stage with `/azp run`
```

Notes: every `dotnet-android (...)` check is one job, so the Stage > Job table *is* the check list (the only non-`dotnet-android` check is `license/cla`). Step 3d's cross-config matrix is the fastest way to tell a real failure (fails across flavors/OSes, never passes on retry) from a flake (single flavor, or `Failed→Passed` on retry). For a crashed lane with no failed-test list, name the culprit from the device `logcat-<flavor>.txt` (Step 3d's command; recipe in [references/azdo-queries.md](references/azdo-queries.md)) — not the test message.

## Phase 2 — Deep dive (only when asked)

Read the matching reference, then act on it:

- Logs, per-test error/stack, ETA, per-flavor breakdown fields + run→job mapping, **crash-culprit from logcat** → [references/azdo-queries.md](references/azdo-queries.md)
- `.binlog` download + analysis → [references/binlog-analysis.md](references/binlog-analysis.md)
- Categorize a failure (real / flaky / infra) → [references/error-patterns.md](references/error-patterns.md)
