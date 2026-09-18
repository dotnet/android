param (
	[string] $JobStatus = $env:AGENT_JOBSTATUS,
	[string] $RecoveredOptionalTaskRefs = $env:RECOVERED_OPTIONAL_TASK_REFS,
	[string] $JobId = $env:SYSTEM_JOBID,
	[string[]] $TimelinePath,
	[ValidateRange(0, 300)]
	[int] $TimelinePollTimeoutSeconds = 20,
	[ValidateRange(0, 60)]
	[int] $TimelinePollIntervalSeconds = 2
)

$ErrorActionPreference = 'Stop'
$gateTaskReferenceName = 'failOnUnexpectedIssues'
$validTaskResults = @('succeeded', 'succeededWithIssues', 'failed', 'canceled', 'skipped', 'abandoned')
Write-Host "Current job status is: $JobStatus"

function Complete-AsFailed ([string] $message) {
	Write-Host $message
	Write-Host '##vso[task.complete result=Failed;]DONE'
}

function Get-TaskReferenceName ($task) {
	if (-not [string]::IsNullOrWhiteSpace($task.refName)) {
		return $task.refName
	}
	return $task.name
}

function Get-TimelineFromApi ([string] $uri, [string] $accessToken) {
	$client = [Net.Http.HttpClient]::new()
	$request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $uri)
	$cancellation = [Threading.CancellationTokenSource]::new()
	$response = $null
	try {
		$client.Timeout = [Threading.Timeout]::InfiniteTimeSpan
		$request.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $accessToken)
		$remainingMilliseconds = [Math]::Floor(
			($TimelinePollTimeoutSeconds - $timelineStopwatch.Elapsed.TotalSeconds) * 1000
		)
		if ($remainingMilliseconds -le 0) {
			throw [TimeoutException]::new('The timeline polling deadline elapsed before the request started.')
		}
		$cancellation.CancelAfter([int] $remainingMilliseconds)

		$response = $client.SendAsync(
			$request,
			[Net.Http.HttpCompletionOption]::ResponseContentRead,
			$cancellation.Token
		).GetAwaiter().GetResult()
		if (-not $response.IsSuccessStatusCode) {
			throw "The timeline request failed with HTTP status $([int] $response.StatusCode) ($($response.ReasonPhrase))."
		}

		$content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
		return $content | ConvertFrom-Json
	} finally {
		if ($null -ne $response) {
			$response.Dispose()
		}
		$cancellation.Dispose()
		$request.Dispose()
		$client.Dispose()
	}
}

if ($JobStatus -ne 'SucceededWithIssues') {
	return
}

$allowedTasks = @($RecoveredOptionalTaskRefs -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($allowedTasks.Count -eq 0) {
	Complete-AsFailed 'No recovered optional tasks were recorded.'
	return
}

$hasTimelineFixture = $null -ne $TimelinePath -and $TimelinePath.Count -ne 0
try {
	if (-not $hasTimelineFixture) {
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
		Add-Type -AssemblyName System.Net.Http
	}
} catch {
	Complete-AsFailed "Could not prepare the Azure Pipelines timeline request: $($_.Exception.Message)"
	return
}

$maxTimelineAttempts = if ($TimelinePollIntervalSeconds -eq 0) {
	$TimelinePollTimeoutSeconds + 1
} else {
	[Math]::Floor($TimelinePollTimeoutSeconds / $TimelinePollIntervalSeconds) + 1
}
$precedingTasks = $null
$timelineStatus = 'The gate task was not present in the current-job timeline.'
$timelineStopwatch = [Diagnostics.Stopwatch]::StartNew()

for ($attempt = 1; $attempt -le $maxTimelineAttempts; $attempt++) {
	$remainingMilliseconds = [Math]::Floor(
		($TimelinePollTimeoutSeconds - $timelineStopwatch.Elapsed.TotalSeconds) * 1000
	)
	if ($remainingMilliseconds -le 0) {
		break
	}

	try {
		if ($hasTimelineFixture) {
			$timelinePathIndex = [Math]::Min($attempt - 1, $TimelinePath.Count - 1)
			$timeline = Get-Content -LiteralPath $TimelinePath[$timelinePathIndex] -Raw | ConvertFrom-Json
		} else {
			$timeline = Get-TimelineFromApi $timelineUri $env:SYSTEM_ACCESSTOKEN
		}

		if ($null -eq $timeline -or $null -eq $timeline.records) {
			throw 'The timeline response does not contain records.'
		}

		$currentJobTasks = @($timeline.records | Where-Object {
			$_.parentId -eq $JobId -and $_.type -eq 'Task'
		})
		$gateTasks = @($currentJobTasks | Where-Object {
			(Get-TaskReferenceName $_) -eq $gateTaskReferenceName
		})
		if ($gateTasks.Count -gt 1) {
			throw "The current-job timeline contains multiple '$gateTaskReferenceName' task records."
		}
		if ($gateTasks.Count -eq 1) {
			$gateOrder = 0
			if (-not [int]::TryParse([string] $gateTasks[0].order, [ref] $gateOrder) -or $gateOrder -lt 1) {
				throw "The '$gateTaskReferenceName' task has an invalid timeline order."
			}

			$taskOrders = [System.Collections.Generic.List[int]]::new()
			foreach ($task in $currentJobTasks) {
				$taskOrder = 0
				if (-not [int]::TryParse([string] $task.order, [ref] $taskOrder) -or $taskOrder -lt 1) {
					throw "Task '$(Get-TaskReferenceName $task)' has an invalid timeline order."
				}
				$taskOrders.Add($taskOrder)
			}
			if (@($taskOrders | Sort-Object -Unique).Count -ne $taskOrders.Count) {
				throw 'The current-job timeline contains duplicate task order values.'
			}

			$visiblePrecedingTasks = @($currentJobTasks | Where-Object {
				[int] $_.order -lt $gateOrder
			})
			if ($visiblePrecedingTasks.Count -ne $gateOrder - 1) {
				$timelineStatus = "Only $($visiblePrecedingTasks.Count) of $($gateOrder - 1) preceding task records are visible."
			} else {
				$incompleteTasks = @($visiblePrecedingTasks | Where-Object {
					$_.state -ne 'completed' -or [string]::IsNullOrWhiteSpace($_.result)
				})
				if ($incompleteTasks.Count -eq 0) {
					$invalidResultTasks = @($visiblePrecedingTasks | Where-Object {
						$_.result -notin $validTaskResults
					})
					if ($invalidResultTasks.Count -ne 0) {
						$invalidTask = $invalidResultTasks[0]
						throw "Task '$(Get-TaskReferenceName $invalidTask)' has unsupported result '$($invalidTask.result)'."
					}
					if ($timelineStopwatch.Elapsed.TotalSeconds -le $TimelinePollTimeoutSeconds) {
						$precedingTasks = $visiblePrecedingTasks
						break
					}
					$timelineStatus = 'The complete timeline was returned after the polling timeout.'
				}

				if ($incompleteTasks.Count -ne 0) {
					$incompleteNames = @($incompleteTasks | ForEach-Object {
						"'$(Get-TaskReferenceName $_)' ($($_.state), $($_.result))"
					})
					$timelineStatus = "Preceding task records are not terminal: $($incompleteNames -join ', ')."
				}
			}
		}
	} catch {
		$timelineStatus = "Could not inspect the Azure Pipelines timeline: $($_.Exception.Message)"
	}

	if ($attempt -lt $maxTimelineAttempts) {
		Write-Host "$timelineStatus Retrying timeline inspection ($attempt/$maxTimelineAttempts)."
		$remainingMilliseconds = [Math]::Floor(
			($TimelinePollTimeoutSeconds - $timelineStopwatch.Elapsed.TotalSeconds) * 1000
		)
		if ($remainingMilliseconds -le 0) {
			break
		}
		$sleepMilliseconds = [Math]::Min($TimelinePollIntervalSeconds * 1000, $remainingMilliseconds)
		if ($sleepMilliseconds -gt 0) {
			Start-Sleep -Milliseconds $sleepMilliseconds
		}
	}
}

if ($null -eq $precedingTasks) {
	Complete-AsFailed "$timelineStatus Timeline completeness was not established within $TimelinePollTimeoutSeconds seconds."
	return
}

$issueTasks = @($precedingTasks | Where-Object {
	$_.result -in @('failed', 'succeededWithIssues')
})
$unexpectedTasks = @($issueTasks | Where-Object {
	(Get-TaskReferenceName $_) -notin $allowedTasks
})

if ($issueTasks.Count -eq 0 -or $unexpectedTasks.Count -ne 0) {
	foreach ($task in $unexpectedTasks) {
		$taskReferenceName = Get-TaskReferenceName $task
		Write-Host "Unexpected issue task: $($task.name) ($taskReferenceName, result $($task.result))"
	}
	Complete-AsFailed 'The current job contains an unexpected issue.'
	return
}

foreach ($task in $issueTasks) {
	$taskReferenceName = Get-TaskReferenceName $task
	Write-Host "Ignoring recovered optional task: $($task.name) ($taskReferenceName)"
}
