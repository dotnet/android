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

#### Step 1 — Resolve the PR

**No PR specified** — detect from current branch:

```bash
gh pr view --json number,title,url,headRefName --jq '{number,title,url,headRefName}'
```

```powershell
gh pr view --json number,title,url,headRefName | ConvertFrom-Json
```

If no PR exists for the current branch, tell the user and stop.

**PR number given** — use it directly.

#### Step 2 — Get GitHub check status

```bash
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" 2>&1 \
  | jq '[.[] | {name, state, bucket, link}]'
```

```powershell
gh pr checks $PR --repo dotnet/android --json "name,state,link,bucket" | ConvertFrom-Json
```

Note which checks passed/failed/pending. The `link` field contains the AZDO build URL for internal checks.

#### Step 3 — Get Azure DevOps build status

Extract the AZDO build URL from the check `link` fields. Parse `{orgUrl}`, `{project}`, and `{buildId}` from patterns:
- `https://dev.azure.com/{org}/{project}/_build/results?buildId={id}`
- `https://{org}.visualstudio.com/{project}/_build/results?buildId={id}`

Then fetch the build timeline:

```bash
az devops invoke --area build --resource timeline \
  --route-parameters project=$PROJECT buildId=$BUILD_ID \
  --org $ORG_URL --project $PROJECT \
  --query "records[?result=='failed'] | [].{name:name, type:type, result:result, issues:issues, errorCount:errorCount, log:log}" \
  --output json 2>&1
```

```powershell
az devops invoke --area build --resource timeline `
  --route-parameters project=$PROJECT buildId=$BUILD_ID `
  --org $ORG_URL --project $PROJECT `
  --query "records[?result=='failed'] | [].{name:name, type:type, result:result, issues:issues, errorCount:errorCount, log:log}" `
  --output json
```

Check `issues` arrays first — they often contain the root cause directly.

#### Step 4 — Present summary

Use this format:

```
# CI Status for PR #NNNN — "PR Title"

## GitHub Checks
| Check | Status |
|-------|--------|
| check-name | ✅ / ❌ / 🟡 |

## Azure DevOps Build [#BuildId](link)
**Result:** ✅ Succeeded / ❌ Failed / 🟡 In Progress

### Failures (if any)
❌ Stage > Job > Task
   Error: <first error message>

## What next?
1. View full logs for a failure
2. Download and analyze .binlog artifacts
3. Retry failed stages
```

**If no failures found anywhere**, report CI as green and stop.

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

- **Build in progress:** Report current status and offer to watch with `gh pr checks --watch`
- **No AZDO build found:** The PR may not have triggered internal CI yet. Report GitHub checks only.
- **Auth expired:** Tell user to run `az login` and retry.
- **Build not found:** Verify the PR number/build ID is correct.

## Tips

- Focus on the **first** error chronologically — later errors often cascade
- `.binlog` has richer detail than text logs when logs show only "Build FAILED"
- `issues` in timeline records often contain the root cause without needing to download logs
