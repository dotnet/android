$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot 'Invoke-ProcessWithRetry.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
$childScript = Join-Path $testDirectory 'Hang.ps1'
$attemptFile = Join-Path $testDirectory 'attempts.txt'
$retryChildScript = Join-Path $testDirectory 'FailThenSucceed.ps1'
$retryAttemptFile = Join-Path $testDirectory 'retry-attempts.txt'
$callerScript = Join-Path $testDirectory 'Caller.ps1'
$callerResultFile = Join-Path $testDirectory 'caller-results.txt'

try {
	New-Item -ItemType Directory -Path $testDirectory | Out-Null
	@'
param (
	[string] $AttemptFile
)

$attempt = 0
if (Test-Path -LiteralPath $AttemptFile) {
	$attempt = [int] (Get-Content -LiteralPath $AttemptFile -Raw)
}
Set-Content -LiteralPath $AttemptFile -Value ($attempt + 1) -Encoding ASCII
Start-Sleep -Seconds 30
'@ | Set-Content -LiteralPath $childScript -Encoding ASCII

	$childArguments = "-NoLogo -NoProfile -File `"$childScript`" -AttemptFile `"$attemptFile`""
	$stopwatch = [Diagnostics.Stopwatch]::StartNew()
	& $powerShellExe -NoLogo -NoProfile -File $scriptUnderTest `
		-FilePath $powerShellExe `
		-Arguments $childArguments `
		-TimeoutSeconds 1 `
		-RetryCount 1 `
		-RetryDelaySeconds 0
	$exitCode = $LASTEXITCODE
	$stopwatch.Stop()

	if ($exitCode -ne 124) {
		throw "Expected timeout exit code 124, got $exitCode."
	}

	$attempts = [int] (Get-Content -LiteralPath $attemptFile -Raw)
	if ($attempts -ne 2) {
		throw "Expected two process attempts, got $attempts."
	}

	if ($stopwatch.Elapsed.TotalSeconds -lt 1.5 -or $stopwatch.Elapsed.TotalSeconds -gt 10) {
		throw "Expected two bounded one-second attempts, elapsed time was $($stopwatch.Elapsed)."
	}

	@'
param (
	[string] $AttemptFile
)

$attempt = 0
if (Test-Path -LiteralPath $AttemptFile) {
	$attempt = [int] (Get-Content -LiteralPath $AttemptFile -Raw)
}
$attempt++
Set-Content -LiteralPath $AttemptFile -Value $attempt -Encoding ASCII
if ($attempt -eq 1) {
	exit 42
}
'@ | Set-Content -LiteralPath $retryChildScript -Encoding ASCII

	$retryArguments = "-NoLogo -NoProfile -File `"$retryChildScript`" -AttemptFile `"$retryAttemptFile`""
	& $powerShellExe -NoLogo -NoProfile -File $scriptUnderTest `
		-FilePath $powerShellExe `
		-Arguments $retryArguments `
		-TimeoutSeconds 10 `
		-RetryCount 1 `
		-RetryDelaySeconds 0
	$retryExitCode = $LASTEXITCODE

	if ($retryExitCode -ne 0) {
		throw "Expected successful retry exit code 0, got $retryExitCode."
	}

	$retryAttempts = [int] (Get-Content -LiteralPath $retryAttemptFile -Raw)
	if ($retryAttempts -ne 2) {
		throw "Expected two nonzero-exit process attempts, got $retryAttempts."
	}

	@'
param (
	[string] $RetryScript,
	[string] $PowerShellExe,
	[string] $ResultFile
)

$ErrorActionPreference = 'Stop'
$cases = @(
	@{ Command = 'exit 0'; TimeoutSeconds = 10 }
	@{ Command = 'exit 42'; TimeoutSeconds = 10 }
	@{ Command = 'Start-Sleep -Seconds 30'; TimeoutSeconds = 1 }
)
$exitCodes = @()
foreach ($case in $cases) {
	& $RetryScript `
		-FilePath $PowerShellExe `
		-Arguments "-NoLogo -NoProfile -Command $($case.Command)" `
		-TimeoutSeconds $case.TimeoutSeconds
	$exitCodes += $LASTEXITCODE
}
Set-Content -LiteralPath $ResultFile -Value ($exitCodes -join ',') -Encoding ASCII
'@ | Set-Content -LiteralPath $callerScript -Encoding ASCII

	# A separate caller and result file also detect a premature successful exit.
	& $powerShellExe -NoLogo -NoProfile -File $callerScript `
		-RetryScript $scriptUnderTest `
		-PowerShellExe $powerShellExe `
		-ResultFile $callerResultFile
	if ($LASTEXITCODE -ne 0) {
		throw "Expected the calling script to complete successfully, got $LASTEXITCODE."
	}
	if (-not (Test-Path -LiteralPath $callerResultFile)) {
		throw 'The retry helper exited the calling script before it could record the exit codes.'
	}
	$callerExitCodes = (Get-Content -LiteralPath $callerResultFile -Raw).Trim()
	if ($callerExitCodes -ne '0,42,124') {
		throw "Expected the caller to observe exit codes 0,42,124, got '$callerExitCodes'."
	}
} finally {
	Remove-Item -LiteralPath $testDirectory -Recurse -Force -ErrorAction Ignore
}
