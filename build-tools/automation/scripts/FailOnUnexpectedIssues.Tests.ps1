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
		-TimelinePollTimeoutSeconds 1 6>&1
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
	$timelinePaths = [System.Collections.Generic.List[string]]::new()
	foreach ($timeline in $timelines) {
		$timelinePath = Join-Path $tempRoot "$([Guid]::NewGuid()).json"
		$timeline | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $timelinePath -Encoding ASCII
		$timelinePaths.Add($timelinePath)
	}
	return & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs $recoveredTasks `
		-JobId $jobId `
		-TimelinePath $timelinePaths.ToArray() `
		-TimelinePollTimeoutSeconds $timelines.Count `
		-TimelinePollIntervalSeconds 0 6>&1
}

function Get-FreeTcpPort {
	$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
	try {
		$listener.Start()
		return ([Net.IPEndPoint] $listener.LocalEndpoint).Port
	} finally {
		$listener.Stop()
	}
}

function Start-TestHttpServer ([string] $responseBody, [int] $bodyDelayMilliseconds = 0) {
	$port = Get-FreeTcpPort
	$readyPath = Join-Path $tempRoot "$([Guid]::NewGuid()).ready"
	$requestPath = Join-Path $tempRoot "$([Guid]::NewGuid()).request"
	$job = Start-Job -ScriptBlock {
		param ($port, $readyPath, $requestPath, $responseBody, $bodyDelayMilliseconds)
		$ErrorActionPreference = 'Stop'
		$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
		try {
			$listener.Start()
			Set-Content -LiteralPath $readyPath -Value 'ready' -Encoding ASCII
			$client = $listener.AcceptTcpClient()
			try {
				$stream = $client.GetStream()
				$reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
				$requestLines = [System.Collections.Generic.List[string]]::new()
				while ($true) {
					$line = $reader.ReadLine()
					if ([string]::IsNullOrEmpty($line)) {
						break
					}
					$requestLines.Add($line)
				}
				$requestLines | Set-Content -LiteralPath $requestPath -Encoding ASCII

				$bodyBytes = [Text.Encoding]::UTF8.GetBytes($responseBody)
				$headers = "HTTP/1.1 200 OK`r`nContent-Type: application/json`r`nContent-Length: $($bodyBytes.Length)`r`nConnection: close`r`n`r`n"
				$headerBytes = [Text.Encoding]::ASCII.GetBytes($headers)
				$stream.Write($headerBytes, 0, $headerBytes.Length)
				$stream.Flush()
				if ($bodyDelayMilliseconds -gt 0) {
					Start-Sleep -Milliseconds $bodyDelayMilliseconds
				}
				$stream.Write($bodyBytes, 0, $bodyBytes.Length)
				$stream.Flush()
			} finally {
				if ($null -ne $reader) {
					$reader.Dispose()
				}
				if ($null -ne $client) {
					$client.Dispose()
				}
			}
		} finally {
			$listener.Stop()
		}
	} -ArgumentList $port, $readyPath, $requestPath, $responseBody, $bodyDelayMilliseconds

	$readyDeadline = [DateTime]::UtcNow.AddSeconds(10)
	while (-not (Test-Path -LiteralPath $readyPath) -and [DateTime]::UtcNow -lt $readyDeadline) {
		if ($job.State -in @('Failed', 'Stopped', 'Completed')) {
			break
		}
		Start-Sleep -Milliseconds 50
	}
	if (-not (Test-Path -LiteralPath $readyPath)) {
		$jobOutput = Receive-Job -Job $job 2>&1
		Remove-Job -Job $job -Force
		throw "The test HTTP server did not start: $($jobOutput -join "`n")"
	}

	return @{
		Job = $job
		Port = $port
		RequestPath = $requestPath
	}
}

function Stop-TestHttpServer ($server) {
	$serverFailed = $server.Job.State -eq 'Failed'
	if ($server.Job.State -notin @('Completed', 'Failed', 'Stopped')) {
		Stop-Job -Job $server.Job
	}
	$serverOutput = Receive-Job -Job $server.Job 2>&1
	Remove-Job -Job $server.Job -Force
	if ($serverFailed) {
		throw "The test HTTP server failed: $($serverOutput -join "`n")"
	}
}

try {
	New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

	$output = Invoke-Gate @(
		(New-Task 'androidArchivesCache' 'failed')
	) 'androidArchivesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered optional cache failure remained gating.'

	$timelineJson = New-Timeline @(
		(New-Task 'androidArchivesCache' 'failed')
	) | ConvertTo-Json -Depth 5 -Compress
	$server = Start-TestHttpServer $timelineJson
	try {
		$env:SYSTEM_ACCESSTOKEN = 'test-token'
		$env:SYSTEM_COLLECTIONURI = "http://127.0.0.1:$($server.Port)/"
		$env:SYSTEM_TEAMPROJECT = 'project'
		$env:BUILD_BUILDID = '42'
		$env:SYSTEM_JOBID = $jobId
		$output = & $gateScript `
			-JobStatus 'SucceededWithIssues' `
			-RecoveredOptionalTaskRefs 'androidArchivesCache' `
			-TimelinePollTimeoutSeconds 5 6>&1
		$requestLines = Get-Content -LiteralPath $server.RequestPath
	} finally {
		Stop-TestHttpServer $server
	}
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered cache failure from the timeline API remained gating.'
	Assert-True ($requestLines[0] -eq 'GET /project/_apis/build/builds/42/timeline?api-version=7.1 HTTP/1.1') 'The timeline API URI was incorrect.'
	Assert-True ($requestLines -contains 'Authorization: Bearer test-token') 'The timeline API authorization header was incorrect.'
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID

	$output = Invoke-Gate @(
		@{
			name = 'androidArchivesCache'
			parentId = $jobId
			refName = $null
			result = 'failed'
			state = 'completed'
			type = 'Task'
		}
	) 'androidArchivesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A recovered task without a timeline refName remained gating.'

	$output = Invoke-Gate @(
		(New-Task 'androidArchivesCache' 'failed'),
		(New-Task 'realFailure' 'succeededWithIssues')
	) 'androidArchivesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A later genuine issue was masked by cache recovery.'
	Assert-True (($output -join "`n").Contains('realFailure')) 'The unexpected issue task was not diagnosed.'

	$output = Invoke-Gate @(
		(New-Task 'androidArchivesCache' 'failed'),
		(New-Task 'unknownResult' 'futureResult')
	) 'androidArchivesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'An unknown completed task result did not fail closed.'

	$output = Invoke-GateSequence @(
		(New-Timeline @(
			(New-Task 'androidArchivesCache' 'failed'),
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
			(New-Task 'androidArchivesCache' 'failed'),
			(New-Task 'realFailure' 'succeededWithIssues')
		))
	) 'androidArchivesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'An issue published after the first timeline read was masked.'
	Assert-True (($output -join "`n").Contains('realFailure')) 'The later published issue was not diagnosed.'

	$output = Invoke-GateSequence @(
		(New-Timeline @(
			(New-Task 'androidArchivesCache' 'failed'),
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
			(New-Task 'androidArchivesCache' 'failed'),
			(New-Task 'eventualSuccess' 'succeeded')
		))
	) 'androidArchivesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A preceding task that became successful remained gating.'

	$cacheTask = New-Task 'androidArchivesCache' 'failed'
	$cacheTask.order = 1
	$output = Invoke-GateSequence @(
		@{
			records = @($cacheTask)
		},
		(New-Timeline @(
			(New-Task 'androidArchivesCache' 'failed')
		))
	) 'androidArchivesCache'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'The gate did not wait for its own timeline record to become visible.'

	$incompleteTimeline = New-Timeline @(
		(New-Task 'androidArchivesCache' 'failed'),
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
	) 'androidArchivesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A non-terminal preceding task did not fail closed after the poll bound.'
	Assert-True (($output -join "`n").Contains('Timeline completeness was not established')) 'The timeline poll timeout was not diagnosed.'

	$server = Start-TestHttpServer $timelineJson 3000
	try {
		$env:SYSTEM_ACCESSTOKEN = 'test-token'
		$env:SYSTEM_COLLECTIONURI = "http://127.0.0.1:$($server.Port)/"
		$env:SYSTEM_TEAMPROJECT = 'project'
		$env:BUILD_BUILDID = '42'
		$env:SYSTEM_JOBID = $jobId
		$deadlineStopwatch = [Diagnostics.Stopwatch]::StartNew()
		$output = & $gateScript `
			-JobStatus 'SucceededWithIssues' `
			-RecoveredOptionalTaskRefs 'androidArchivesCache' `
			-TimelinePollTimeoutSeconds 1 `
			-TimelinePollIntervalSeconds 0 6>&1
		$deadlineStopwatch.Stop()
	} finally {
		Stop-TestHttpServer $server
	}
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A response body completed after the deadline did not fail closed.'
	Assert-True ($deadlineStopwatch.Elapsed.TotalSeconds -lt 2.5) 'The response-body read exceeded the timeline polling deadline tolerance.'
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID

	$output = Invoke-Gate @(
		(New-Task 'realFailure' 'succeededWithIssues')
	) ''
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A job with no recovered-task allowlist did not fail.'

	$output = Invoke-Gate @(
		(New-Task 'unrelatedJobFailure' 'failed' 'other-job')
	) 'androidArchivesCache'
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A missing recovered task record did not fail safe.'

	$missingTimeline = Join-Path $tempRoot 'missing.json'
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'androidArchivesCache' `
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
		-RecoveredOptionalTaskRefs 'androidArchivesCache' `
		-JobId $jobId 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A missing Azure access token did not fail closed.'

	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$env:SYSTEM_COLLECTIONURI = "http://127.0.0.1:$(Get-FreeTcpPort)/"
	$env:SYSTEM_TEAMPROJECT = 'project'
	$env:BUILD_BUILDID = '42'
	$env:SYSTEM_JOBID = $jobId
	$output = & $gateScript `
		-JobStatus 'SucceededWithIssues' `
		-RecoveredOptionalTaskRefs 'androidArchivesCache' `
		-TimelinePollTimeoutSeconds 1 6>&1
	Assert-True (($output -join "`n").Contains('result=Failed')) 'A timeline API failure did not fail closed.'
	Remove-Item Env:SYSTEM_ACCESSTOKEN, Env:SYSTEM_JOBID

	$output = Invoke-Gate @() '' 'Succeeded'
	Assert-True (-not ($output -join "`n").Contains('result=Failed')) 'A successful job was failed.'
} finally {
	Remove-Item Env:BUILD_BUILDID -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_COLLECTIONURI -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_TEAMPROJECT -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_JOBID -ErrorAction SilentlyContinue
	Remove-Item Env:SYSTEM_ACCESSTOKEN -ErrorAction SilentlyContinue
	Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
