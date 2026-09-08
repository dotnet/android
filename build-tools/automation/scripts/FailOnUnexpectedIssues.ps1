param (
	[string] $JobStatus = $env:AGENT_JOBSTATUS,
	[string] $RecoveredOptionalTaskRefs = $env:RECOVERED_OPTIONAL_TASK_REFS,
	[string] $JobId = $env:SYSTEM_JOBID,
	[string] $TimelinePath
)

$ErrorActionPreference = 'Stop'
Write-Host "Current job status is: $JobStatus"

if ($JobStatus -ne 'SucceededWithIssues') {
	return
}

$allowedTasks = @($RecoveredOptionalTaskRefs -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($allowedTasks.Count -eq 0) {
	Write-Host '##vso[task.complete result=Failed;]DONE'
	return
}

try {
	if (-not [string]::IsNullOrWhiteSpace($TimelinePath)) {
		$timeline = Get-Content -LiteralPath $TimelinePath -Raw | ConvertFrom-Json
	} else {
		$requiredEnvironmentVariables = @{
			BUILD_BUILDID = $env:BUILD_BUILDID
			SYSTEM_COLLECTIONURI = $env:SYSTEM_COLLECTIONURI
			SYSTEM_ACCESSTOKEN = $env:SYSTEM_ACCESSTOKEN
			SYSTEM_JOBID = $JobId
			SYSTEM_TEAMPROJECT = $env:SYSTEM_TEAMPROJECT
		}
		foreach ($entry in $requiredEnvironmentVariables.GetEnumerator()) {
			if ([string]::IsNullOrWhiteSpace($entry.Value)) {
				throw "Required Azure Pipelines environment variable '$($entry.Key)' is not set."
			}
		}

		$project = [Uri]::EscapeDataString($env:SYSTEM_TEAMPROJECT)
		$timelineUri = "$($env:SYSTEM_COLLECTIONURI)$project/_apis/build/builds/$($env:BUILD_BUILDID)/timeline?api-version=7.1"
		$headers = @{
			Authorization = "Bearer $($env:SYSTEM_ACCESSTOKEN)"
		}
		$timeline = Invoke-RestMethod -Uri $timelineUri -Headers $headers
	}
} catch {
	Write-Host "Could not inspect the Azure Pipelines timeline: $($_.Exception.Message)"
	Write-Host '##vso[task.complete result=Failed;]DONE'
	return
}

$issueTasks = @($timeline.records | Where-Object {
	$_.parentId -eq $JobId -and
	$_.type -eq 'Task' -and
	$_.result -in @('failed', 'succeededWithIssues')
})
function Get-TaskReferenceName ($task) {
	if (-not [string]::IsNullOrWhiteSpace($task.refName)) {
		return $task.refName
	}
	return $task.name
}

$unexpectedTasks = @($issueTasks | Where-Object { (Get-TaskReferenceName $_) -notin $allowedTasks })

if ($issueTasks.Count -eq 0 -or $unexpectedTasks.Count -ne 0) {
	foreach ($task in $unexpectedTasks) {
		$taskReferenceName = Get-TaskReferenceName $task
		Write-Host "Unexpected issue task: $($task.name) ($taskReferenceName, result $($task.result))"
	}
	Write-Host '##vso[task.complete result=Failed;]DONE'
	return
}

foreach ($task in $issueTasks) {
	$taskReferenceName = Get-TaskReferenceName $task
	Write-Host "Ignoring recovered optional task: $($task.name) ($taskReferenceName)"
}
