---
name: azdo-build-investigator
description: Investigate Azure DevOps (AZDO) pipeline build failures by fetching logs, downloading artifacts, and analyzing .binlog files to find the root cause of errors. Use this when users share an AZDO build URL, a GitHub PR URL, or ask about a failing pipeline, build errors, or CI failures.
---

# AZDO Build Investigator

Given a build URL or GitHub PR URL, fetch run details, find failed jobs/tasks, download logs and .binlog artifacts, and produce a summarized root-cause error trail.

## Prerequisites

All three tools below are **required only when needed** (gh for PR URLs, az always, binlogtool for .binlog analysis). If any is missing, **stop immediately**, show the relevant setup link, and do NOT install on the user's behalf.

| Tool | Check | Install / Docs |
|------|-------|----------------|
| `gh` (GitHub CLI) | `gh --version` | https://docs.github.com/github-cli/github-cli/quickstart |
| `az` (Azure CLI + DevOps ext) | `az --version` | Install: https://learn.microsoft.com/cli/azure/install-azure-cli — Extension: `az extension add --name azure-devops` — Docs: https://learn.microsoft.com/cli/azure/devops — Then: `az login` and `az devops configure --defaults organization=https://dev.azure.com/ORGNAME project=PROJECTNAME` |
| `binlogtool` (.NET global tool) | `dotnet tool list -g` | `dotnet tool install -g binlogtool` — https://www.nuget.org/packages/binlogtool |

## Workflow

Follow in order. Stop early if root cause is found.

### 1. Resolve Input URL

**GitHub PR** (`https://github.com/{owner}/{repo}/pull/{number}`):

```powershell
gh pr checks {prNumber} --repo {owner}/{repo} --json "name,state,link,bucket"
```

Filter `bucket == "fail"`. Each has a `link` with the AZDO build URL. Multiple failures → ask user which to investigate. No failures → report passing/in-progress.

**AZDO build URL** patterns:
- `https://dev.azure.com/{org}/{project}/_build/results?buildId={id}`
- `https://{org}.visualstudio.com/{project}/_build/results?buildId={id}`

Extract `{org}`, `{project}` (may be a GUID — that's fine), and `{buildId}`.

### 2. Get Build Overview

```powershell
az pipelines runs show --id {buildId} --org https://dev.azure.com/{org} --project {project}
```

### 3. Get Failed Jobs & Tasks (Timeline)

```powershell
az devops invoke --area build --resource timeline --route-parameters buildId={buildId} --org https://dev.azure.com/{org} --project {project} --query "records[?result=='failed'] | [].{name:name, type:type, result:result, log:log, issues:issues, errorCount:errorCount}" --output json
```

Check the `issues` array first — it often contains the root cause directly.

### 4. Fetch Full Logs (if needed)

Get log URLs from failed timeline records, then fetch by log ID:

```powershell
az devops invoke --area build --resource logs --route-parameters buildId={buildId} logId={logId} --org https://dev.azure.com/{org} --project {project} --output json
```

### 5. Check for .binlog Artifacts

```powershell
az pipelines runs artifact list --run-id {buildId} --org https://dev.azure.com/{org} --project {project}
```

Look for artifact names containing `binlog`, `msbuild`, or `build-log`.

### 6. Download & Analyze .binlog

```powershell
$tempDir = Join-Path $env:TEMP "azdo-binlog-$buildId"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
az pipelines runs artifact download --artifact-name "{artifactName}" --path $tempDir --run-id {buildId} --org https://dev.azure.com/{org} --project {project}
```

Key `binlogtool` commands:

```powershell
binlogtool search "$tempDir\*.binlog" "error"          # broad search
binlogtool search "$tempDir\*.binlog" "XA"             # .NET Android errors
binlogtool search "$tempDir\*.binlog" "error CS"       # C# compiler
binlogtool search "$tempDir\*.binlog" "error NU"       # NuGet
binlogtool reconstruct "$tempDir\file.binlog" "$tempDir\reconstructed"  # full text log
binlogtool listproperties "$tempDir\file.binlog"       # MSBuild properties
binlogtool doublewrites "$tempDir\file.binlog" "$tempDir\dw"            # double-write issues
```

### 7. Summarize

Report: **Build overview** (pipeline, branch, trigger, result) → **Failed stage/job/task** → **Error messages** (with codes) → **Root cause analysis** → **Suggested fixes**.

### 8. Cleanup

```powershell
Remove-Item -Recurse -Force (Join-Path $env:TEMP "azdo-binlog-$buildId")
```

## Error Patterns (dotnet/android)

| Category | Patterns to search |
|----------|--------------------|
| MSBuild | `XA####` (.NET Android), `APT####` (Android tooling), `error CS####` (C#), `error NU####` (NuGet) |
| Tests | `Failed :`, `Assert.`, `System.TimeoutException`, `ADB`, `device not found` |
| Infra | `##[error]`, disk space, `Process exit code`, timeout |
| NuGet | `NU1100`–`NU1699` (resolution), `NU1301` (feed connectivity) |

## Tips

- Auth errors → user needs `az login` + `az devops configure --defaults`
- `--detect` auto-detects org/project from git remote when run inside the repo
- Focus on the **first** error chronologically — later errors often cascade
- `.binlog` has richer detail than text logs; use it when text logs show only generic "Build FAILED"
- `binlogtool search` broad terms first, then narrow with specific codes
