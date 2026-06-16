# AZDO queries (dnceng-public)

Deeper `az` commands for the `dotnet-android` build, beyond the core ones in SKILL.md. Shared setup:

```bash
ORG=https://dev.azure.com/dnceng-public; PROJECT=public
RES=499b84ac-1321-427f-aa17-267ca6975798   # Azure DevOps app id, for `az rest --resource`
```

`build`-area `az devops invoke` works unauthenticated; the `test` area is broken (404) so the test data goes through `az rest`; `az rest` and artifact/log downloads need `az login`.

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

## Test-runs list (per-run pass/total)

```bash
az rest --method get --resource $RES \
  --url "$ORG/$PROJECT/_apis/test/runs?buildUri=vstfs:///Build/Build/$BUILD_ID&api-version=7.1" \
  --query "value[].{id:id, name:name, total:totalTests, passed:passedTests}" -o json
```

## Fetch a failed task's log

Take `log.id` from a `records[?result=='failed']` timeline entry, then:

```bash
az devops invoke --area build --resource logs --org $ORG --project $PROJECT \
  --route-parameters project=$PROJECT buildId=$BUILD_ID logId=$LOG_ID \
  --out-file "/tmp/azdo-$LOG_ID.log"
```
