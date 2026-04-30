param (
    [string] $GitHubOwner = "dotnet",
    [string] $GitHubRepo = "android",
    [string] $GitHubApiUrl = "https://api.github.com",
    [string] $GitHubTokenEnvVar = "GITHUB_TOKEN",
    [string] $AzureDevOpsTokenEnvVar = "SYSTEM_ACCESSTOKEN",
    [string] $AzureDevOpsCollectionUri = $env:SYSTEM_COLLECTIONURI,
    [string] $AzureDevOpsProject = $env:SYSTEM_TEAMPROJECT,
    [string] $BuildId = $env:BUILD_BUILDID,
    [string] $BuildDefinitionId = $env:BUILD_DEFINITIONID,
    [string] $BuildNumber = $env:BUILD_BUILDNUMBER,
    [string] $BuildSourceBranch = $env:BUILD_SOURCEBRANCH,
    [string] $BuildSourceVersion = $env:BUILD_SOURCEVERSION,
    [string] $PullRequestNumber = $env:SYSTEM_PULLREQUEST_PULLREQUESTNUMBER,
    [string] $SummaryMarkdownPath,
    [string] $BuildJsonPath,
    [string] $TimelineJsonPath,
    [string] $PullRequestJsonPath,
    [string] $CopilotOptInLogins = $env:COPILOT_BUILD_SUMMARY_OPT_IN_LOGINS,
    [string] $CopilotAuthorLogins = "copilot,copilot-swe-agent,github-copilot[bot]",
    [string] $DisableCopilotMentions = $env:COPILOT_BUILD_SUMMARY_DISABLE_MENTIONS,
    [string] $SkipCanceledBuilds = "true",
    [string] $SkipIfNotLatestBuild = "true",
    [switch] $SkipAzureDevOpsSummary,
    [switch] $DryRun,
    [int] $MaxJobsToShow = 20,
    [int] $MaxIssuesPerJob = 3,
    [int] $MaxFailedTestsToShow = 10
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Get-RequiredEnvironmentVariable {
    param ([string] $Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Environment variable '$Name' is required."
    }
    return $value
}

function Get-OptionalEnvironmentVariable {
    param ([string] $Name)

    return [Environment]::GetEnvironmentVariable($Name)
}

function Get-PullRequestNumber {
    if (-not [string]::IsNullOrWhiteSpace($PullRequestNumber)) {
        return $PullRequestNumber
    }

    if ($env:BUILD_SOURCEBRANCH -match "^refs/pull/(\d+)/") {
        return $Matches[1]
    }

    throw "Unable to determine GitHub pull request number from System.PullRequest.PullRequestNumber or Build.SourceBranch."
}

function Get-BuildUrl {
    if ([string]::IsNullOrWhiteSpace($AzureDevOpsCollectionUri) -or [string]::IsNullOrWhiteSpace($AzureDevOpsProject) -or [string]::IsNullOrWhiteSpace($BuildId)) {
        return ""
    }

    return "$AzureDevOpsCollectionUri$AzureDevOpsProject/_build/results?buildId=$BuildId"
}

function Invoke-GitHubApi {
    param (
        [string] $Method,
        [string] $Path,
        [object] $Body = $null
    )

    $token = Get-RequiredEnvironmentVariable $GitHubTokenEnvVar
    $headers = @{
        Authorization = "Bearer $token"
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "dotnet-android-azdo-build-summary"
    }

    $uri = "$($GitHubApiUrl.TrimEnd('/'))$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 20
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body $json -ContentType "application/json; charset=utf-8"
}

function Invoke-AzureDevOpsApi {
    param ([string] $Path)

    $token = Get-RequiredEnvironmentVariable $AzureDevOpsTokenEnvVar
    $headers = @{
        Authorization = "Bearer $token"
        Accept = "application/json"
    }

    $uri = "$($AzureDevOpsCollectionUri.TrimEnd('/'))/$AzureDevOpsProject$Path"
    return Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
}

function Get-Timeline {
    if (-not [string]::IsNullOrWhiteSpace($TimelineJsonPath)) {
        if (-not (Test-Path -LiteralPath $TimelineJsonPath)) {
            throw "TimelineJsonPath '$TimelineJsonPath' does not exist."
        }
        return Get-Content -LiteralPath $TimelineJsonPath -Raw | ConvertFrom-Json
    }

    if ([string]::IsNullOrWhiteSpace($AzureDevOpsCollectionUri)) {
        throw "AzureDevOpsCollectionUri is required."
    }
    if ([string]::IsNullOrWhiteSpace($AzureDevOpsProject)) {
        throw "AzureDevOpsProject is required."
    }
    if ([string]::IsNullOrWhiteSpace($BuildId)) {
        throw "BuildId is required."
    }

    $escapedBuildId = [Uri]::EscapeDataString($BuildId)
    return Invoke-AzureDevOpsApi "/_apis/build/builds/$escapedBuildId/timeline?api-version=7.1"
}

function Get-Build {
    if (-not [string]::IsNullOrWhiteSpace($BuildJsonPath)) {
        if (-not (Test-Path -LiteralPath $BuildJsonPath)) {
            throw "BuildJsonPath '$BuildJsonPath' does not exist."
        }
        return Get-Content -LiteralPath $BuildJsonPath -Raw | ConvertFrom-Json
    }

    if ([string]::IsNullOrWhiteSpace($BuildId)) {
        throw "BuildId is required."
    }

    $escapedBuildId = [Uri]::EscapeDataString($BuildId)
    return Invoke-AzureDevOpsApi "/_apis/build/builds/$escapedBuildId`?api-version=7.1"
}

function Get-LatestBuildForBranch {
    if ([string]::IsNullOrWhiteSpace($BuildDefinitionId) -or [string]::IsNullOrWhiteSpace($BuildSourceBranch)) {
        return $null
    }

    $escapedDefinitionId = [Uri]::EscapeDataString($BuildDefinitionId)
    $escapedBranch = [Uri]::EscapeDataString($BuildSourceBranch)
    return Invoke-AzureDevOpsApi "/_apis/build/builds?definitions=$escapedDefinitionId&branchName=$escapedBranch&queryOrder=queueTimeDescending&`$top=1&api-version=7.1"
}

function Get-ResultIcon {
    param ([string] $Result)

    switch ($Result) {
        "succeeded" { return ":white_check_mark:" }
        "succeededWithIssues" { return ":warning:" }
        "failed" { return ":x:" }
        "canceled" { return ":no_entry_sign:" }
        "skipped" { return ":arrow_right:" }
        "abandoned" { return ":no_entry_sign:" }
        default { return ":hourglass_flowing_sand:" }
    }
}

function Get-DisplayResult {
    param ([object] $Record)

    if (-not [string]::IsNullOrWhiteSpace($Record.result)) {
        return $Record.result
    }
    if (-not [string]::IsNullOrWhiteSpace($Record.state)) {
        return $Record.state
    }
    return "unknown"
}

function Escape-MarkdownTableCell {
    param ([string] $Value)

    if ($null -eq $Value) {
        return ""
    }

    return ($Value -replace "\|", "\|" -replace "`r?`n", "<br />")
}

function Format-IssueMessage {
    param ([string] $Message)

    if ($null -eq $Message) {
        return ""
    }

    $singleLine = $Message -replace "\s+", " "
    if ($singleLine.Length -gt 240) {
        return "$($singleLine.Substring(0, 237))..."
    }
    return $singleLine
}

function Get-Count {
    param ([object] $Value)

    if ($null -eq $Value) {
        return 0
    }
    return [int] $Value
}

function Get-PropertyValue {
    param (
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-Issues {
    param ([object] $Record)

    $property = $Record.PSObject.Properties["issues"]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }

    return @($property.Value)
}

function Test-SummaryLooksActionableForCopilot {
    param ([object[]] $Records)

    $failedRecords = @($Records | Where-Object {
        $_.result -in @("failed", "succeededWithIssues") -or (Get-Count $_.errorCount) -gt 0
    })

    if ($failedRecords.Count -eq 0) {
        return $false
    }

    $messages = @()
    foreach ($record in $failedRecords) {
        $messages += [string] $record.name
        foreach ($issue in Get-Issues $record) {
            $messages += [string] $issue.message
        }
    }

    $text = ($messages -join "`n")
    $actionablePatterns = @(
        "error\s+(CS|MSB|XA|APT|NETSDK)\d+",
        "compil",
        "syntax",
        "typo",
        "spelling",
        "PoliCheck"
    )
    $nonActionablePatterns = @(
        "canceled",
        "cancelled",
        "timeout",
        "timed out",
        "agent",
        "pool",
        "infrastructure",
        "network",
        "connection reset",
        "service unavailable",
        "DEVICE_NOT_FOUND",
        "emulator",
        "No space left on device"
    )

    foreach ($pattern in $nonActionablePatterns) {
        if ($text -match $pattern) {
            return $false
        }
    }

    foreach ($pattern in $actionablePatterns) {
        if ($text -match $pattern) {
            return $true
        }
    }

    return $false
}

function Split-LoginList {
    param ([string] $Logins)

    if ([string]::IsNullOrWhiteSpace($Logins)) {
        return @()
    }

    return @($Logins.Split(",", [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
}

function Test-LoginInList {
    param (
        [string] $Login,
        [string[]] $Logins
    )

    if ([string]::IsNullOrWhiteSpace($Login)) {
        return $false
    }

    return $Logins -contains $Login.ToLowerInvariant()
}

function Test-Truthy {
    param ([string] $Value)

    return $Value -in @("1", "true", "True", "TRUE", "yes", "Yes", "YES")
}

function Get-RecordName {
    param ([object] $Record)

    if (-not [string]::IsNullOrWhiteSpace($Record.name)) {
        return $Record.name
    }
    if (-not [string]::IsNullOrWhiteSpace($Record.identifier)) {
        return $Record.identifier
    }
    return $Record.id
}

function Get-LogLines {
    param ([object] $Log)

    $logId = Get-PropertyValue $Log "id"
    if ([string]::IsNullOrWhiteSpace("$logId")) {
        return @()
    }

    if ([string]::IsNullOrWhiteSpace((Get-OptionalEnvironmentVariable $AzureDevOpsTokenEnvVar))) {
        Write-Host "Skipping failed test log scan for log $logId because $AzureDevOpsTokenEnvVar is unavailable."
        return @()
    }

    $escapedBuildId = [Uri]::EscapeDataString($BuildId)
    $escapedLogId = [Uri]::EscapeDataString("$logId")
    $logContent = Invoke-AzureDevOpsApi "/_apis/build/builds/$escapedBuildId/logs/$escapedLogId"

    if ($null -eq $logContent) {
        return @()
    }
    if ($logContent -is [string]) {
        return @($logContent -split "`r?`n")
    }
    if ($logContent -is [System.Array]) {
        return @($logContent)
    }

    $value = Get-PropertyValue $logContent "value"
    if ($null -ne $value) {
        return @($value)
    }

    return @($logContent | Out-String -Stream)
}

function Remove-LogTimestamp {
    param ([string] $Line)

    if ($null -eq $Line) {
        return ""
    }

    return ($Line -replace "^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z\s*", "")
}

function Get-FailedTestSummaries {
    param ([object[]] $Records)

    $failedTests = [System.Collections.Generic.List[object]]::new()
    $seenTests = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

    foreach ($record in $Records) {
        $log = Get-PropertyValue $record "log"
        $lines = @(Get-LogLines $log)
        $currentTest = ""

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = Remove-LogTimestamp ([string] $lines[$i])
            if ($line -match "\]\s+(.+?)\s+\[FAIL\]") {
                $currentTest = $Matches[1].Trim()
            } elseif ($line -match "^\s*Failed\s+(.+?)\s+\[\d") {
                $currentTest = $Matches[1].Trim()
            }

            if ($line -match "^\s*Error Message:\s*$" -and -not [string]::IsNullOrWhiteSpace($currentTest)) {
                $message = ""
                for ($j = $i + 1; $j -lt [Math]::Min($i + 6, $lines.Count); $j++) {
                    $candidate = (Remove-LogTimestamp ([string] $lines[$j])).Trim()
                    if ([string]::IsNullOrWhiteSpace($candidate)) {
                        continue
                    }
                    if ($candidate -eq "Stack Trace:") {
                        break
                    }
                    $message = $candidate
                    break
                }

                if ($seenTests.Add($currentTest)) {
                    $failedTests.Add([PSCustomObject] @{
                        TestName = $currentTest
                        Message = $message
                    })
                }
            }
        }
    }

    return @($failedTests)
}

function New-SummaryMarkdown {
    param (
        [object] $Build,
        [object] $Timeline,
        [object] $PullRequest,
        [string] $PrNumber
    )

    $records = @($Timeline.records)
    $currentStageName = Get-OptionalEnvironmentVariable "SYSTEM_STAGENAME"
    $summaryStageNames = @($currentStageName, "StartBuildSummaryComment", "PostBuildSummaryComment", "BuildSummary")
    $stages = @($records | Where-Object {
        $_.type -eq "Stage" -and ($summaryStageNames -notcontains $_.identifier) -and ($summaryStageNames -notcontains $_.name)
    } | Sort-Object order)

    $jobsNeedingAttention = @($records | Where-Object {
        $_.type -eq "Job" -and (
            $_.result -in @("failed", "canceled", "succeededWithIssues", "abandoned") -or
            (Get-Count $_.errorCount) -gt 0 -or
            (Get-Count $_.warningCount) -gt 0
        )
    } | Sort-Object order | Select-Object -First $MaxJobsToShow)
    $tasksNeedingAttention = @($records | Where-Object {
        $_.type -eq "Task" -and $_.result -in @("failed", "canceled", "succeededWithIssues", "abandoned")
    } | Sort-Object order | Select-Object -First $MaxJobsToShow)
    $failedTests = @(Get-FailedTestSummaries $tasksNeedingAttention | Select-Object -First $MaxFailedTestsToShow)

    $failedStages = @($stages | Where-Object { $_.result -in @("failed", "canceled", "abandoned") })
    $warningStages = @($stages | Where-Object { $_.result -eq "succeededWithIssues" -or (Get-Count $_.warningCount) -gt 0 })
    $incompleteStages = @($stages | Where-Object { $_.state -ne "completed" -and [string]::IsNullOrWhiteSpace($_.result) })

    $buildStatus = Get-PropertyValue $Build "status"
    $buildResult = Get-PropertyValue $Build "result"

    $overall = "Succeeded"
    if (-not [string]::IsNullOrWhiteSpace($buildStatus) -and $buildStatus -ne "completed") {
        $overall = "In progress"
    } elseif (-not [string]::IsNullOrWhiteSpace($buildResult)) {
        switch ($buildResult) {
            "succeeded" { $overall = "Succeeded" }
            "succeededWithIssues" { $overall = "Succeeded with issues" }
            "failed" { $overall = "Failed" }
            "canceled" { $overall = "Canceled" }
            "abandoned" { $overall = "Abandoned" }
            default { $overall = $buildResult }
        }
    } elseif ($failedStages.Count -gt 0) {
        $overall = "Failed"
    } elseif ($warningStages.Count -gt 0) {
        $overall = "Succeeded with issues"
    } elseif ($incompleteStages.Count -gt 0) {
        $overall = "In progress"
    }

    $buildUrl = Get-BuildUrl
    $effectiveBuildNumber = Get-PropertyValue $Build "buildNumber"
    if ([string]::IsNullOrWhiteSpace($effectiveBuildNumber)) {
        $effectiveBuildNumber = $BuildNumber
    }

    $commit = Get-PropertyValue $Build "sourceVersion"
    if ([string]::IsNullOrWhiteSpace($commit)) {
        $commit = $BuildSourceVersion
    }
    if (-not [string]::IsNullOrWhiteSpace($commit) -and $commit.Length -gt 12) {
        $commit = $commit.Substring(0, 12)
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("<!-- dotnet-android-azdo-build-summary -->")
    $lines.Add("## Azure DevOps build summary")
    $lines.Add("")
    $lines.Add("**Result:** $overall")
    if ($overall -eq "In progress") {
        $lines.Add("")
        $lines.Add("> This build is still running. This comment is the latest status and will be updated when the final build-summary stage runs.")
    }
    $lines.Add("")
    $lines.Add("| Item | Value |")
    $lines.Add("| --- | --- |")
    $lines.Add("| Build | [$effectiveBuildNumber]($buildUrl) |")
    $lines.Add("| Build ID | ``$BuildId`` |")
    $lines.Add("| Pull request | #$PrNumber |")
    if (-not [string]::IsNullOrWhiteSpace($commit)) {
        $lines.Add("| Commit | ``$commit`` |")
    }
    if ($null -ne $PullRequest -and -not [string]::IsNullOrWhiteSpace($PullRequest.user.login)) {
        $lines.Add("| Author | @$($PullRequest.user.login) |")
    }
    $lines.Add("")

    if ($stages.Count -gt 0) {
        $lines.Add("### Stages")
        $lines.Add("")
        $lines.Add("| Stage | Result | Errors | Warnings |")
        $lines.Add("| --- | --- | ---: | ---: |")
        foreach ($stage in $stages) {
            $result = Get-DisplayResult $stage
            $icon = Get-ResultIcon $result
            $stageName = Escape-MarkdownTableCell (Get-RecordName $stage)
            $lines.Add("| $stageName | $icon $result | $(Get-Count $stage.errorCount) | $(Get-Count $stage.warningCount) |")
        }
        $lines.Add("")
    }

    if ($jobsNeedingAttention.Count -gt 0) {
        $lines.Add("### Jobs needing attention")
        $lines.Add("")
        foreach ($job in $jobsNeedingAttention) {
            $result = Get-DisplayResult $job
            $jobName = Get-RecordName $job
            $lines.Add("- $(Get-ResultIcon $result) **$jobName** - $result, $(Get-Count $job.errorCount) error(s), $(Get-Count $job.warningCount) warning(s)")
            $issues = @(Get-Issues $job | Select-Object -First $MaxIssuesPerJob)
            foreach ($issue in $issues) {
                $message = Format-IssueMessage $issue.message
                if (-not [string]::IsNullOrWhiteSpace($message)) {
                    $lines.Add("  - ``$($issue.type)`` $message")
                }
            }
        }
        $lines.Add("")
    }

    if ($tasksNeedingAttention.Count -gt 0) {
        $lines.Add("### Tasks needing attention")
        $lines.Add("")
        foreach ($task in $tasksNeedingAttention) {
            $result = Get-DisplayResult $task
            $taskName = Get-RecordName $task
            $lines.Add("- $(Get-ResultIcon $result) **$taskName** - $result, $(Get-Count $task.errorCount) error(s), $(Get-Count $task.warningCount) warning(s)")
            $issues = @(Get-Issues $task | Select-Object -First $MaxIssuesPerJob)
            foreach ($issue in $issues) {
                $message = Format-IssueMessage $issue.message
                if (-not [string]::IsNullOrWhiteSpace($message)) {
                    $lines.Add("  - ``$($issue.type)`` $message")
                }
            }
        }
        $lines.Add("")
    }

    if ($failedTests.Count -gt 0) {
        $lines.Add("### Failed tests")
        $lines.Add("")
        foreach ($failedTest in $failedTests) {
            $testName = Escape-MarkdownTableCell $failedTest.TestName
            $message = Escape-MarkdownTableCell (Format-IssueMessage $failedTest.Message)
            $lines.Add("- **$testName** - $message")
        }
        $lines.Add("")
    }

    if ($jobsNeedingAttention.Count -eq 0 -and $tasksNeedingAttention.Count -eq 0 -and $failedTests.Count -eq 0) {
        $lines.Add("No failed or warning jobs or tasks were found in the Azure DevOps timeline records available to this step.")
        $lines.Add("")
    }

    $lines.Add("> Generated from Azure DevOps timeline data for build ``$BuildId``. See the linked build for full logs and artifacts.")

    return ($lines -join "`n")
}

function Add-CopilotPromptIfNeeded {
    param (
        [string] $Markdown,
        [object] $PullRequest,
        [object] $Timeline
    )

    if (Test-Truthy $DisableCopilotMentions) {
        return $Markdown
    }

    $authorLogin = ""
    if ($null -ne $PullRequest -and $null -ne $PullRequest.user) {
        $authorLogin = $PullRequest.user.login
    }

    $copilotAuthors = Split-LoginList $CopilotAuthorLogins
    $optedInLogins = Split-LoginList $CopilotOptInLogins
    $shouldMention = (Test-LoginInList $authorLogin $copilotAuthors) -or (Test-LoginInList $authorLogin $optedInLogins)
    if (-not $shouldMention) {
        return $Markdown
    }

    if (-not (Test-SummaryLooksActionableForCopilot -Records @($Timeline.records))) {
        return $Markdown
    }

    $prompt = "@copilot This Azure DevOps build summary appears to contain code-actionable failures for this PR. Please address only the clear issues indicated by the build summary and avoid broad refactors."
    return "$prompt`n`n$Markdown"
}

function Get-SummaryComments {
    param ([string] $PrNumber)

    $marker = "<!-- dotnet-android-azdo-build-summary"
    $matchingComments = @()
    $comments = Invoke-GitHubApi -Method Get -Path "/repos/$GitHubOwner/$GitHubRepo/issues/$PrNumber/comments?per_page=100"
    foreach ($comment in @($comments)) {
        if ($comment.body -like "*$marker*") {
            $matchingComments += $comment
        }
    }

    return $matchingComments
}

function Add-BuildMarkerIfNeeded {
    param ([string] $Markdown)

    $marker = "<!-- dotnet-android-azdo-build-summary -->"
    if ($Markdown -like "*<!-- dotnet-android-azdo-build-summary*") {
        return $Markdown
    }

    return "$marker`n$Markdown"
}

function ConvertTo-BuildIdNumber {
    param ([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    [long] $number = 0
    if ([long]::TryParse($Value, [ref] $number)) {
        return $number
    }

    return $null
}

function Get-CommentBuildId {
    param ([object] $Comment)

    $body = Get-PropertyValue $Comment "body"
    if ([string]::IsNullOrWhiteSpace($body)) {
        return $null
    }

    $tableMatch = [regex]::Match($body, "\|\s*Build ID\s*\|\s*``?(\d+)``?\s*\|")
    if ($tableMatch.Success) {
        return ConvertTo-BuildIdNumber $tableMatch.Groups[1].Value
    }

    $markerMatch = [regex]::Match($body, "dotnet-android-azdo-build-summary\s+build-id=(\d+)")
    if ($markerMatch.Success) {
        return ConvertTo-BuildIdNumber $markerMatch.Groups[1].Value
    }

    return $null
}

function Test-CurrentBuildIsLatest {
    if (-not (Test-Truthy $SkipIfNotLatestBuild)) {
        return $true
    }

    $latestBuildList = Get-LatestBuildForBranch
    if ($null -eq $latestBuildList) {
        Write-Host "Skipping latest-build check because BuildDefinitionId or BuildSourceBranch is unavailable."
        return $true
    }

    $latestBuildValue = Get-PropertyValue $latestBuildList "value"
    if ($null -eq $latestBuildValue) {
        Write-Host "Skipping latest-build check because no builds were returned for definition $BuildDefinitionId and branch $BuildSourceBranch."
        return $true
    }

    $latestBuilds = @($latestBuildValue)
    if ($latestBuilds.Count -eq 0) {
        Write-Host "Skipping latest-build check because no builds were returned for definition $BuildDefinitionId and branch $BuildSourceBranch."
        return $true
    }

    $latestBuildId = Get-PropertyValue $latestBuilds[0] "id"
    if ("$latestBuildId" -ne "$BuildId") {
        Write-Host "Skipping GitHub PR build summary comment for build $BuildId because newer build $latestBuildId exists for $BuildSourceBranch."
        return $false
    }

    return $true
}

function Update-BuildMetadataFromBuild {
    param ([object] $Build)

    if ([string]::IsNullOrWhiteSpace($BuildDefinitionId)) {
        $definition = Get-PropertyValue $Build "definition"
        $definitionId = Get-PropertyValue $definition "id"
        if (-not [string]::IsNullOrWhiteSpace("$definitionId")) {
            $script:BuildDefinitionId = "$definitionId"
        }
    }

    if ([string]::IsNullOrWhiteSpace($BuildSourceBranch)) {
        $sourceBranch = Get-PropertyValue $Build "sourceBranch"
        if (-not [string]::IsNullOrWhiteSpace($sourceBranch)) {
            $script:BuildSourceBranch = $sourceBranch
        }
    }

    if ([string]::IsNullOrWhiteSpace($BuildSourceVersion)) {
        $sourceVersion = Get-PropertyValue $Build "sourceVersion"
        if (-not [string]::IsNullOrWhiteSpace($sourceVersion)) {
            $script:BuildSourceVersion = $sourceVersion
        }
    }

    if ([string]::IsNullOrWhiteSpace($BuildNumber)) {
        $buildNumber = Get-PropertyValue $Build "buildNumber"
        if (-not [string]::IsNullOrWhiteSpace($buildNumber)) {
            $script:BuildNumber = $buildNumber
        }
    }
}

function Publish-GitHubComment {
    param (
        [string] $PrNumber,
        [string] $Body
    )

    $existingComments = @(Get-SummaryComments $PrNumber)
    if ($existingComments.Count -gt 0) {
        $currentBuildId = ConvertTo-BuildIdNumber $BuildId
        if ((Test-Truthy $SkipIfNotLatestBuild) -and $null -ne $currentBuildId) {
            foreach ($comment in $existingComments) {
                $commentBuildId = Get-CommentBuildId $comment
                if ($null -ne $commentBuildId -and $commentBuildId -gt $currentBuildId) {
                    Write-Host "Skipping GitHub PR build summary comment for build $BuildId because existing comment $($comment.id) already points to newer build $commentBuildId."
                    return $comment
                }
            }
        }

        $primaryComment = $existingComments[0]
        Write-Host "Updating existing GitHub PR build summary comment $($primaryComment.id) for build $BuildId."
        $updatedComment = Invoke-GitHubApi -Method Patch -Path "/repos/$GitHubOwner/$GitHubRepo/issues/comments/$($primaryComment.id)" -Body @{ body = $Body }

        foreach ($comment in @($existingComments | Select-Object -Skip 1)) {
            Write-Host "Deleting duplicate GitHub PR build summary comment $($comment.id)."
            Invoke-GitHubApi -Method Delete -Path "/repos/$GitHubOwner/$GitHubRepo/issues/comments/$($comment.id)"
        }
        return $updatedComment
    }

    Write-Host "Creating GitHub PR build summary comment for build $BuildId."
    return Invoke-GitHubApi -Method Post -Path "/repos/$GitHubOwner/$GitHubRepo/issues/$PrNumber/comments" -Body @{ body = $Body }
}

$prNumber = Get-PullRequestNumber
$build = Get-Build
Update-BuildMetadataFromBuild $build
$buildResult = Get-PropertyValue $build "result"
if ((Test-Truthy $SkipCanceledBuilds) -and $buildResult -in @("canceled", "abandoned")) {
    Write-Host "Skipping GitHub PR build summary comment for $buildResult build $BuildId."
    exit 0
}
if (-not (Test-CurrentBuildIsLatest)) {
    exit 0
}

$timeline = Get-Timeline
if (-not [string]::IsNullOrWhiteSpace($PullRequestJsonPath)) {
    if (-not (Test-Path -LiteralPath $PullRequestJsonPath)) {
        throw "PullRequestJsonPath '$PullRequestJsonPath' does not exist."
    }
    $pullRequest = Get-Content -LiteralPath $PullRequestJsonPath -Raw | ConvertFrom-Json
} else {
    $pullRequest = Invoke-GitHubApi -Method Get -Path "/repos/$GitHubOwner/$GitHubRepo/pulls/$prNumber"
}

if (-not [string]::IsNullOrWhiteSpace($SummaryMarkdownPath) -and (Test-Path -LiteralPath $SummaryMarkdownPath)) {
    $markdown = Get-Content -LiteralPath $SummaryMarkdownPath -Raw
} else {
    $markdown = New-SummaryMarkdown -Build $build -Timeline $timeline -PullRequest $pullRequest -PrNumber $prNumber
}

$markdown = Add-BuildMarkerIfNeeded -Markdown $markdown
$markdown = Add-CopilotPromptIfNeeded -Markdown $markdown -PullRequest $pullRequest -Timeline $timeline

if ($DryRun) {
    Write-Host "Dry run: would post the following GitHub comment to $GitHubOwner/$GitHubRepo#${prNumber}:"
    Write-Host "-----"
    Write-Host $markdown
    Write-Host "-----"
    exit 0
}

if (-not $SkipAzureDevOpsSummary) {
    $summaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "dotnet-android-build-summary-$BuildId.md"
    Set-Content -LiteralPath $summaryPath -Value $markdown -Encoding UTF8
    Write-Host "##vso[task.uploadsummary]$summaryPath"
}

$comment = Publish-GitHubComment -PrNumber $prNumber -Body $markdown
$commentUrl = Get-PropertyValue $comment "html_url"
if (-not [string]::IsNullOrWhiteSpace($commentUrl)) {
    Write-Host "Posted GitHub PR comment: $commentUrl"
} else {
    Write-Host "Posted or updated GitHub PR build summary comment."
}
