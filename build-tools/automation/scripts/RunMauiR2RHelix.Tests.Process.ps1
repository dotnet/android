$ErrorActionPreference = 'Stop'

$testScript = Join-Path $PSScriptRoot 'RunMauiR2RHelix.Tests.ps1'
$powerShell = Get-Command 'pwsh' -ErrorAction Stop
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "Maui R2R process $([IO.Path]::GetRandomFileName())"
$testsPassed = $false

function Invoke-TestProcess ([string] $Name, [string []] $AdditionalArguments)
{
	$stdoutPath = Join-Path $testRoot "$Name.out.log"
	$stderrPath = Join-Path $testRoot "$Name.err.log"
	$arguments = @('-NoLogo', '-NoProfile', '-File', "`"$testScript`"") + $AdditionalArguments
	$process = Start-Process -FilePath $powerShell.Source -ArgumentList $arguments -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -PassThru
	# Windows PowerShell requires the process handle to be cached before waiting when output is redirected.
	$null = $process.Handle
	$process.WaitForExit()

	return [pscustomobject]@{
		ExitCode = $process.ExitCode
		StandardOutput = [string](Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction Ignore)
		StandardError = [string](Get-Content -LiteralPath $stderrPath -Raw -ErrorAction Ignore)
	}
}

try {
	New-Item -ItemType Directory -Path $testRoot | Out-Null

	$success = Invoke-TestProcess 'success' @()
	if ($success.ExitCode -ne 0) {
		throw "The successful MAUI R2R Helix suite exited with code $($success.ExitCode). Output:`n$($success.StandardOutput)`n$($success.StandardError)"
	}
	Write-Host $success.StandardOutput.TrimEnd()

	$failure = Invoke-TestProcess 'injected-failure' @('-InjectAssertionFailure')
	if ($failure.ExitCode -eq 0) {
		throw 'The injected MAUI R2R Helix assertion failure unexpectedly exited with code 0.'
	}
	if ($failure.StandardError -notmatch 'Injected process-level assertion failure') {
		throw "The injected MAUI R2R Helix assertion failure did not report the expected error. Output:`n$($failure.StandardOutput)`n$($failure.StandardError)"
	}

	Write-Host 'MAUI R2R Helix process exit-code tests passed.'
	$testsPassed = $true
} finally {
	Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Ignore
}

if ($testsPassed) {
	exit 0
}
