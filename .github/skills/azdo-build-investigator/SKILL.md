---
name: azdo-build-investigator
description: Investigate Azure DevOps (AZDO) pipeline build failures by fetching logs, downloading artifacts, and analyzing .binlog files to find the root cause of errors. Use this when users share an AZDO build URL, a GitHub PR URL, or ask about a failing pipeline, build errors, or CI failures.
---

# AZDO Build Investigator

Investigate Azure DevOps pipeline build failures to find root causes. Given a build URL or GitHub PR URL, this skill fetches run details, identifies failed jobs/tasks, downloads logs and .binlog artifacts, and produces a summarized error trail.

## Prerequisites

### GitHub CLI (for PR URLs)

If the user provides a GitHub PR URL, the `gh` CLI is required to look up the failing checks. If `gh` is not found, **stop immediately** and tell the user to set it up manually:

- **Install GitHub CLI:** https://docs.github.com/github-cli/github-cli/quickstart

Do NOT attempt to install or configure the GitHub CLI on the user's behalf.

### Azure CLI with DevOps Extension

This skill requires the Azure CLI with the `azure-devops` extension. If the `az` command is not found or `az devops` is not available, **stop immediately** and tell the user to set it up manually:

- **Install Azure CLI:** https://learn.microsoft.com/cli/azure/install-azure-cli
- **Install the DevOps extension:** `az extension add --name azure-devops`
- **DevOps extension docs:** https://learn.microsoft.com/cli/azure/devops
- **Authenticate:** `az login`
- **Configure defaults:** `az devops configure --defaults organization=https://dev.azure.com/ORGNAME project=PROJECTNAME`

Do NOT attempt to install or configure these tools on the user's behalf.

### binlogtool (.NET Global Tool)

For `.binlog` analysis, this skill uses the `binlogtool` .NET global tool. Check if it's installed:

```shell
dotnet tool list -g | findstr binlogtool   # Windows
dotnet tool list -g | grep binlogtool      # macOS/Linux
```

If missing, install it:

```shell
dotnet tool install -g binlogtool
```

- **NuGet page:** https://www.nuget.org/packages/binlogtool
- **MSBuild binary log info:** https://msbuildlog.com

## Investigation Workflow

Follow these steps in order. Stop early if the root cause is found.

### Step 1: Determine the Input URL Type

The user may provide either:
- A **GitHub PR URL** like `https://github.com/{owner}/{repo}/pull/{number}`
- An **AZDO build URL** like `https://dev.azure.com/{org}/{project}/_build/results?buildId={id}`

If the URL is a GitHub PR, go to Step 1a. If it's an AZDO build URL, skip to Step 1b.

### Step 1a: Resolve GitHub PR to AZDO Build URLs

Use the `gh` CLI to list check runs on the PR and find failing ones:

```powershell
gh pr checks {prNumber} --repo {owner}/{repo} --json "name,state,link,bucket"
```

Filter for checks where `bucket` is `fail` (or `state` is `FAILURE`). Each failing check has a `link` field containing the AZDO build URL. The `name` field identifies which check failed.

If there are **multiple failing checks**, list them for the user and ask which one to investigate, or investigate all of them starting with the first.

If there are **no failing checks**, tell the user all checks are passing (or still in progress if `state` is `IN_PROGRESS`).

Extract the AZDO build URL from the `link` field and continue to Step 1b.

### Step 1b: Parse the AZDO Build URL

Extract the organization, project, and build/run ID from the URL. AZDO build URLs follow these patterns:

```
https://dev.azure.com/{org}/{project}/_build/results?buildId={id}
https://dev.azure.com/{org}/{project}/_build/results?buildId={id}&view=results
https://{org}.visualstudio.com/{project}/_build/results?buildId={id}
```

Note: The `{project}` field may be a GUID (e.g., `cbb18261-c48f-4abb-8651-8cdcb5474649`) — this is fine, `az` commands accept project GUIDs.

### Step 2: Check Tool Availability

Verify `az` is available:

```powershell
az --version
```

If it fails, tell the user to install Azure CLI and the DevOps extension (see Prerequisites above), then stop.

### Step 3: Get Build Overview

```powershell
az pipelines runs show --id {buildId} --org https://dev.azure.com/{org} --project {project}
```

Report: pipeline name, status, result, source branch, start/finish times, and the reason it ran.

### Step 4: Get Build Timeline (Failed Jobs & Tasks)

Use the REST API via `az devops invoke` to get the build timeline, which contains every job and task with its status and error details:

```powershell
az devops invoke --area build --resource timeline --route-parameters buildId={buildId} --org https://dev.azure.com/{org} --project {project} --query "records[?result=='failed'] | [].{name:name, type:type, result:result, log:log, issues:issues, errorCount:errorCount}" --output json
```

This returns the failed records. For each failed task/job, note:
- The **name** and **type** (Job or Task)
- The **issues** array which often contains the actual error messages
- The **log** object which has a URL to the full log

If the timeline shows error messages in the `issues` field, these are often sufficient to identify the root cause without downloading full logs.

### Step 5: Download and Read Full Logs (if needed)

If the timeline issues don't reveal the root cause, download the full log for a failed task. The log URL from the timeline response can be fetched directly:

```powershell
az devops invoke --area build --resource timeline --route-parameters buildId={buildId} --org https://dev.azure.com/{org} --project {project} --query "records[?result=='failed' && log!=null] | [].{name:name, logUrl:log.url}" --output json
```

Then fetch individual log content using the log URL via `az devops invoke` or use:

```powershell
# Get all logs for the build
az devops invoke --area build --resource logs --route-parameters buildId={buildId} --org https://dev.azure.com/{org} --project {project} --output json
```

To fetch a specific log by its ID:

```powershell
az devops invoke --area build --resource logs --route-parameters buildId={buildId} logId={logId} --org https://dev.azure.com/{org} --project {project} --output json
```

### Step 6: Check for .binlog Artifacts

List artifacts attached to the run:

```powershell
az pipelines runs artifact list --run-id {buildId} --org https://dev.azure.com/{org} --project {project}
```

Look for artifacts with names containing `binlog`, `msbuild`, or `build-log`. Common artifact names in this repo include patterns like `Build *.binlog`, `msbuild.binlog`, etc.

### Step 7: Download and Analyze .binlog Files

If .binlog artifacts are found, download them to a temp directory:

```powershell
$tempDir = Join-Path $env:TEMP "azdo-binlog-$buildId"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
az pipelines runs artifact download --artifact-name "{artifactName}" --path $tempDir --run-id {buildId} --org https://dev.azure.com/{org} --project {project}
```

Then analyze with `binlogtool`. First check if it's installed (see Prerequisites). Then:

**Search for errors:**

```powershell
binlogtool search "$tempDir\*.binlog" "error"
```

**Search for specific error codes (useful for this repo):**

```powershell
binlogtool search "$tempDir\*.binlog" "XA"
binlogtool search "$tempDir\*.binlog" "error CS"
binlogtool search "$tempDir\*.binlog" "error NU"
```

**Reconstruct full text logs from a .binlog:**

```powershell
binlogtool reconstruct "$tempDir\file.binlog" "$tempDir\reconstructed"
```

Then search the reconstructed text logs for error patterns.

**Other useful binlogtool commands:**

```powershell
# List NuGet packages referenced in the build
binlogtool listnuget "$tempDir\file.binlog" "$tempDir\nuget-list"

# List all properties set during the build
binlogtool listproperties "$tempDir\file.binlog"

# Check for double-write issues (files written multiple times)
binlogtool doublewrites "$tempDir\file.binlog" "$tempDir\doublewrites"
```

### Step 8: Summarize Findings

Present a clear root-cause summary:

1. **Build Overview** — Pipeline name, branch, trigger, overall result
2. **Failed Stage/Job/Task** — Which part of the pipeline failed
3. **Error Messages** — The actual error text, with error codes if present
4. **Root Cause** — Your analysis of what went wrong
5. **Suggestions** — Possible fixes or next investigation steps

## Error Patterns (dotnet/android Repository)

When investigating builds from this repository, look for these common error patterns:

### MSBuild / Build Task Errors
- **`XA####`** — .NET for Android build errors/warnings (e.g., `XA0000`–`XA9999`). Search the repo's `Documentation/` or source for the error code meaning.
- **`APT####`** — Android asset/resource packaging tool errors
- **`error CS####`** — C# compiler errors
- **`error NU####`** — NuGet restore/package errors

### Test Failures
- **`NUnit`** / **`xUnit`** test failures — Look for `Failed :` or `Assert.` in logs
- **Device test crashes** — Look for `System.TimeoutException`, `ADB`, `emulator`, or `device not found`
- **`MSBuildDeviceIntegration`** — Integration test failures often show as build errors in test projects

### Infrastructure / Environment
- **`Agent.BuildDirectory`** / **disk space** — Out of disk space on CI agents
- **`##[error]`** — Azure DevOps pipeline error annotations
- **`Process exit code`** — Non-zero exit codes from build steps
- **Timeout** — Tasks exceeding time limits

### NuGet / Dependency
- **`NU1100`–`NU1699`** — NuGet dependency resolution failures
- **`error NU1301`** — Unable to load service index (feed connectivity)

## Cleanup

After investigation, clean up downloaded artifacts:

```powershell
Remove-Item -Recurse -Force (Join-Path $env:TEMP "azdo-binlog-$buildId")
```

## Tips

- If `az devops invoke` returns auth errors, the user needs to run `az login` and may need to configure `az devops configure --defaults`.
- The `--detect` flag on `az` commands tries to auto-detect org/project from the git remote — this works when running from within the repo directory.
- For very large builds with many failures, focus on the **first** error chronologically — later errors are often cascading failures.
- `.binlog` files contain much richer detail than text logs. If the text logs show a generic "Build FAILED" message, the `.binlog` will have the specific MSBuild target and task that failed.
- Use `binlogtool search` with broad terms first (`error`, `failed`), then narrow down with specific codes.
