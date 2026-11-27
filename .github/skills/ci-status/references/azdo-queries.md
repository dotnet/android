# AZDO queries (dnceng-public)

Deeper `az` commands for the `dotnet-android` build, beyond the core ones in SKILL.md. Shared setup:

```bash
ORG=https://dev.azure.com/dnceng-public; PROJECT=public
RES=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id, for `az rest --resource`
```

`build`-area `az devops invoke` works unauthenticated; in the `test` area only `--resource runs` is broken (404 on dnceng-public, so `runs` and `ResultsByBuild` go through `az rest`) — other resources like `--resource results` work fine. `az rest` and artifact/log downloads need `az login`.

## Previous attempts after a targeted retry

Once a stage retry starts, the root timeline shows the new attempt. Preserve the original failure evidence by following each retried stage's `previousAttempts[].timelineId`:

```bash
az devops invoke --area build --resource timeline --org "$ORG" \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query 'records[?type==`Stage` && attempt>`1`].{name:name,refName:refName,attempt:attempt,previousAttempts:previousAttempts}' -o json

az rest --method get --resource "$RES" \
  --url "$ORG/$PROJECT/_apis/build/builds/$BUILD_ID/timeline/$PREVIOUS_TIMELINE_ID?api-version=7.1" \
  --query 'records[?result==`failed` || result==`canceled`].{type:type,name:name,refName:refName,parentId:parentId,result:result,issues:issues[].message}' -o json
```

Do not report “no failures” merely because the selected stages advanced to attempt 2. Report the original classification plus the current retry state.

## Targeted retry of failed stage jobs

The Azure DevOps [Stages - Update API](https://learn.microsoft.com/rest/api/azure/devops/build/stages/update?view=azure-devops-rest-7.1) accepts `state: retry`. With `forceRetryAllJobs: false`, Azure retries the failed jobs in the selected stage (and jobs transitively dependent on them), rather than rerunning every successful job in that stage.

List the completed failed stages and their stable YAML `refName`:

```bash
az devops invoke --area build --resource timeline --org "$ORG" \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query 'records[?type==`Stage` && state==`completed` && (result==`failed` || result==`canceled`)].{name:name,refName:refName,result:result,attempt:attempt}' -o json
```

After the user explicitly confirms the exact stages, retry one stage at a time:

```bash
az rest --method patch --resource "$RES" \
  --url "$ORG/$PROJECT/_apis/build/builds/$BUILD_ID/stages/$STAGE_REF?api-version=7.1" \
  --headers Content-Type=application/json \
  --body '{"state":"retry","forceRetryAllJobs":false}'
```

The API returns HTTP 200 with no body on success.

### Authentication and permissions

- Run `az login` using the Microsoft Entra tenant associated with the Azure DevOps organization. If necessary, use `az login --tenant <tenant-id>` and select a subscription from that tenant.
- Azure CLI requests a token for the Azure DevOps resource `499b84ac-1321-427f-aa17-267ca6975798`.
- The API operation requires OAuth scope `vso.build_execute`.
- The signed-in identity must exist in the Azure DevOps organization and have permission to queue/retry the pipeline (normally the pipeline's **Queue builds** permission). A token scope cannot override an Azure DevOps permission denial.

### Safeguards before PATCH

1. Re-fetch the build and verify its `sourceBranch` is `refs/pull/$PR/merge`; verify the build link/id is the one analyzed.
2. Re-fetch the timeline immediately before retrying.
3. Retry only stages that are still `completed` with `result` `failed`/`canceled`.
4. Do not issue another retry if the stage attempt advanced or is pending/in progress.
5. Select only stages whose failed descendants are all classified `known-flaky-test`, `transient-infrastructure`, or evidenced `timeout-or-crash` with confidence at least `0.60`.
6. Exclude stages containing a `likely-pr-regression` or `unknown` failure. A mixed stage requires an explicit user selection because the API acts at stage granularity.
7. Show the stage display names, `refName` values, and exact command before asking for confirmation.
8. Warn that failed dependent jobs can rerun.

### Verify the retry

Refresh the build and timeline after each PATCH. The selected stage must increment `attempt` or enter `pending`/`inProgress`:

```bash
az devops invoke --area build --resource timeline --org "$ORG" \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --query "records[?type==\`Stage\` && refName==\`$STAGE_REF\`].{name:name,attempt:attempt,state:state,result:result}" -o json
```

### Safe failures and fallback

| Response | Meaning / action |
|---|---|
| `203` or `401` | Missing/wrong Entra login or tenant. Reauthenticate; do not fall back automatically. |
| `403` | The identity lacks Azure DevOps organization/pipeline permission. Give the command to an authorized maintainer. |
| `404` | The build/stage ref is stale or incorrect. Refresh the timeline; never guess a refName. |
| `400` or `409` | The stage is no longer retryable or changed state. Refresh and report the current state. |

Fallback order:

1. Give the targeted REST command to an authorized maintainer.
2. Recommend Azure DevOps UI **Retry failed jobs** for the named stages.
3. Offer `/azp run` only with explicit confirmation when targeted retry is unavailable or a genuinely new full run is required.

Never silently turn a failed targeted retry into a full pipeline run.

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

`scripts/ci_failures.cs` flags crashed/incomplete/timed-out lanes, but the culprit test is only in the device **logcat**, published inside that lane's `Test Results - ...` build artifact (100 MB–2 GB — prefer the smaller `Debug` lane). Download it, then scan `logcat-<flavor>.txt`:

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
