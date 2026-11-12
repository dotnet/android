$ErrorActionPreference = 'Stop'
$environmentScript = Join-Path $PSScriptRoot 'ConfigureMauiR2RHelixEnvironment.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

$childScript = Join-Path ([IO.Path]::GetTempPath()) "$([IO.Path]::GetRandomFileName()).ps1"
try {
	@'
param (
	[string] $ExpectAccessToken
)

$hasAccessToken = Test-Path Env:SYSTEM_ACCESSTOKEN
if ($hasAccessToken -ne [Convert]::ToBoolean($ExpectAccessToken)) {
	Write-Error "Expected inherited SYSTEM_ACCESSTOKEN presence to be '$ExpectAccessToken', but it was '$hasAccessToken'."
	exit 1
}
'@ | Set-Content -LiteralPath $childScript -Encoding ASCII

	$testCases = @(
		@{ IsFork = 'True'; ExpectAccessToken = 'False' },
		@{ IsFork = 'False'; ExpectAccessToken = 'True' }
	)

	foreach ($testCase in $testCases) {
		$env:SYSTEM_PULLREQUEST_ISFORK = $testCase.IsFork
		$env:SYSTEM_ACCESSTOKEN = 'test-token'
		. $environmentScript

		& $powerShellExe -NoLogo -NoProfile -File $childScript $testCase.ExpectAccessToken
		if ($LASTEXITCODE -ne 0) {
			throw "MAUI R2R Helix environment test failed for fork value '$($testCase.IsFork)'."
		}
	}
} finally {
	Remove-Item -LiteralPath $childScript -Force -ErrorAction Ignore
}

. (Join-Path $PSScriptRoot 'InvokeMauiR2RHelixSubmissions.ps1')
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) "Maui R2R Helix $([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$launchedIds = New-Object 'Collections.Generic.List[int]'
$allLaunchedIds = New-Object 'Collections.Generic.List[int]'

# Keep the actual launch IDs even when the fixture is stopped before it writes a PID file.
function Start-Process {
	param (
		[string] $FilePath,
		[string[]] $ArgumentList,
		[string] $RedirectStandardOutput,
		[string] $RedirectStandardError,
		[switch] $PassThru
	)
	$process = Microsoft.PowerShell.Management\Start-Process @PSBoundParameters
	$launchedIds.Add($process.Id)
	$allLaunchedIds.Add($process.Id)
	return $process
}

function New-HelixTestSubmission([string] $name, [string] $script) {
	$scriptPath = Join-Path $testDirectory "$name.ps1"
	Set-Content -LiteralPath $scriptPath -Value $script -Encoding ASCII
	return [pscustomobject]@{
		Name = $name
		DisplayName = $name
		ScriptPath = $scriptPath
		OutputPath = Join-Path $testDirectory "$name.out.log"
		ErrorPath = Join-Path $testDirectory "$name.err.log"
	}
}

function Assert-HelixFixtureExited([int] $processId) {
	try {
		$process = [Diagnostics.Process]::GetProcessById($processId)
	} catch [ArgumentException] {
		return
	}
	try {
		if (-not $process.WaitForExit(5000)) {
			throw "Fixture process $processId is still running."
		}
	} finally {
		$process.Dispose()
	}
}

try {
	$first = New-HelixTestSubmission 'first result' "[Console]::Out.Write('first stdout'); [Console]::Error.Write('first stderr'); exit 7"
	$last = New-HelixTestSubmission 'last result' "Start-Sleep -Milliseconds 100; [Console]::Out.Write('last stdout'); exit 0"
	$failure = $null
	try {
		# Expected failures must not emit Azure task errors from this regression step.
		Invoke-MauiR2RHelixSubmissions -Submissions @($first, $last) -Timeout ([TimeSpan]::FromSeconds(10)) 6>&1 | Out-Null
	} catch {
		$failure = $_
	}
	if (-not $failure -or $failure.Exception.Message -notlike '*Failed MAUI R2R Helix submissions: first result*') {
		throw "Expected the first submission's nonzero exit code: $failure"
	}
	if ($launchedIds.Count -ne 2 -or
		(Get-Content -LiteralPath $first.ErrorPath -Raw).TrimEnd("`r", "`n") -ne 'first stderr' -or
		(Get-Content -LiteralPath $last.OutputPath -Raw).TrimEnd("`r", "`n") -ne 'last stdout') {
		throw "The submission owner did not observe every result and preserve both diagnostic files (launched $($launchedIds.Count))."
	}
	foreach ($processId in $launchedIds) {
		Assert-HelixFixtureExited $processId
	}

	$launchedIds.Clear()
	$running = New-HelixTestSubmission 'partial launch' 'Start-Sleep -Seconds 60'
	$invalid = New-HelixTestSubmission 'invalid launch' 'exit 0'
	$invalid.OutputPath = Join-Path (Join-Path $testDirectory 'missing') 'output.log'
	$failure = $null
	try {
		Invoke-MauiR2RHelixSubmissions -Submissions @($running, $invalid) -Timeout ([TimeSpan]::FromSeconds(10)) 6>&1 | Out-Null
	} catch {
		$failure = $_
	}
	if (-not $failure -or $launchedIds.Count -ne 1) {
		throw "Expected a launch failure after one owned worker was started: $failure"
	}
	Assert-HelixFixtureExited $launchedIds[0]

	$launchedIds.Clear()
	$failure = $null
	function Write-Host([object] $Object) {
		if ([string]$Object -like 'Waiting for MAUI R2R Helix submissions:*') {
			throw 'Injected progress logging failure.'
		}
		Microsoft.PowerShell.Utility\Write-Host $Object
	}
	try {
		Invoke-MauiR2RHelixSubmissions -Submissions @($running) -Timeout ([TimeSpan]::FromSeconds(10)) 6>&1 | Out-Null
	} catch {
		$failure = $_
	} finally {
		Remove-Item Function:\Write-Host
	}
	if (-not $failure -or $failure.Exception.Message -notlike '*Injected progress logging failure*' -or $launchedIds.Count -ne 1) {
		throw "Expected progress logging failure after the worker started: $failure"
	}
	Assert-HelixFixtureExited $launchedIds[0]

	$launchedIds.Clear()
	$childPath = Join-Path $testDirectory 'child.ps1'
	$childPidPath = Join-Path $testDirectory 'child.pid'
	Set-Content -LiteralPath $childPath -Value 'Start-Sleep -Seconds 60' -Encoding ASCII
	$running = New-HelixTestSubmission 'timeout with child' @"
`$child = Start-Process -FilePath '$($powerShellExe.Replace("'", "''"))' -ArgumentList @('-NoLogo', '-NoProfile', '-File', '"$($childPath.Replace("'", "''"))"') -PassThru
`$child.Id | Set-Content -LiteralPath '$($childPidPath.Replace("'", "''"))'
Start-Sleep -Seconds 60
"@
	$other = New-HelixTestSubmission 'second timeout' 'Start-Sleep -Seconds 60'
	$unrelated = Microsoft.PowerShell.Management\Start-Process -FilePath $powerShellExe `
		-ArgumentList @('-NoLogo', '-NoProfile', '-File', "`"$childPath`"") -PassThru
	$allLaunchedIds.Add($unrelated.Id)
	try {
		$failure = $null
		$timer = [Diagnostics.Stopwatch]::StartNew()
		try {
			Invoke-MauiR2RHelixSubmissions -Submissions @($running, $other) -Timeout ([TimeSpan]::FromSeconds(5)) 6>&1 | Out-Null
		} catch {
			$failure = $_
		}
		if (-not $failure -or $failure.Exception.Message -notlike '*host submissions exceeded*') {
			throw "Expected a local host deadline failure: $failure"
		}
		if ($timer.Elapsed -gt [TimeSpan]::FromSeconds(20) -or $launchedIds.Count -ne 2) {
			throw 'Submission timeout did not bound and join both owned workers.'
		}
		foreach ($processId in $launchedIds) {
			Assert-HelixFixtureExited $processId
		}
		if (-not (Test-Path -LiteralPath $childPidPath)) {
			throw 'The timeout fixture did not start its owned child.'
		}
		Assert-HelixFixtureExited ([int](Get-Content -LiteralPath $childPidPath))
		if ($unrelated.HasExited) {
			throw 'Submission cleanup terminated a process outside its launches.'
		}
	} finally {
		try {
			Stop-MauiR2RHelixSubmission $unrelated
		} finally {
			$unrelated.Dispose()
		}
	}
	Write-Host 'MAUI R2R Helix submission policy tests passed.'
} finally {
	$cleanupErrors = New-Object 'Collections.Generic.List[string]'
	if (Test-Path -LiteralPath (Join-Path $testDirectory 'child.pid')) {
		$allLaunchedIds.Add([int](Get-Content -LiteralPath (Join-Path $testDirectory 'child.pid')))
	}
	foreach ($processId in $allLaunchedIds) {
		try {
			$process = [Diagnostics.Process]::GetProcessById($processId)
		} catch [ArgumentException] {
			continue
		}
		try {
			Stop-MauiR2RHelixSubmission $process
		} catch {
			$cleanupErrors.Add("Fixture process $processId`: $_")
		} finally {
			$process.Dispose()
		}
	}
	Remove-Item -LiteralPath $testDirectory -Recurse -Force
	if ($cleanupErrors.Count -ne 0) {
		throw ($cleanupErrors -join '; ')
	}
}
