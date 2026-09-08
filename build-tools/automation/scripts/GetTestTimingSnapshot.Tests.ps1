$ErrorActionPreference = 'Stop'
$snapshotScript = Join-Path $PSScriptRoot 'GetTestTimingSnapshot.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) "android-test-timings-$([guid]::NewGuid())"
$originalToken = $env:SYSTEM_ACCESSTOKEN
$script:requests = [Collections.Generic.List[object]]::new()

function Assert-Equal ($Expected, $Actual, [string] $Message) {
	if ($Expected -cne $Actual) {
		throw "${Message}: expected '$Expected', got '$Actual'."
	}
}

function Invoke-RestMethod {
	[CmdletBinding()]
	param ([uri] $Uri, [hashtable] $Headers, [int] $TimeoutSec, [int] $MaximumRedirection)

	Assert-Equal 'Bearer test-token' $Headers.Authorization 'Authentication'
	Assert-Equal 0 $MaximumRedirection 'Do not follow authentication redirects'
	if ($TimeoutSec -lt 1 -or $TimeoutSec -gt 10) {
		throw "Unbounded request timeout: $TimeoutSec"
	}
	$query = @{}
	foreach ($pair in $Uri.Query.TrimStart('?').Split('&')) {
		$parts = $pair.Split('=', 2)
		$query[[uri]::UnescapeDataString($parts[0])] = [uri]::UnescapeDataString($parts[1])
	}
	$script:requests.Add(@{ Path = $Uri.AbsolutePath; Query = $query })
	return & $script:requestHandler $Uri.AbsolutePath $query
}

function Invoke-Snapshot ([int] $TimeBudgetSeconds = 60) {
	$script:requests.Clear()
	$script:messages = @(. $snapshotScript `
		-CollectionUri 'https://dev.azure.com/example/' -Project 'project name' `
		-DefinitionId 333 -OutputDirectory $temp -TimeBudgetSeconds $TimeBudgetSeconds 3>&1 6>&1)
}

function Read-Timings ([string] $Group) {
	return [xml] (Get-Content -LiteralPath (Join-Path $temp "$Group.xml") -Raw)
}

function Assert-EmptySnapshot {
	foreach ($group in @('windows', 'macos', 'macos-device')) {
		$xml = Read-Timings $group
		Assert-Equal 0 $xml.SelectNodes('/tests/test').Count "$group must fall back to count-based slicing"
	}
}

function Assert-FallbackWarning {
	if (-not ($script:messages | Where-Object { $_ -is [Management.Automation.WarningRecord] -and "$_" -like '*count-based*' })) {
		throw 'Missing visible count-based fallback warning.'
	}
}

function New-Result ([string] $Name, [double] $Duration, [string] $Outcome = 'Passed') {
	return @{ automatedTestName = $Name; durationInMs = $Duration; outcome = $Outcome }
}

function Save-SlicerArguments {
	$script:slicerArguments = @($args)
}

try {
	$env:SYSTEM_ACCESSTOKEN = 'test-token'
	$script:requestHandler = {
		param ($Path, $Query)
		Assert-Equal '/example/project%20name/_apis/build/builds' $Path 'Collection/project URL'
		Assert-Equal '333' $Query.definitions 'Current pipeline only'
		Assert-Equal 'refs/heads/main' $Query.branchName 'Trusted main branch only'
		Assert-Equal 'succeeded' $Query.resultFilter 'Successful builds only'
		Assert-Equal 'completed' $Query.statusFilter 'Completed builds only'
		Assert-Equal 'finishTimeDescending' $Query.queryOrder 'Newest completed build'
		Assert-Equal '1' $Query['$top'] 'Resolve baseline once'
		return @{ value = @() }
	}
	Invoke-Snapshot
	Assert-EmptySnapshot
	Assert-Equal 1 $script:requests.Count 'No requests after missing history'

	$script:escapedName = 'Example.Fixture("a&b").Test("<value>")'
	$script:requestHandler = {
		param ($Path, $Query)
		switch -Wildcard ($Path) {
			'*/build/builds' { return @{ value = @(@{ id = 42 }) } }
			'*/test/runs' {
				Assert-Equal 'vstfs:///Build/Build/42' $Query.buildUri 'One immutable baseline build'
				return @{ value = @(
					@{ id = 1; name = 'Xamarin.Android.Build.Tests - Windows-1'; state = 'Completed' },
					@{ id = 2; name = 'Xamarin.Android.Build.Tests - Windows-2'; state = 'Completed' },
					@{ id = 3; name = 'Xamarin.Android.Build.Tests - macOS-1'; state = 'Completed' },
					@{ id = 4; name = 'MSBuildDeviceIntegration On Device - macOS-1'; state = 'Completed' },
					@{ id = 5; name = 'MSBuildDeviceIntegration On Device - macOS-1 (Auto-Retry)'; state = 'Completed' },
					@{ id = 6; name = 'Xamarin.Android.Build.Tests - Linux BuildTest'; state = 'Completed' },
					@{ id = 7; name = 'Xamarin.Android.Build.Tests - Windows-3'; state = 'InProgress' }
				) }
			}
			'*/Runs/1/results' {
				Assert-Equal 'Passed' $Query.outcomes 'Passed durations only'
				return @{ value = @(
					(New-Result $script:escapedName 100.5),
					(New-Result 'Example.Test' 500),
					(New-Result 'Example.test' 800),
					(New-Result 'Example.Zero' 0),
					(New-Result 'Example.Failed' 999999 'Failed'),
					(New-Result 'Example.Skipped' 0 'NotExecuted')
				) }
			}
			'*/Runs/2/results' { return @{ value = @((New-Result $script:escapedName 300.5)) } }
			'*/Runs/3/results' { return @{ value = @((New-Result 'Example.Test' 50)) } }
			'*/Runs/4/results' { return @{ value = @((New-Result 'Example.Test' 1000)) } }
			'*/Runs/5/results' { return @{ value = @((New-Result 'Example.Retried' 2000)) } }
			default { throw "Unexpected request: $Path" }
		}
	}
	Invoke-Snapshot
	Assert-Equal 7 $script:requests.Count 'Ignore unrelated and unfinished runs'
	$windows = Read-Timings 'windows'
	Assert-Equal 4 $windows.tests.test.Count 'Unique passed Windows cases'
	$escaped = $windows.tests.test | Where-Object { $_.name -ceq $script:escapedName }
	Assert-Equal '200' $escaped.duration 'Average duplicate executions, truncated to milliseconds'
	Assert-Equal '1' ($windows.tests.test | Where-Object { $_.name -ceq 'Example.Zero' }).duration 'Submillisecond tests have nonzero weight'
	Assert-Equal '500' ($windows.tests.test | Where-Object { $_.name -ceq 'Example.Test' }).duration 'Case-sensitive identity'
	Assert-Equal '800' ($windows.tests.test | Where-Object { $_.name -ceq 'Example.test' }).duration 'Distinct case-sensitive identity'
	Assert-Equal '50' (Read-Timings 'macos').tests.test.duration 'macOS timings stay separate'
	Assert-Equal 2 (Read-Timings 'macos-device').tests.test.Count 'Include successful device retries'
	$firstSnapshot = Get-Content -LiteralPath (Join-Path $temp 'windows.xml') -Raw
	Invoke-Snapshot
	Assert-Equal $firstSnapshot (Get-Content -LiteralPath (Join-Path $temp 'windows.xml') -Raw) 'Deterministic snapshot'

	$script:requestHandler = {
		param ($Path, $Query)
		switch -Wildcard ($Path) {
			'*/build/builds' { return @{ value = @(@{ id = 42 }) } }
			'*/test/runs' {
				if ($Query['$skip'] -eq '0') {
					return @{ value = @(1..100 | ForEach-Object { @{ id = $_; name = 'Unrelated'; state = 'Completed' } }) }
				}
				Assert-Equal '100' $Query['$skip'] 'Run pagination'
				return @{ value = @(@{ id = 101; name = 'Xamarin.Android.Build.Tests - Windows-8'; state = 'Completed' }) }
			}
			'*/Runs/101/results' {
				if ($Query['$skip'] -eq '0') {
					return @{ value = @(1..1000 | ForEach-Object { New-Result "Example.Test$_" $_ }) }
				}
				Assert-Equal '1000' $Query['$skip'] 'Result pagination'
				return @{ value = @((New-Result 'Example.LastTest' 1001)) }
			}
			default { throw "Unexpected request: $Path" }
		}
	}
	Invoke-Snapshot
	Assert-Equal 1001 (Read-Timings 'windows').tests.test.Count 'Every result page is consumed'
	Assert-Equal 5 $script:requests.Count 'Every run and result page is consumed'

	foreach ($failure in @('Http', 'Forbidden', 'Canceled', 'Budget')) {
		$script:failure = $failure
		$script:requestHandler = {
			param ($Path, $Query)
			switch -Wildcard ($Path) {
				'*/build/builds' { return @{ value = @(@{ id = 42 }) } }
				'*/test/runs' {
					return @{ value = @(
						@{ id = 1; name = 'Xamarin.Android.Build.Tests - Windows-1'; state = 'Completed' },
						@{ id = 2; name = 'Xamarin.Android.Build.Tests - macOS-1'; state = 'Completed' }
					) }
				}
				'*/Runs/1/results' { return @{ value = @((New-Result 'Example.Partial' 100)) } }
				'*/Runs/2/results' {
					switch ($script:failure) {
						'Http' { throw [Net.Http.HttpRequestException]::new('Service unavailable') }
						'Forbidden' {
							$response = [Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::Forbidden)
							throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('Forbidden', $response)
						}
						'Canceled' { throw [Threading.Tasks.TaskCanceledException]::new('Request timed out') }
						'Budget' { throw [TimeoutException]::new('Budget exhausted') }
					}
				}
				default { throw "Unexpected request: $Path" }
			}
		}
		Invoke-Snapshot
		Assert-EmptySnapshot
		Assert-FallbackWarning
		if ($failure -eq 'Forbidden' -and -not ($script:messages | Where-Object { "$_" -like '*HTTP 403*' })) {
			throw 'The fallback warning must explain permission failures.'
		}
	}

	$script:requestHandler = {
		Start-Sleep -Milliseconds 1100
		return @{ value = @(@{ id = 42 }) }
	}
	Invoke-Snapshot -TimeBudgetSeconds 1
	Assert-EmptySnapshot
	Assert-FallbackWarning
	Assert-Equal 1 $script:requests.Count 'Stop requesting pages when the total time budget expires'

	$env:SYSTEM_ACCESSTOKEN = ''
	Invoke-Snapshot
	Assert-EmptySnapshot
	Assert-Equal 0 $script:requests.Count 'No network access without a token'
	Assert-FallbackWarning
	$env:SYSTEM_ACCESSTOKEN = 'test-token'

	$script:requestHandler = { return @{ unexpected = 'response' } }
	$invalidResponseRejected = $false
	try {
		Invoke-Snapshot
	} catch [IO.InvalidDataException] {
		$invalidResponseRejected = $true
	}
	Assert-Equal $true $invalidResponseRejected 'Malformed responses must not be silently accepted'

	foreach ($invalidDuration in @(-1, [double]::NaN, [double]::PositiveInfinity, ([double][int]::MaxValue + 1))) {
		$script:invalidDuration = $invalidDuration
		$script:requestHandler = {
			param ($Path, $Query)
			switch -Wildcard ($Path) {
				'*/build/builds' { return @{ value = @(@{ id = 42 }) } }
				'*/test/runs' {
					return @{ value = @(@{ id = 1; name = 'Xamarin.Android.Build.Tests - Windows-1'; state = 'Completed' }) }
				}
				'*/Runs/1/results' { return @{ value = @((New-Result 'Example.Invalid' $script:invalidDuration)) } }
				default { throw "Unexpected request: $Path" }
			}
		}
		$invalidDurationRejected = $false
		try {
			Invoke-Snapshot
		} catch [IO.InvalidDataException] {
			$invalidDurationRejected = $true
		}
		Assert-Equal $true $invalidDurationRejected "Reject invalid duration $invalidDuration"
	}

	$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../yaml-templates/run-sliced-nunit-tests.yaml') -Raw
	$match = [regex]::Match($template, '(?m)^- pwsh: \|\r?\n((?:    .*\r?\n|\r?\n)+)')
	Assert-Equal $true $match.Success 'Find the slicer command in the pipeline template'
	$command = [regex]::Replace($match.Groups[1].Value, '(?m)^    ', '')
	$command = $command.Replace('"$(Agent.ToolsDirectory)/dotnet-test-slicer"', "'Save-SlicerArguments'")
	$command = $command.Replace('${{ parameters.testAssembly }}', 'path with spaces/test.dll')
	$command = $command.Replace('$(System.JobPositionInPhase)', '2').Replace('$(System.TotalJobsInPhase)', '8')
	foreach ($balanceFile in @('', 'path with spaces/windows.xml')) {
		foreach ($filter in @('', 'cat != Excluded')) {
			$rendered = $command.Replace('${{ parameters.balanceFile }}', $balanceFile).Replace('${{ parameters.testFilter }}', $filter)
			& ([scriptblock]::Create($rendered))
			Assert-Equal 'slice' $script:slicerArguments[0] 'Slicer verb'
			Assert-Equal '--test-assembly=path with spaces/test.dll' $script:slicerArguments[1] 'Preserve paths with spaces'
			Assert-Equal '--slice-number=2' $script:slicerArguments[2] 'Preserve slice position'
			Assert-Equal '--total-slices=8' $script:slicerArguments[3] 'Preserve agent count'
			$balanceArguments = @($script:slicerArguments | Where-Object { $_ -like '--balance-file=*' })
			Assert-Equal ([int][bool]$balanceFile) $balanceArguments.Count 'Do not pass an empty balance argument'
			if ($balanceFile) {
				Assert-Equal "--balance-file=$balanceFile" $balanceArguments[0] 'Shared timing snapshot argument'
			}
			$filterArguments = @($script:slicerArguments | Where-Object { $_ -like '--test-filter=*' })
			Assert-Equal ([int][bool]$filter) $filterArguments.Count 'Preserve optional filtering'
			if ($filter) {
				Assert-Equal "--test-filter=$filter" $filterArguments[0] 'NUnit filter argument'
			}
		}
	}

	Write-Host 'Test timing snapshot tests passed.'
} finally {
	$env:SYSTEM_ACCESSTOKEN = $originalToken
	Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction Ignore
}
