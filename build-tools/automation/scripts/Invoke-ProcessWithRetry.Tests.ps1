$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot 'Invoke-ProcessWithRetry.ps1'
$powerShellExe = (Get-Process -Id $PID).Path
if (-not $powerShellExe) {
	$powerShellExe = 'powershell.exe'
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
$childScript = Join-Path $testDirectory 'Hang.ps1'
$attemptFile = Join-Path $testDirectory 'attempts.txt'

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
} finally {
	Remove-Item -LiteralPath $testDirectory -Recurse -Force -ErrorAction Ignore
}
