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
	@{ records = $records } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $timelinePath -Encoding ASCII
	return & $gateScript `
		-JobStatus $jobStatus `
		-RecoveredOptionalTaskRefs $recoveredTasks `
		-JobId $jobId `
		-TimelinePath $timelinePath 6>&1
}

function New-Task ([string] $refName, [string] $result, [string] $parentId = $jobId) {
	return @{
		name = $refName
		parentId = $parentId
		refName = $refName
		result = $result
		type = 'Task'
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
		param ($Headers, $Method, $Uri)
		$env:CAPTURED_AUTHORIZATION = $Headers.Authorization
		$env:CAPTURED_TIMELINE_URI = $Uri
		return @{
			records = @(
				(New-Task 'gradleDependenciesCache' 'failed')
			)
		}
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
		-TimelinePath $missingTimeline 6>&1
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
		-RecoveredOptionalTaskRefs 'gradleDependenciesCache' 6>&1
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
	Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
