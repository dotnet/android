$ErrorActionPreference = 'Stop'
$gateScript = Join-Path $PSScriptRoot 'FailOnUnexpectedIssues.ps1'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "xa-job-issue-tests-$([Guid]::NewGuid())"
$jobId = 'test-job'

function Assert-True ([bool] $condition, [string] $message) {
	if (-not $condition) {
		throw $message
	}
}

function Invoke-Gate ([object[]] $records, [string] $recoveredTasks, [string] $jobStatus = 'SucceededWithIssues') {
	$timelinePath = Join-Path $tempRoot "$([Guid]::NewGuid()).json"
	New-Timeline $records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $timelinePath -Encoding ASCII
	return & $gateScript `
		-JobStatus $jobStatus `
		-RecoveredOptionalTaskRefs $recoveredTasks `
		-JobId $jobId `
		-TimelinePath $timelinePath `
		-TimelinePollTimeoutSeconds 0 6>&1
}

function New-Task ([string] $refName, [string] $result, [string] $parentId = $jobId) {
	return @{
		name = $refName
		parentId = $parentId
		refName = $refName
		result = $result
		state = 'completed'
		type = 'Task'
	}
}

function New-Timeline ([object[]] $records) {
	$timelineRecords = [System.Collections.Generic.List[object]]::new()
	$nextOrder = 1
	foreach ($record in $records) {
		if ($record.parentId -eq $jobId) {
			if ($null -eq $record.order) {
				$record.order = $nextOrder
			}
			$nextOrder = [Math]::Max($nextOrder, [int] $record.order + 1)
		}
		$timelineRecords.Add($record)
	}
	$timelineRecords.Add(@{
		name = 'fail if any issues occurred'
		order = $nextOrder
		parentId = $jobId
		refName = 'failOnUnexpectedIssues'
		result = $null
		state = 'inProgress'
		type = 'Task'
	})
	return @{
		records = $timelineRecords.ToArray()
	}
}

function Invoke-GateSequence ([object[]] $timelines, [string] $recoveredTasks) {
	$global:XATimelineSequence = $timelines
	$global:XATimelineSequenceIndex = 0
	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
	$env:SYSTEM_TEAMPROJECT = 'project'
	$env:BUILD_BUILDID = '42'
	$env:SYSTEM_JOBID = $jobId
	function Invoke-RestMethod {
		param ($Headers, $TimeoutSec, $Uri)
		$index = [Math]::Min($global:XATimelineSequenceIndex, $global:XATimelineSequence.Count - 1)
		$global:XATimelineSequenceIndex++
		return $global:XATimelineSequence[$index]
	}
	try {
		return & $gateScript `
			-JobStatus 'SucceededWithIssues' `
			-RecoveredOptionalTaskRefs $recoveredTasks `
			-TimelinePollTimeoutSeconds $timelines.Count `
			-TimelinePollIntervalSeconds 0 6>&1
	} finally {
		Remove-Item Function:\Invoke-RestMethod -ErrorAction SilentlyContinue
		Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID -ErrorAction SilentlyContinue
		Remove-Variable XATimelineSequence, XATimelineSequenceIndex -Scope Global -ErrorAction SilentlyContinue
	}
}

try {
	New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

	$output = Invoke-Gate @(
		(New-Task 'gradleDependenciesCache' 'failed')
	) 'gradleDependenciesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered optional cache failure remained gating.'

	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
	$env:SYSTEM_TEAMPROJECT = 'project'
	$env:BUILD_BUILDID = '42'
	$env:SYSTEM_JOBID = $jobId
	function Invoke-RestMethod {
		param ($Headers, $TimeoutSec, $Uri)
		$env:CAPTURED_AUTHORIZATION = $Headers.Authorization
		$env:CAPTURED_TIMELINE_URI = $Uri
		return New-Timeline @(
				(New-Task 'gradleDependenciesCache' 'failed')
			)
	}
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' 6>&1
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered cache failure from the timeline API remained gating.'
	Assert-True ($env:CAPTURED_AUTHORIZATION -eq 'Bearer test-token') 'The timeline API authorization header was incorrect.'
	Assert-True ($env:CAPTURED_TIMELINE_URI -eq 'https://dev.azure.com/example/project/_apis/build/builds/42/timeline?api-version=7.1') 'The timeline API URI was incorrect.'
	Remove-Item Function:\Invoke-RestMethod
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID, Env:CAPTURED_AUTHORIZATION, Env:CAPTURED_TIMELINE_URI

	$output = Invoke-Gate @(
		@{
			name = 'gradleDependenciesCache'
			parentId = $jobId
			refName = $null
			result = 'failed'
			state = 'completed'
			type = 'Task'
		}
	) 'gradleDependenciesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered task without a timeline refName remained gating.'

	$output = Invoke-Gate @(
		(New-Task 'gradleDependenciesCache' 'failed'),
		(New-Task 'realFailure' 'succeededWithIssues')
	) 'gradleDependenciesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A later genuine issue was masked by cache recovery.'
	Assert-True (($output -join "`n").Contains('realFailure')) 'The unexpected issue task was not diagnosed.'

	$output = Invoke-GateSequence @(
		(New-Timeline @(
			(New-Task 'gradleDependenciesCache' 'failed'),
			@{
				name = 'realFailure'
				parentId = $jobId
				refName = 'realFailure'
				result = $null
				state = 'inProgress'
				type = 'Task'
			}
		)),
		(New-Timeline @(
			(New-Task 'gradleDependenciesCache' 'failed'),
			(New-Task 'realFailure' 'succeededWithIssues')
		))
	) 'gradleDependenciesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'An issue published after the first timeline read was masked.'
	Assert-True (($output -join "`n").Contains('realFailure')) 'The later published issue was not diagnosed.'

	$output = Invoke-GateSequence @(
		(New-Timeline @(
			(New-Task 'gradleDependenciesCache' 'failed'),
			@{
				name = 'eventualSuccess'
				parentId = $jobId
				refName = 'eventualSuccess'
				result = $null
				state = 'inProgress'
				type = 'Task'
			}
		)),
		(New-Timeline @(
			(New-Task 'gradleDependenciesCache' 'failed'),
			(New-Task 'eventualSuccess' 'succeeded')
		))
	) 'gradleDependenciesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A preceding task that became successful remained gating.'

	$cacheTask = New-Task 'gradleDependenciesCache' 'failed'
	$cacheTask.order = 1
	$output = Invoke-GateSequence @(
		@{
			records = @($cacheTask)
		},
		(New-Timeline @(
			(New-Task 'gradleDependenciesCache' 'failed')
		))
	) 'gradleDependenciesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'The gate did not wait for its own timeline record to become visible.'

	$incompleteTimeline = New-Timeline @(
		(New-Task 'gradleDependenciesCache' 'failed'),
		@{
			name = 'neverCompletes'
			parentId = $jobId
			refName = 'neverCompletes'
			result = $null
			state = 'inProgress'
			type = 'Task'
		}
	)
	$output = Invoke-GateSequence @(
		$incompleteTimeline,
		$incompleteTimeline,
		$incompleteTimeline
	) 'gradleDependenciesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A non-terminal preceding task did not fail closed after the poll bound.'
	Assert-True (($output -join "`n").Contains('Timeline completeness was not established')) 'The timeline poll timeout was not diagnosed.'

	$global:XATimelineRequestCount = 0
	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$env:SYSTEM_JOBID = $jobId
	function Invoke-RestMethod {
		$global:XATimelineRequestCount++
		Start-Sleep -Milliseconds 600
		return $incompleteTimeline
	}
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' `
		-TimelinePollTimeoutSeconds 1 `
		-TimelinePollIntervalSeconds 0 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A slow timeline request did not fail closed.'
	Assert-True ($global:XATimelineRequestCount -eq 1) 'Timeline request time was not counted against the polling timeout.'
	Remove-Item Function:\Invoke-RestMethod
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID
	Remove-Variable XATimelineRequestCount -Scope Global

	$output = Invoke-Gate @(
		(New-Task 'realFailure' 'succeededWithIssues')
	) ''
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A job with no recovered-task allowlist did not fail.'

	$output = Invoke-Gate @(
		(New-Task 'unrelatedJobFailure' 'failed' 'other-job')
	) 'gradleDependenciesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A missing recovered task record did not fail safe.'

	$missingTimeline = Join-Path $tempRoot 'missing.json'
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' `
		-JobId $jobId `
		-TimelinePath $missingTimeline `
		-TimelinePollTimeoutSeconds 0 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A timeline read failure did not fail closed.'

	Remove-Item Env:SYSTEM_ACCESSTOKEN -ErrorAction SilentlyContinue
	$env:BUILD_BUILDID = '123'
	$env:SYSTEM_COLLECTIONURI = 'https://example.invalid/'
	$env:SYSTEM_TEAMPROJECT = 'test-project'
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' `
		-JobId $jobId 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A missing Azure access token did not fail closed.'

	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$env:SYSTEM_JOBID = $jobId
	function Invoke-RestMethod {
		throw 'simulated timeline API failure'
	}
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' `
		-TimelinePollTimeoutSeconds 0 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A timeline API failure did not fail closed.'
	Remove-Item Function:\Invoke-RestMethod
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID

	$output = Invoke-Gate @() '' 'Succeeded'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A successful job was failed.'
} finally {
	Remove-Item Env:BUILD_BUILDID -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_COLLECTIONURI -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_TEAMPROJECT -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_JOBID -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_ACCESSTOKEN -ErrorAction SilentlyContinue
	Remove-Item Env:CAPTURED_AUTHORIZATION -ErrorAction SilentlyContinue
	Remove-Item Env:CAPTURED_TIMELINE_URI -ErrorAction SilentlyContinue
	Remove-Item Function:\Invoke-RestMethod -ErrorAction SilentlyContinue
	Remove-Variable XATimelineRequestCount -Scope Global -ErrorAction SilentlyContinue
	Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
