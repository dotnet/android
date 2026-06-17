# AZDO queries (dnceng-public)

Deeper `az` commands for the `dotnet-android` build, beyond the core ones in SKILL.md. Shared setup:

```bash
ORG=https://dev.azure.com/dnceng-public; PROJECT=public
RES=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id, for `az rest --resource`
```

`build`-area `az devops invoke` works unauthenticated; in the `test` area only `--resource runs` is broken (404 on dnceng-public, so `runs` and `ResultsByBuild` go through `az rest`) — other resources like `--resource results` work fine. `az rest` and artifact/log downloads need `az login`.

## ETA for an in-progress build

Duration is dominated by hosted-agent queue time (same ~38 jobs every run, yet ~50 min to ~3 h+). Pull recent green runs of def `333`, take the **median** duration, `ETA = startTime + median`; present it as a rough window.

```bash
az devops invoke --area build --resource builds --org $ORG \
  --route-parameters project=$PROJECT \
  --query-parameters "definitions=333&statusFilter=completed&resultFilter=succeeded&\$top=10" \
  --query "value[].{start:startTime, finish:finishTime}" -o json
```

## Failed-test error message / stack trace

`ResultsByBuild` (SKILL.md) gives the names + `runId`. For messages, list the run's failed results — the single-result-by-`testId` route returns null here. Repeat per distinct `runId`:

```bash
az devops invoke --area test --resource results --org $ORG \
  --route-parameters project=$PROJECT runId=$RUN_ID \
  --query-parameters "outcomes=Failed&\$top=20" \
  --query "value[].{test:testCaseTitle, error:errorMessage, stack:stackTrace}" -o json
```

## Per-flavor test breakdown — fields & run → job mapping

The breakdown in SKILL.md fetches `/tmp/runs.json` from `/_apis/test/runs?...&includeRunDetails=true`. Field meanings per run (one run = one test *flavor*, e.g. `Mono.Android.NET_Tests-NativeAOT`):

| Field | Source | Meaning |
|-------|--------|---------|
| `total` | `totalTests` | all tests in the run |
| `passed` | `passedTests` | passed |
| `failed` | `unanalyzedTests` | failed/aborted |
| `skipped` | `notApplicableTests` | skipped / inconclusive |
| `phase` | `pipelineReference.phaseReference.phaseName` | the pipeline phase the run belongs to |

`run.phase` equals a timeline **Phase** record's `refName`; that record's `name` is the human lane — e.g. `mac_apk_tests_net_2` → `macOS > Tests > APKs 2`. That join (`runs` × timeline phases) is what the breakdown `jq` does. **Matrix lanes that share one phase** (e.g. all `MSBuild+Emulator N` jobs are phase `mac_dotnetdevice_tests`) aggregate into a single breakdown block — use the per-job timing table to see which numbered job actually failed/timed out.

Quick per-run counts without the join:

```bash
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/test/runs?buildUri=vstfs:///Build/Build/$BUILD_ID&api-version=7.1&includeRunDetails=true" \
  --query "value[].{name:name, total:totalTests, passed:passedTests, failed:unanalyzedTests, skipped:notApplicableTests}" -o json
```

To enrich the breakdown with the **actual error message** under each failed test, replace `/tmp/failed.json` with per-run results that include `errorMessage` (the "Failed-test error message" query above) — key them by `runId` the same way the breakdown's `$ft` lookup does.

## Fetch a failed task's log

Take `log.id` from a `records[?result=='failed']` timeline entry, then (works unauthenticated via `az rest`):

```bash
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/build/builds/$BUILD_ID/logs/$LOG_ID?api-version=7.1" --output-file "/tmp/azdo-$LOG_ID.log"
```

The per-flavor `run <flavor>` task log holds the MTP summary (`Test run summary: Zero tests ran` ⇒ the app crashed at startup); the per-test lifecycle and native crash are **not** here — they are in logcat (below).

## Crash culprit from logcat

`scripts/ci_failures.py` flags crashed/incomplete/timed-out lanes, but the culprit test is only in the device **logcat**, published inside that lane's `Test Results - ...` build artifact (100 MB–2 GB — prefer the smaller `Debug` lane). Download it, then scan `logcat-<flavor>.txt`:

```bash
# list artifacts + sizes to pick the failing lane:
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/build/builds/$BUILD_ID/artifacts?api-version=7.1" \
  --query "value[].{name:name, mb:(resource.properties.artifactsize)}" -o json

az pipelines runs artifact download --run-id $BUILD_ID --org $ORG --project $PROJECT \
  --artifact-name "Test Results - APKs .NET Debug - macOS 1" --path /tmp/cilogs

# The crasher is the LAST test that logged a start with no matching pass/fail,
# usually right before a native signal:
grep -nE 'Running |\[PASS\]|\[FAIL\]|SIGSEGV|SIGABRT|tombstone|FATAL|art::|JNI DETECTED|Process .* died' \
  /tmp/cilogs/**/logcat-*.txt | tail -60
```

For a `Zero tests ran` lane the crash is at app startup (look for the first `SIGSEGV`/`tombstone`/`JNI DETECTED ERROR`, not a specific test); for a timeout the suspect is the last `Running <test>` with no result.
