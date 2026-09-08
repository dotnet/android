[CmdletBinding()]
param (
	[Parameter(Mandatory = $true)]
	[string] $Destination,
	[string] $AdbPath = 'adb',
	[int] $DeviceTimeoutSeconds = 10,
	[int] $LogcatTimeoutSeconds = 45,
	[int] $TerminationTimeoutSeconds = 2
)

$ErrorActionPreference = 'Stop'

function Write-CaptureWarning {
	param (
		[Parameter(Mandatory = $true)]
		[string] $Message
	)

	$escapedMessage = $Message.Replace('%', '%AZP25').Replace("`r", '%0D').Replace("`n", '%0A')
	Write-Host "##vso[task.logissue type=warning]$escapedMessage"
}

function Read-ProcessOutput {
	param (
		[Parameter(Mandatory = $true)]
		[string] $Path
	)

	if (-not (Test-Path -LiteralPath $Path)) {
		return ''
	}

	$content = Get-Content -LiteralPath $Path -Raw
	if ($null -eq $content) {
		return ''
	}

	return $content.TrimEnd()
}

function Invoke-BoundedProcess {
	param (
		[Parameter(Mandatory = $true)]
		[string] $FilePath,
		[Parameter(Mandatory = $true)]
		[string[]] $Arguments,
		[Parameter(Mandatory = $true)]
		[string] $StandardOutputPath,
		[Parameter(Mandatory = $true)]
		[string] $StandardErrorPath,
		[Parameter(Mandatory = $true)]
		[int] $TimeoutSeconds,
		[Parameter(Mandatory = $true)]
		[int] $TerminationTimeoutSeconds
	)

	$process = $null
	try {
		$process = Start-Process -FilePath $FilePath `
			-ArgumentList $Arguments `
			-NoNewWindow `
			-PassThru `
			-RedirectStandardOutput $StandardOutputPath `
			-RedirectStandardError $StandardErrorPath

		$exited = $process.WaitForExit([int] [TimeSpan]::FromSeconds($TimeoutSeconds).TotalMilliseconds)
		if ($exited) {
			return [PSCustomObject] @{
				ExitCode = $process.ExitCode
				TimedOut = $false
				TerminationTimedOut = $false
				KillError = ''
			}
		}

		$killError = ''
		try {
			$process.Kill($true)
		} catch [InvalidOperationException] {
			# The process exited between the timeout and the kill request.
		} catch {
			$killError = $_.Exception.Message
		}

		$terminated = $process.WaitForExit([int] [TimeSpan]::FromSeconds($TerminationTimeoutSeconds).TotalMilliseconds)
		return [PSCustomObject] @{
			ExitCode = $null
			TimedOut = $true
			TerminationTimedOut = -not $terminated
			KillError = $killError
		}
	} finally {
		if ($null -ne $process) {
			$process.Dispose()
		}
	}
}

function Get-FailureDetails {
	param (
		[string] $StandardError,
		[string] $KillError,
		[bool] $TerminationTimedOut
	)

	$details = @()
	if (-not [string]::IsNullOrWhiteSpace($StandardError)) {
		$details += "stderr: $StandardError"
	}
	if (-not [string]::IsNullOrWhiteSpace($KillError)) {
		$details += "kill failed: $KillError"
	}
	if ($TerminationTimedOut) {
		$details += "process did not exit within $TerminationTimeoutSeconds seconds after termination"
	}

	if ($details.Count -eq 0) {
		return ''
	}

	return '; ' + ($details -join '; ')
}

$temporaryPaths = @()

try {
	$devicesOutputPath = [IO.Path]::GetTempFileName()
	$devicesErrorPath = [IO.Path]::GetTempFileName()
	$logcatErrorPath = [IO.Path]::GetTempFileName()
	$temporaryPaths = @($devicesOutputPath, $devicesErrorPath, $logcatErrorPath)

	$destinationDirectory = Split-Path -Parent $Destination
	if ([string]::IsNullOrEmpty($destinationDirectory)) {
		$destinationDirectory = (Get-Location).Path
	}
	New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

	$devicesResult = Invoke-BoundedProcess `
		-FilePath $AdbPath `
		-Arguments @('devices') `
		-StandardOutputPath $devicesOutputPath `
		-StandardErrorPath $devicesErrorPath `
		-TimeoutSeconds $DeviceTimeoutSeconds `
		-TerminationTimeoutSeconds $TerminationTimeoutSeconds
	$devicesOutput = Read-ProcessOutput -Path $devicesOutputPath
	$devicesError = Read-ProcessOutput -Path $devicesErrorPath

	if (-not [string]::IsNullOrWhiteSpace($devicesOutput)) {
		Write-Host $devicesOutput
	}
	if ($devicesResult.TimedOut) {
		$details = Get-FailureDetails -StandardError $devicesError -KillError $devicesResult.KillError -TerminationTimedOut $devicesResult.TerminationTimedOut
		Write-CaptureWarning "logcat capture skipped: adb devices timed out after $DeviceTimeoutSeconds seconds$details"
		exit 0
	}
	if ($devicesResult.ExitCode -ne 0) {
		$details = Get-FailureDetails -StandardError $devicesError -KillError '' -TerminationTimedOut $false
		Write-CaptureWarning "logcat capture skipped: adb devices exited with code $($devicesResult.ExitCode)$details"
		exit 0
	}
	if (-not [string]::IsNullOrWhiteSpace($devicesError)) {
		Write-Host $devicesError
	}

	$connectedDevice = $devicesOutput -split '\r?\n' | Where-Object { $_ -match '^\S+\s+device(?:\s|$)' } | Select-Object -First 1
	if ($null -eq $connectedDevice) {
		Write-Host 'logcat capture skipped: no connected device'
		exit 0
	}

	$logcatResult = Invoke-BoundedProcess `
		-FilePath $AdbPath `
		-Arguments @('logcat', '-d') `
		-StandardOutputPath $Destination `
		-StandardErrorPath $logcatErrorPath `
		-TimeoutSeconds $LogcatTimeoutSeconds `
		-TerminationTimeoutSeconds $TerminationTimeoutSeconds
	$logcatError = Read-ProcessOutput -Path $logcatErrorPath

	if ($logcatResult.TimedOut) {
		$details = Get-FailureDetails -StandardError $logcatError -KillError $logcatResult.KillError -TerminationTimedOut $logcatResult.TerminationTimedOut
		Write-CaptureWarning "logcat capture timed out after $LogcatTimeoutSeconds seconds; partial output was retained at $Destination$details"
		exit 0
	}
	if ($logcatResult.ExitCode -ne 0) {
		$details = Get-FailureDetails -StandardError $logcatError -KillError '' -TerminationTimedOut $false
		Write-CaptureWarning "logcat capture exited with code $($logcatResult.ExitCode); partial output was retained at $Destination$details"
		exit 0
	}
	if (-not [string]::IsNullOrWhiteSpace($logcatError)) {
		Write-Host $logcatError
	}

	Write-Host "logcat capture completed: $Destination"
} catch {
	Write-CaptureWarning "logcat capture failed: $($_.Exception.Message)"
} finally {
	if ($temporaryPaths.Count -gt 0) {
		Remove-Item -LiteralPath $temporaryPaths -Force -ErrorAction Ignore
	}
}

exit 0
